# Sourced by jobs that access the deployment host.
: "${SSH_HOST:?}" "${SSH_PORT:?}" "${SSH_USER:?}" "${SSHPASS:?}" "${SSH_KNOWN_HOSTS:?}"
[[ $SSH_HOST =~ ^[a-zA-Z0-9][a-zA-Z0-9.-]*$ && $SSH_USER =~ ^[a-zA-Z_][a-zA-Z0-9_-]*$ && $SSH_PORT =~ ^[1-9][0-9]{0,4}$ && $SSH_PORT -le 65535 ]]

root=${PKV_DEPLOY_ROOT:-${PKV_DATA_ROOT:-/run/media/min/Data/pickleverse}}
root=${root%/}
if [[ ! $root =~ ^(/[a-zA-Z0-9._-]+)+$ || $root/ == */../* || $root/ == */./* ]]; then
  echo 'PKV_DEPLOY_ROOT must be an absolute Linux directory path, such as /opt/pickleverse.' >&2
  exit 1
fi

known_hosts=$(mktemp "$RUNNER_TEMP/pkv-known-hosts.XXXXXXXX")
trap 'rm -f -- "$known_hosts"' EXIT
printf '%s\n' "$SSH_KNOWN_HOSTS" > "$known_hosts"
key_name=$SSH_HOST
if [[ $SSH_PORT != 22 ]]; then key_name="[$SSH_HOST]:$SSH_PORT"; fi
ssh-keygen -F "$key_name" -f "$known_hosts" >/dev/null
options=(-o StrictHostKeyChecking=yes -o "UserKnownHostsFile=$known_hosts" -o GlobalKnownHostsFile=/dev/null
  -o PreferredAuthentications=password -o PubkeyAuthentication=no -o NumberOfPasswordPrompts=1
  -o ConnectTimeout=15 -o ServerAliveInterval=15 -o ServerAliveCountMax=4)
target="$SSH_USER@$SSH_HOST"
