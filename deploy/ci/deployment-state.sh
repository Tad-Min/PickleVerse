# Sent to the host. The state contains only Git and image metadata.
state_dir=.deploy
state_file=$state_dir/release.json
pending_file=$state_dir/release.pending

check_state_paths() {
  if [[ -L $state_dir || ( -e $state_dir && ! -d $state_dir ) ]]; then
    echo 'Invalid deployment state directory.' >&2
    return 1
  fi
  for file in "$state_file" "$pending_file"; do
    if [[ -L $file || ( -e $file && ! -f $file ) ]]; then
      echo 'Invalid deployment state file.' >&2
      return 1
    fi
  done
}

deployment_token() {
  {
    if [[ -f $state_file ]]; then sha256sum < "$state_file"; else printf 'first-deploy\n'; fi
    if [[ -f $pending_file ]]; then sha256sum < "$pending_file"; else printf 'clean\n'; fi
  } | sha256sum | cut -d ' ' -f 1
}
