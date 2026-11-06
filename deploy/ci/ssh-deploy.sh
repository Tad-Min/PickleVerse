#!/usr/bin/env bash
set -euo pipefail
umask 077

bundle=${1:?Pass the downloaded release directory}
: "${SSH_HOST:?}" "${SSH_PORT:?}" "${SSH_USER:?}" "${SSHPASS:?}" "${SSH_KNOWN_HOSTS:?}"
: "${DOCKERHUB_PULL_TOKEN:?}" "${DOCKERHUB_USERNAME:?}" "${GH_TOKEN:?}"
[[ $SSH_HOST =~ ^[a-zA-Z0-9][a-zA-Z0-9.-]*$ && $SSH_USER =~ ^[a-zA-Z_][a-zA-Z0-9_-]*$ && $SSH_PORT =~ ^[1-9][0-9]{0,4}$ && $SSH_PORT -le 65535 ]]
[[ $DOCKERHUB_USERNAME =~ ^[a-z0-9][a-z0-9_-]*$ ]]

head_sha=$(curl --fail --silent --show-error -H "Authorization: Bearer $GH_TOKEN" \
  "$GITHUB_API_URL/repos/$GITHUB_REPOSITORY/git/ref/heads/main" | jq -er .object.sha)
if [[ $head_sha != "$GITHUB_SHA" ]]; then
  echo 'A newer main commit exists. Its workflow owns deployment.'
  exit 0
fi

(cd "$bundle" && sha256sum --check SHA256SUMS)
known_hosts=$(mktemp "$RUNNER_TEMP/pkv-known-hosts.XXXXXXXX")
archive=$(mktemp "$RUNNER_TEMP/pkv-upload.XXXXXXXX")
trap 'rm -f "$known_hosts" "$archive"' EXIT
printf '%s\n' "$SSH_KNOWN_HOSTS" > "$known_hosts"
key_name=$SSH_HOST
if [[ $SSH_PORT != 22 ]]; then key_name="[$SSH_HOST]:$SSH_PORT"; fi
ssh-keygen -F "$key_name" -f "$known_hosts" >/dev/null
options=(-o StrictHostKeyChecking=yes -o "UserKnownHostsFile=$known_hosts" -o GlobalKnownHostsFile=/dev/null
  -o PreferredAuthentications=password -o PubkeyAuthentication=no -o NumberOfPasswordPrompts=1
  -o ConnectTimeout=15 -o ServerAliveInterval=15 -o ServerAliveCountMax=4)
target="$SSH_USER@$SSH_HOST"
remote() { sshpass -e ssh -T "${options[@]}" -p "$SSH_PORT" "$target" "$@"; }

incoming=$(remote 'set -eu; root=/run/media/min/Data/pickleverse; mountpoint -q /run/media/min/Data; test "$(realpath -e "$root")" = "$root"; install -d -m 700 "$root/.deploy"; test "$(realpath -e "$root/.deploy")" = "$root/.deploy"; mktemp -d "$root/.deploy/incoming.XXXXXXXX"')
[[ $incoming =~ ^/run/media/min/Data/pickleverse/\.deploy/incoming\.[a-zA-Z0-9]+$ ]]
tar -czf "$archive" -C "$bundle" -- docker-compose.linux.yml src/Database.sql release.env schema.sha256 deploy.sh schema-fingerprint.sh SHA256SUMS
sshpass -e scp "${options[@]}" -P "$SSH_PORT" "$archive" "$target:$incoming/release.tar.gz"
printf '%s\n' "$DOCKERHUB_PULL_TOKEN" | remote "set -eu; cd '$incoming'; tar -xzf release.tar.gz; sha256sum -c SHA256SUMS; bash deploy.sh '$incoming' '$DOCKERHUB_USERNAME'"
