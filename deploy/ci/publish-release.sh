#!/usr/bin/env bash
set -euo pipefail
umask 077

: "${GITHUB_SHA:?}"
: "${GITHUB_RUN_ID:?}"
: "${GITHUB_RUN_ATTEMPT:?}"
: "${RUNNER_TEMP:?}"
: "${PKV_DOCKER_REPOSITORY:?}" "${PKV_PROTOCOL:?}" "${PKV_BASE_IMAGES:?}"
[[ $GITHUB_SHA =~ ^[0-9a-f]{40}$ && $GITHUB_RUN_ID =~ ^[0-9]+$ && $GITHUB_RUN_ATTEMPT =~ ^[0-9]+$ ]]
[[ $PKV_DOCKER_REPOSITORY =~ ^[a-z0-9][a-z0-9_-]*/[a-z0-9][a-z0-9_.-]*$ ]]
[[ $PKV_PROTOCOL =~ ^[1-9][0-9]{0,4}$ && $PKV_PROTOCOL -le 65535 ]]

native=artifacts/ci/dedicatedserver
release="$GITHUB_SHA-$GITHUB_RUN_ID-$GITHUB_RUN_ATTEMPT"
declare -A images=()
declare -Ar keys=( [api]=PKV_API_IMAGE [ai]=PKV_AI_IMAGE [web]=PKV_WEB_IMAGE [server]=PKV_GAME_SERVER_IMAGE )
for component in api ai web server; do
  if [[ " ${PKV_BUILD_COMPONENTS:-} " != *" $component "* ]]; then
    images[$component]=$(jq -er --arg component "$component" '.[$component]' <<< "$PKV_BASE_IMAGES")
    [[ ${images[$component]} == "$PKV_DOCKER_REPOSITORY@"* && ${images[$component]#*@} =~ ^sha256:[0-9a-f]{64}$ ]]
    continue
  fi
  args=()
  case "$component" in
    api)
      dockerfile=src/Backend/API/Dockerfile
      python3 deploy/api/sanitize_settings.py src/Backend/API "$RUNNER_TEMP/pkv-settings" "$PKV_PROTOCOL"
      cp "$RUNNER_TEMP/pkv-settings"/appsettings*.json src/Backend/API/
      ;;
    ai) dockerfile=src/AiService/Dockerfile ;;
    web) dockerfile=src/Frontend/Dockerfile ;;
    server)
      dockerfile=deploy/game-server/Dockerfile
      [[ $(python3 deploy/ci/verify-native.py "$native" "$GITHUB_SHA") == "$PKV_PROTOCOL" ]]
      args+=(--build-arg "SERVER_BUILD_PATH=$native")
      ;;
  esac
  metadata="$RUNNER_TEMP/pkv-$component-build.json"
  docker buildx build --platform linux/amd64 --push --file "$dockerfile" \
    --metadata-file "$metadata" \
    --label "org.opencontainers.image.revision=$GITHUB_SHA" \
    --label "org.opencontainers.image.version=$release" \
    --tag "$PKV_DOCKER_REPOSITORY:$component-$release" "${args[@]}" .
  digest=$(jq -er '."containerimage.digest" | select(test("^sha256:[0-9a-f]{64}$"))' "$metadata")
  images[$component]="$PKV_DOCKER_REPOSITORY@$digest"
done

# Test the exact four digests selected for deployment.
scratch="$RUNNER_TEMP/pkv-smoke"
mkdir -p "$scratch"/{PostgresData,RedisData,S3Data,AiService/models,GameServerData}
runtime="$RUNNER_TEMP/pkv-smoke.env"
for component in api ai web server; do
  printf '%s=%s\n' "${keys[$component]}" "${images[$component]}"
done > "$runtime"
# These values apply only to the disposable Compose project on the GitHub runner.
# The deployment host uses its own .env for PostgreSQL and pgAdmin passwords.
printf '%s\n' 'POSTGRES_PASSWORD=ci-only-postgres' 'PGADMIN_PASSWORD=ci-only-pgadmin' >> "$runtime"
for key in S3_ACCESS_KEY S3_SECRET_KEY JWT_KEY AI_SERVICE_KEY PKV_GAME_SERVER_KEY; do
  printf '%s=%s\n' "$key" "$(openssl rand -hex 32)" >> "$runtime"
done
printf 'PKV_DATA_ROOT=%s\n' "$scratch" >> "$runtime"
compose=(docker compose --project-name pkv-ci --env-file "$runtime" -f docker-compose.linux.yml)
cleanup() {
  local status=$?
  if ((status != 0)); then "${compose[@]}" ps >&2 || true; fi
  "${compose[@]}" down --timeout 20 || true
}
trap cleanup EXIT
"${compose[@]}" pull --quiet
"${compose[@]}" run --rm --no-deps -u 0 --entrypoint chown game-server 10001:10001 /home/unity/.config/unity3d
"${compose[@]}" up --detach --no-build --pull never --wait --wait-timeout 180
curl --fail --silent --show-error --retry 15 --retry-all-errors http://127.0.0.1:8080/health/ready >/dev/null
curl --fail --silent --show-error http://127.0.0.1:3000/healthz >/dev/null
status=$(curl --silent --show-error --max-time 10 --output /dev/null --write-out '%{http_code}' http://127.0.0.1:3000/api/Profiles/me)
if [[ $status != 401 ]]; then
  echo "Admin web API proxy returned HTTP $status; expected 401 without a token." >&2
  exit 1
fi
api_protocol=$("${compose[@]}" exec -T backend-api cat /app/appsettings.json | jq -er '.DedicatedServer.Protocol')
server_protocol=$("${compose[@]}" exec -T game-server cat /app/build-info.json | jq -er '.protocol')
if [[ $api_protocol != "$PKV_PROTOCOL" || $server_protocol != "$PKV_PROTOCOL" ]]; then
  echo "Dedicated protocol mismatch: release=$PKV_PROTOCOL API=$api_protocol server=$server_protocol" >&2
  exit 1
fi
"${compose[@]}" exec -T ai-service python -c 'import json, urllib.request; data=json.load(urllib.request.urlopen("http://localhost:8000/ready", timeout=5)); assert data["model"] == "unavailable", data'
manifest=$(jq -cn --arg commit "$GITHUB_SHA" --argjson protocol "$PKV_PROTOCOL" \
  --arg api "${images[api]}" --arg ai "${images[ai]}" --arg web "${images[web]}" --arg server "${images[server]}" \
  '{commit:$commit,protocol:$protocol,images:{$api,$ai,$web,$server}}')
printf 'release=%s\nmanifest=%s\n' "$release" "$manifest" >> "$GITHUB_OUTPUT"
{
  printf '### Linux image configuration\n\nBuilt: %s\n\n```dotenv\n' "${PKV_BUILD_COMPONENTS:-none}"
  for component in api ai web server; do
    printf '%s=%s\n' "${keys[$component]}" "${images[$component]}"
  done
  printf '```\n'
} >> "$GITHUB_STEP_SUMMARY"
