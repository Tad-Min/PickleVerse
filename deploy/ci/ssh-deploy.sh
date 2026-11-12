#!/usr/bin/env bash
set -euo pipefail
umask 077

: "${SSH_HOST:?}" "${SSH_PORT:?}" "${SSH_USER:?}" "${SSHPASS:?}" "${SSH_KNOWN_HOSTS:?}"
: "${PKV_DOCKER_REPOSITORY:?}" "${PKV_RELEASE:?}" "${GH_TOKEN:?}"
[[ $SSH_HOST =~ ^[a-zA-Z0-9][a-zA-Z0-9.-]*$ && $SSH_USER =~ ^[a-zA-Z_][a-zA-Z0-9_-]*$ && $SSH_PORT =~ ^[1-9][0-9]{0,4}$ && $SSH_PORT -le 65535 ]]
[[ $PKV_DOCKER_REPOSITORY =~ ^[a-z0-9][a-z0-9_-]*/[a-z0-9][a-z0-9_.-]*$ ]]
[[ $PKV_RELEASE =~ ^[0-9a-f]{40}-[0-9]+-[0-9]+$ && $PKV_RELEASE == "$GITHUB_SHA-"* ]]

root=${PKV_DEPLOY_ROOT:-${PKV_DATA_ROOT:-/run/media/min/Data/pickleverse}}
root=${root%/}
if [[ ! $root =~ ^(/[a-zA-Z0-9._-]+)+$ ]]; then
  echo 'PKV_DEPLOY_ROOT must be an absolute Linux directory path, such as /opt/pickleverse.' >&2
  exit 1
fi

head_sha=$(curl --fail --silent --show-error -H "Authorization: Bearer $GH_TOKEN" \
  "$GITHUB_API_URL/repos/$GITHUB_REPOSITORY/git/ref/heads/main" | jq -er .object.sha)
if [[ $head_sha != "$GITHUB_SHA" ]]; then
  echo 'A newer main commit exists. Its workflow owns deployment.'
  exit 0
fi

known_hosts=$(mktemp "$RUNNER_TEMP/pkv-known-hosts.XXXXXXXX")
trap 'rm -f "$known_hosts"' EXIT
printf '%s\n' "$SSH_KNOWN_HOSTS" > "$known_hosts"
key_name=$SSH_HOST
if [[ $SSH_PORT != 22 ]]; then key_name="[$SSH_HOST]:$SSH_PORT"; fi
ssh-keygen -F "$key_name" -f "$known_hosts" >/dev/null
options=(-o StrictHostKeyChecking=yes -o "UserKnownHostsFile=$known_hosts" -o GlobalKnownHostsFile=/dev/null
  -o PreferredAuthentications=password -o PubkeyAuthentication=no -o NumberOfPasswordPrompts=1
  -o ConnectTimeout=15 -o ServerAliveInterval=15 -o ServerAliveCountMax=4)
target="$SSH_USER@$SSH_HOST"
compose_payload=$(base64 -w 0 -- docker-compose.linux.yml)
database_payload=$(base64 -w 0 -- src/Database.sql)
{
  printf 'compose_payload=%s\n' "$compose_payload"
  printf 'database_payload=%s\n' "$database_payload"
  cat <<'REMOTE'
set -euo pipefail
umask 077

root=$1
repository=$2
release=$3
cd -- "$root"
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
trap 'rm -f -- "$next_env" "$next_compose" "$next_database"' EXIT
next_compose=$(mktemp .compose.XXXXXXXX)
printf '%s' "$compose_payload" | base64 --decode > "$next_compose"
awk -v repository="$repository" -v release="$release" '
  /^[[:blank:]]*(export[[:blank:]]+)?DOCKER_REPOSITORY[[:blank:]]*=/ {
    print "DOCKER_REPOSITORY=" repository
    repository_set = 1
    next
  }
  /^[[:blank:]]*(export[[:blank:]]+)?DOCKER_TAG_RELEASE[[:blank:]]*=/ {
    print "DOCKER_TAG_RELEASE=" release
    release_set = 1
    next
  }
  { print }
  END {
    if (!repository_set) print "DOCKER_REPOSITORY=" repository
    if (!release_set) print "DOCKER_TAG_RELEASE=" release
  }
' .env > "$next_env"
unset DOCKER_REPOSITORY DOCKER_TAG_RELEASE
docker compose --env-file "$next_env" -f "$next_compose" config --quiet
chmod --reference=.env "$next_env"
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
sync_file "$next_compose" "$compose_file"
sync_file "$next_database" "$database_file"
mv -f -- "$next_env" .env
docker compose --env-file .env -f "$compose_file" up -d --wait --wait-timeout 180
printf 'Development release active: %s\n' "$release"
REMOTE
} | sshpass -e ssh -T "${options[@]}" -p "$SSH_PORT" "$target" \
  "bash -s -- '$root' '$PKV_DOCKER_REPOSITORY' '$PKV_RELEASE'"
