#!/usr/bin/env bash
set -euo pipefail
umask 077

: "${PKV_DOCKER_REPOSITORY:?}" "${PKV_RELEASE:?}" "${PKV_RELEASE_MANIFEST:?}" "${PKV_STATE_TOKEN:?}" "${GH_TOKEN:?}"
[[ $PKV_DOCKER_REPOSITORY =~ ^[a-z0-9][a-z0-9_-]*/[a-z0-9][a-z0-9_.-]*$ ]]
[[ $PKV_RELEASE =~ ^[0-9a-f]{40}-[0-9]+-[0-9]+$ && $PKV_RELEASE == "$GITHUB_SHA-"* ]]
[[ $PKV_STATE_TOKEN =~ ^[0-9a-f]{64}$ ]]
manifest=$(jq -ce --arg commit "$GITHUB_SHA" --arg repo "$PKV_DOCKER_REPOSITORY" '
  select(.commit == $commit and (.protocol | type == "number" and . >= 1 and . <= 65535))
  | select((.images | keys) == ["ai","api","server","web"])
  | select(all(.images[]; startswith($repo + "@") and (split("@")[1] | test("^sha256:[0-9a-f]{64}$"))))
' <<< "$PKV_RELEASE_MANIFEST")
image_env=$(jq -r '.images | "PKV_API_IMAGE=\(.api)\nPKV_AI_IMAGE=\(.ai)\nPKV_WEB_IMAGE=\(.web)\nPKV_GAME_SERVER_IMAGE=\(.server)"' <<< "$manifest")

head_sha=$(curl --fail --silent --show-error -H "Authorization: Bearer $GH_TOKEN" \
  "$GITHUB_API_URL/repos/$GITHUB_REPOSITORY/git/ref/heads/main" | jq -er .object.sha)
if [[ $head_sha != "$GITHUB_SHA" ]]; then
  echo 'A newer main commit exists. Its workflow owns deployment.'
  exit 0
fi

source deploy/ci/ssh-common.sh
compose_payload=$(base64 -w 0 -- docker-compose.linux.yml)
database_payload=$(base64 -w 0 -- src/Database.sql)
{
  printf 'set -euo pipefail\n'
  cat deploy/ci/deployment-state.sh
  printf 'compose_payload=%s\n' "$compose_payload"
  printf 'database_payload=%s\n' "$database_payload"
  printf 'manifest_payload=%s\n' "$(printf '%s' "$manifest" | base64 -w 0)"
  printf 'image_env_payload=%s\n' "$(printf '%s\n' "$image_env" | base64 -w 0)"
  cat <<'REMOTE'
umask 077

root=$1
release=$2
expected_token=$3
cd -- "$root"
check_state_paths
mkdir -p -- "$state_dir"
if [[ -L $state_dir/release.lock || ( -e $state_dir/release.lock && ! -f $state_dir/release.lock ) ]]; then
  echo 'Invalid deployment lock file.' >&2
  exit 1
fi
exec 9>> "$state_dir/release.lock"
if ! flock -n 9; then
  echo 'Another deployment holds the host lock.' >&2
  exit 1
fi
if [[ $(deployment_token) != "$expected_token" ]]; then
  echo 'The deployment baseline changed since planning. Rerun this workflow.' >&2
  exit 1
fi
if [[ ! -f .env || -L .env ]]; then
  echo "Prepare .env in $root before deploying" >&2
  exit 1
fi
compose_file=docker-compose.linux.yml
database_file=src/Database.sql
if [[ -L src || ( -e src && ! -d src ) ]]; then
  echo 'src must be a directory' >&2
  exit 1
fi
for file in "$compose_file" "$database_file"; do
  if [[ -L $file || ( -e $file && ! -f $file ) ]]; then
    echo "$file must be a regular file" >&2
    exit 1
  fi
done

next_env=$(mktemp .env.XXXXXXXX)
next_compose=
next_database=
next_backup=
next_state=
next_pending=
trap 'rm -f -- "$next_env" "$next_compose" "$next_database" "$next_backup" "$next_state" "$next_pending"' EXIT
next_compose=$(mktemp .compose.XXXXXXXX)
printf '%s' "$compose_payload" | base64 --decode > "$next_compose"
awk '!/^[[:blank:]]*(export[[:blank:]]+)?(PKV_API_IMAGE|PKV_AI_IMAGE|PKV_WEB_IMAGE|PKV_GAME_SERVER_IMAGE)[[:blank:]]*=/' .env > "$next_env"
printf '%s' "$image_env_payload" | base64 --decode >> "$next_env"
unset PKV_API_IMAGE PKV_AI_IMAGE PKV_WEB_IMAGE PKV_GAME_SERVER_IMAGE
unset COMPOSE_PROJECT_NAME COMPOSE_PROFILES
staged_compose=(docker compose --env-file "$next_env" -f "$next_compose")
"${staged_compose[@]}" config --quiet
data_root=$("${staged_compose[@]}" config --environment | sed -n 's/^PKV_DATA_ROOT=//p')
if [[ $data_root != /* || $data_root == *$'\n'* ]]; then
  echo 'PKV_DATA_ROOT in .env must resolve to an absolute Linux directory path.' >&2
  exit 1
fi

backup_postgres() {
  local postgres identity backup_dir backup
  postgres=$(docker ps -aq --filter 'name=^/pickleverse_postgres$')
  if [[ -z $postgres ]]; then
    # A first deployment has no database. Existing data without a running server
    # needs operator attention before deploying over it.
    if [[ -d $data_root/PostgresData && -n $(find "$data_root/PostgresData" -mindepth 1 -maxdepth 1 -print -quit) ]]; then
      echo 'PostgresData exists but the PickleVerse PostgreSQL container is missing.' >&2
      return 1
    fi
    return 0
  fi
  identity=$(docker inspect --format '{{index .Config.Labels "com.docker.compose.project"}}|{{index .Config.Labels "com.docker.compose.service"}}|{{.State.Running}}' "$postgres")
  if [[ $identity != 'pickleverse|postgres|true' ]]; then
    echo 'Start the existing PickleVerse PostgreSQL service before deploying.' >&2
    return 1
  fi

  backup_dir="$root/.deploy/backups"
  if [[ -L $root/.deploy || -L $backup_dir ]]; then
    echo 'PostgreSQL backup directories must not be symbolic links.' >&2
    return 1
  fi
  mkdir -p -- "$backup_dir"
  if [[ $(realpath -e -- "$backup_dir") != "$backup_dir" ]]; then
    echo 'PostgreSQL backup directory must be inside the deployment directory.' >&2
    return 1
  fi
  chmod 0700 -- "$backup_dir"
  backup="$backup_dir/pickleverse-before-$release-$(date -u +%Y%m%dT%H%M%SZ).dump"
  next_backup=$(mktemp "$backup_dir/.postgres.XXXXXXXX")
  docker exec "$postgres" sh -ec '
    [ "$POSTGRES_DB" = pickleverse ]
    export PGPASSWORD="$POSTGRES_PASSWORD"
    exec pg_dump --no-password --username="$POSTGRES_USER" --dbname=pickleverse --format=custom --lock-wait-timeout=10s
  ' > "$next_backup"
  [[ -s $next_backup ]]
  docker exec -i "$postgres" pg_restore --exit-on-error --file=/dev/null < "$next_backup"
  mv -T -- "$next_backup" "$backup"
  next_backup=
  printf 'Verified PostgreSQL backup: %s\n' "$backup"
}

"${staged_compose[@]}" pull --quiet --policy missing backend-api ai-service web game-server
backup_postgres

mkdir -p -- "$data_root"/{PostgresData,RedisData,S3Data}
(umask 022; mkdir -p -- "$data_root/AiService/models")
game_data="$data_root/GameServerData"
if [[ ! -d $game_data ]]; then
  mkdir -- "$game_data"
  if ! "${staged_compose[@]}" run --rm --no-deps -T --interactive=false -u 0 --entrypoint chown \
    game-server 10001:10001 /home/unity/.config/unity3d; then
    rmdir -- "$game_data"
    exit 1
  fi
fi

chmod 0600 "$next_env"
mkdir -p -- src
next_database=$(mktemp src/.database.XXXXXXXX)
printf '%s' "$database_payload" | base64 --decode > "$next_database"

sync_file() {
  local staged_file=$1 destination=$2
  if cmp -s -- "$staged_file" "$destination"; then return; fi
  if [[ -f $destination ]]; then
    chmod --reference="$destination" "$staged_file"
  else
    chmod 0644 "$staged_file"
  fi
  mv -fT -- "$staged_file" "$destination"
  printf 'Synced %s\n' "$destination"
}
next_state=$(mktemp "$state_dir/.release.XXXXXXXX")
printf '%s' "$manifest_payload" | base64 --decode > "$next_state"
next_pending=$(mktemp "$state_dir/.pending.XXXXXXXX")
printf '%s\n' "$release" > "$next_pending"
mv -fT -- "$next_pending" "$pending_file"
sync_file "$next_compose" "$compose_file"
sync_file "$next_database" "$database_file"
mv -f -- "$next_env" .env
compose=(docker compose --env-file .env -f "$compose_file")
"${compose[@]}" up -d --wait --wait-timeout 180
check_http() {
  local endpoint
  endpoint=$("${compose[@]}" port "$1" "$2")
  [[ $endpoint =~ ^127\.0\.0\.1:[0-9]+$ ]]
  curl --fail --silent --show-error --retry 12 --retry-all-errors --max-time 5 "http://$endpoint$3" >/dev/null
}
check_http backend-api 8080 /health/ready
check_http web 80 /healthz
expected_images=$(printf '%s' "$image_env_payload" | base64 --decode)
for service in backend-api ai-service web game-server; do
  container=$("${compose[@]}" ps --quiet "$service")
  [[ -n $container ]]
  actual=$(docker inspect -f '{{.Config.Image}}' "$container")
  case "$service" in
    backend-api) key=PKV_API_IMAGE ;;
    ai-service) key=PKV_AI_IMAGE ;;
    web) key=PKV_WEB_IMAGE ;;
    game-server) key=PKV_GAME_SERVER_IMAGE ;;
  esac
  expected=$(sed -n "s/^$key=//p" <<< "$expected_images")
  [[ -n $expected ]]
  if [[ $actual != "$expected" ]]; then
    echo "Running image does not match the release for $service." >&2
    exit 1
  fi
done
mv -fT -- "$next_state" "$state_file"
rm -- "$pending_file"
printf 'Development release active: %s\n' "$release"
REMOTE
} | sshpass -e ssh -T "${options[@]}" -p "$SSH_PORT" "$target" \
  "bash -s -- '$root' '$PKV_RELEASE' '$PKV_STATE_TOKEN'"
