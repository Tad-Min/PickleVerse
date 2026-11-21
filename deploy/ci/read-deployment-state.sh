#!/usr/bin/env bash
set -euo pipefail
umask 077
source deploy/ci/ssh-common.sh

{
  printf 'set -euo pipefail\n'
  cat deploy/ci/deployment-state.sh
  cat <<'REMOTE'
cd -- "$1"
check_state_paths
before=$(deployment_token)
pending=false
if [[ -f $pending_file ]]; then pending=true; fi
release=null
if [[ -f $state_file ]]; then release=$(cat -- "$state_file"); fi
after=$(deployment_token)
if [[ $before != "$after" ]]; then
  echo 'Deployment changed while reading its state. Rerun after it finishes.' >&2
  exit 1
fi
printf '{"token":"%s","pending":%s,"release":%s}\n' "$before" "$pending" "$release"
REMOTE
} | sshpass -e ssh -T "${options[@]}" -p "$SSH_PORT" "$target" "bash -s -- '$root'"
