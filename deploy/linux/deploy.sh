#!/usr/bin/env bash
set -euo pipefail
umask 077

root=/run/media/min/Data/pickleverse
state="$root/.deploy"
incoming=${1:?Pass the uploaded release directory}
registry_user=${2:?Pass the Docker Hub username}
die() { echo "Deploy: $*" >&2; exit 1; }

[[ $registry_user =~ ^[a-z0-9][a-z0-9_-]*$ ]] || die 'invalid Docker Hub username'
mountpoint -q /run/media/min/Data || die 'persistent Data mount is unavailable'
[[ $(realpath -e "$root") == "$root" && $(realpath -e "$state") == "$state" ]] || die 'unexpected deployment root'
[[ $incoming == "$state"/incoming.* && $(realpath -e "$incoming") == "$incoming" ]] || die 'unexpected upload directory'
[[ -f $root/.env && ! -L $root/.env ]] || die 'the host .env is required'
docker_socket=$(docker context inspect --format '{{.Endpoints.docker.Host}}')
if [[ -z ${DOCKER_CONTEXT:-} && -n ${DOCKER_HOST:-} ]]; then docker_socket=$DOCKER_HOST; fi
[[ $docker_socket == unix://* ]] || die 'a local Docker daemon is required'
[[ $(docker info --format '{{.OSType}}/{{.Architecture}}') =~ ^linux/(x86_64|amd64)$ ]] || die 'Docker must be Linux amd64'
docker compose up --help | grep -q -- '--wait-timeout' || die 'Docker Compose must support up --wait-timeout'

exec 9>"$state/lock"
flock -w 300 9 || die 'another deployment holds the host lock'
(cd "$incoming" && sha256sum --check --status SHA256SUMS) || die 'release checksum mismatch'
release=$(sed -n 's/^PKV_RELEASE=//p' "$incoming/release.env")
expected_schema=$(cat "$incoming/schema.sha256")
[[ $release =~ ^[0-9a-f]{40}-[0-9]+-[0-9]+$ && $expected_schema =~ ^[0-9a-f]{64}$ ]] || die 'invalid release metadata'

[[ $(realpath -m "$state/releases") == "$state/releases" ]] || die 'release directory must remain under the deployment root'
mkdir -p "$state/releases"
bundle="$state/releases/$release"
if [[ -e $bundle ]]; then
  [[ $(realpath -e "$bundle") == "$bundle" ]] && cmp -s "$incoming/SHA256SUMS" "$bundle/SHA256SUMS" || die 'release ID already exists with different content'
  (cd "$bundle" && sha256sum --check --status SHA256SUMS) || die 'stored release checksum mismatch'
else
  mkdir -p "$bundle/src"
  cp "$incoming/"{docker-compose.linux.yml,release.env,schema.sha256,schema-fingerprint.sh,deploy.sh,SHA256SUMS} "$bundle/"
  cp "$incoming/src/Database.sql" "$bundle/src/"
fi

previous=''
if [[ -f $state/current ]]; then
  previous=$(cat "$state/current")
  [[ $previous =~ ^[0-9a-f]{40}-[0-9]+-[0-9]+$ && -f $state/releases/$previous/release.env ]] || die 'invalid current release pointer'
fi

# Shell exports take precedence over Compose env files. Use only the locked release values.
unset PKV_RELEASE PKV_SOURCE_SHA PKV_PROTOCOL PKV_API_IMAGE PKV_AI_IMAGE PKV_WEB_IMAGE PKV_SERVER_IMAGE PKV_POSTGRES_IMAGE PKV_REDIS_IMAGE PKV_S3_IMAGE
export PKV_DATA_ROOT="$root"
compose_for() {
  local directory=$1
  shift
  docker compose --project-name pickleverse --env-file "$root/.env" --env-file "$directory/release.env" \
    -f "$directory/docker-compose.linux.yml" "$@"
}
compose() { compose_for "$bundle" "$@"; }
compose config --quiet

service_container() {
  local ids workdir
  ids=$(docker ps -aq --filter label=com.docker.compose.project=pickleverse --filter "label=com.docker.compose.service=$1")
  [[ $ids != *$'\n'* ]] || die "multiple old containers for $1"
  if [[ -n $ids ]]; then
    workdir=$(docker inspect --format '{{index .Config.Labels "com.docker.compose.project.working_dir"}}' "$ids")
    [[ $workdir == "$root" || $workdir == "$state/releases/"* ]] || die "unexpected Compose project directory for $1"
  fi
  printf '%s' "$ids"
}
schema_of() {
  docker exec "$1" pg_dump -U sa -d pickleverse --schema-only --no-owner --no-privileges --no-comments --no-security-labels |
    bash "$bundle/schema-fingerprint.sh"
}
check_database() {
  local digest version
  version=$(docker exec "$1" psql -U sa -d pickleverse -Atq -v ON_ERROR_STOP=1 -c 'SHOW server_version_num;')
  [[ $version =~ ^16[0-9]{4}$ ]] || die 'PostgreSQL major version must be 16'
  digest=$(schema_of "$1") || die 'could not read the development database schema'
  [[ $digest == "$expected_schema" ]] || die 'the existing schema differs from the release; data was preserved'
}
check_idle() {
  local active
  active=$(docker exec "$1" psql -U sa -d pickleverse -Atq -v ON_ERROR_STOP=1 -c "SELECT count(*) FROM gamematches WHERE status IN ('PREPARING','PLAYING');")
  [[ $active == 0 ]] || die 'there are active matches; retry the deployment after they finish'
}
empty_dir() {
  local entry
  entry=$(find "$1" -mindepth 1 -maxdepth 1 -print -quit) || die "cannot inspect storage directory $1"
  [[ -z $entry ]]
}

services=(postgres redis s3-storage ai-service game-server)
destinations=(PostgresData RedisData S3Data AiService/models GameServerData)
mount_targets=(/var/lib/postgresql/data /data /data /models /home/unity/.config/unity3d)
legacy_volumes=(pickleverse_dev_postgres_data pickleverse_redis_data pickleverse_dev_s3_data '' pickleverse_dev_game_server_data)
volume_labels=(postgres_data redis_data s3_data '' game_server_data)
declare -A sources=() source_volumes=()
for index in "${!services[@]}"; do
  service=${services[$index]}
  directory="$root/${destinations[$index]}"
  [[ $(realpath -m "$directory") == "$directory" ]] || die "invalid bind directory for $service"
  mkdir -p "$directory"
  old=$(service_container "$service")
  if [[ -z $old ]]; then
    if empty_dir "$directory" && [[ -n ${legacy_volumes[$index]} ]]; then
      labelled=$(docker volume ls -q --filter label=com.docker.compose.project=pickleverse --filter "label=com.docker.compose.volume=${volume_labels[$index]}")
      if [[ -n $labelled ]] || docker volume inspect "${legacy_volumes[$index]}" >/dev/null 2>&1; then
        die "detached legacy volume for $service; attach the previous stack before the first deployment"
      fi
    fi
    continue
  fi
  mount=$(docker inspect --format "{{range .Mounts}}{{if eq .Destination \"${mount_targets[$index]}\"}}{{.Type}}|{{.Name}}|{{.Source}}{{end}}{{end}}" "$old")
  IFS='|' read -r type name source <<< "$mount"
  [[ $type == bind || $type == volume ]] || die "cannot identify the old data mount for $service"
  [[ $type != bind || $source != "$directory" ]] || continue
  if ! empty_dir "$directory"; then
    die "old storage exists and the new directory for $service already contains data"
  fi
  if [[ $type == bind ]]; then
    [[ $source == "$root/"* && $(realpath -e "$source") == "$source" ]] || die "old bind mount for $service is outside the authorized root"
    sources[$index]="type=bind,src=$source,dst=/source,readonly"
    source_volumes[$index]=$source
  else
    [[ $name =~ ^[a-zA-Z0-9][a-zA-Z0-9_.-]*$ ]] || die "invalid volume name for $service"
    sources[$index]="type=volume,src=$name,dst=/source,readonly"
    source_volumes[$index]=$name
  fi
done

postgres=$(service_container postgres)
if [[ -n $postgres ]]; then
  [[ $(docker inspect --format '{{.State.Running}}' "$postgres") == true ]] || die 'start the previous PostgreSQL before deployment'
  check_database "$postgres"
  check_idle "$postgres"
  backup=$(mktemp -d "$root/.deploy/backup.$release.XXXXXXXX")
  docker exec "$postgres" pg_dump -U sa -d pickleverse --format=custom > "$backup/postgres.dump.tmp"
  [[ -s $backup/postgres.dump.tmp ]] || die 'PostgreSQL backup is empty'
  mv "$backup/postgres.dump.tmp" "$backup/postgres.dump"
  echo "PostgreSQL backup saved in $backup"
fi

read -r registry_token || die 'missing Docker Hub pull token'
[[ -n $registry_token ]] || die 'missing Docker Hub pull token'
export DOCKER_CONFIG
DOCKER_CONFIG=$(mktemp -d "$state/docker.XXXXXXXX")
export DOCKER_HOST="$docker_socket"
unset DOCKER_CONTEXT
old_running=()
phase=preflight
finish() {
  local status=$?
  if ((status != 0)); then
    if [[ $phase == copying && ${#old_running[@]} -gt 0 ]]; then
      docker start "${old_running[@]}" >/dev/null || true
    elif [[ $phase == starting && -n $previous && $previous != "$release" && $(cat "$state/releases/$previous/schema.sha256") == "$expected_schema" ]]; then
      echo "Restoring the prior image set $previous" >&2
      compose_for "$state/releases/$previous" up --detach --no-build --pull never --wait --wait-timeout 180 || true
    elif [[ $phase == starting ]]; then
      compose stop web game-server backend-api ai-service postgres redis s3-storage || true
      echo 'Cutover failed. Check the retained storage and PostgreSQL backup before recovery.' >&2
    fi
  fi
  rm -f "$DOCKER_CONFIG/config.json"
  rmdir "$DOCKER_CONFIG" 2>/dev/null || true
  exit "$status"
}
trap finish EXIT
printf '%s\n' "$registry_token" | docker login --username "$registry_user" --password-stdin >/dev/null
unset registry_token
compose pull --quiet
if [[ -n $postgres ]]; then check_idle "$postgres"; fi

if ((${#sources[@]} > 0)); then
  phase=copying
  for service in web game-server backend-api ai-service postgres redis s3-storage; do
    old=$(service_container "$service")
    if [[ -n $old && $(docker inspect --format '{{.State.Running}}' "$old") == true ]]; then
      old_running+=("$old")
      docker stop --time 20 "$old" >/dev/null
    fi
  done
  stage=$(mktemp -d "$state/migrate.$release.XXXXXXXX")
  helper=$(sed -n 's/^PKV_POSTGRES_IMAGE=//p' "$bundle/release.env")
  [[ $helper =~ ^postgres@sha256:[0-9a-f]{64}$ ]] || die 'invalid data copy image'
  for index in "${!sources[@]}"; do
    [[ -z $(docker ps -q --filter "volume=${source_volumes[$index]}") ]] || die 'a running container still uses the old storage'
    mkdir -p "$stage/$index"
    docker run --rm --network none --user 0:0 --entrypoint sh \
      --mount "${sources[$index]}" --mount "type=bind,src=$stage/$index,dst=/destination" "$helper" \
      -ec 'cp -a /source/. /destination/'
  done
  for index in "${!sources[@]}"; do
    directory="$root/${destinations[$index]}"
    rmdir "$directory"
    mv "$stage/$index" "$directory"
  done
  rmdir "$stage"
fi

compose run --rm --no-deps -u 0 --entrypoint chown game-server 10001:10001 /home/unity/.config/unity3d
phase=starting
compose up --detach --no-build --pull never --wait --wait-timeout 120 postgres
check_database "$(service_container postgres)"
compose up --detach --no-build --pull never --wait --wait-timeout 180

check_http() {
  local endpoint host port
  endpoint=$(compose port "$1" "$2")
  host=${endpoint%:*}
  port=${endpoint##*:}
  case "$host" in 0.0.0.0|'[::]'|'::') host=127.0.0.1 ;; esac
  [[ $port =~ ^[0-9]+$ ]] || die "invalid health endpoint for $1"
  curl --fail --silent --show-error --retry 10 --retry-all-errors --max-time 5 "http://$host:$port$3" >/dev/null
}
check_http backend-api 8080 /health/ready
check_http web 80 /healthz
compose exec -T ai-service python -c 'import json, urllib.request; data=json.load(urllib.request.urlopen("http://localhost:8000/ready", timeout=5)); assert data["model"] in ("enabled", "unavailable"), data; print("ML status:", data["model"])'

printf '%s\n' "$release" > "$state/current.tmp"
mv -f "$state/current.tmp" "$state/current"
phase=complete
echo "Development release active: $release"
