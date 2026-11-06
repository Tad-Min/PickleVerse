#!/usr/bin/env bash
set -euo pipefail
umask 077

: "${GITHUB_SHA:?}"
: "${GITHUB_RUN_ID:?}"
: "${GITHUB_RUN_ATTEMPT:?}"
: "${RUNNER_TEMP:?}"
: "${PKV_DOCKER_REPOSITORY:?}"
[[ $GITHUB_SHA =~ ^[0-9a-f]{40}$ && $GITHUB_RUN_ID =~ ^[0-9]+$ && $GITHUB_RUN_ATTEMPT =~ ^[0-9]+$ ]]
[[ $PKV_DOCKER_REPOSITORY =~ ^[a-z0-9][a-z0-9_-]*/[a-z0-9][a-z0-9_.-]*$ ]]

native=artifacts/ci/dedicatedserver
protocol=$(python3 deploy/ci/verify-native.py "$native" "$GITHUB_SHA")
release="$GITHUB_SHA-$GITHUB_RUN_ID-$GITHUB_RUN_ATTEMPT"
bundle="$RUNNER_TEMP/pkv-release"
mkdir -p "$bundle/src"
cp docker-compose.linux.yml "$bundle/"
cp src/Database.sql "$bundle/src/"
cp deploy/linux/{deploy.sh,schema-fingerprint.sh} "$bundle/"

# Sanitize the context before any build step or intermediate layer sees settings.
python3 deploy/api/sanitize_settings.py src/Backend/API "$RUNNER_TEMP/pkv-settings"
cp "$RUNNER_TEMP/pkv-settings"/appsettings*.json src/Backend/API/

printf 'PKV_RELEASE=%s\nPKV_SOURCE_SHA=%s\nPKV_PROTOCOL=%s\n' "$release" "$GITHUB_SHA" "$protocol" > "$bundle/release.env"
for component in api ai web server; do
  args=()
  case "$component" in
    api) dockerfile=src/Backend/API/Dockerfile ;;
    ai) dockerfile=src/AiService/Dockerfile ;;
    web) dockerfile=src/Frontend/Dockerfile ;;
    server) dockerfile=deploy/game-server/Dockerfile; args+=(--build-arg "SERVER_BUILD_PATH=$native") ;;
  esac
  metadata="$RUNNER_TEMP/pkv-$component.json"
  docker buildx build --platform linux/amd64 --push --file "$dockerfile" \
    --metadata-file "$metadata" --label "org.opencontainers.image.revision=$GITHUB_SHA" \
    --label "org.opencontainers.image.version=$release" \
    --tag "$PKV_DOCKER_REPOSITORY:$component-$release" "${args[@]}" .
  digest=$(jq -er '."containerimage.digest" | select(test("^sha256:[0-9a-f]{64}$"))' "$metadata")
  printf 'PKV_%s_IMAGE=%s@%s\n' "${component^^}" "$PKV_DOCKER_REPOSITORY" "$digest" >> "$bundle/release.env"
done

pin() {
  local key=$1 tag=$2 digest
  digest=$(docker buildx imagetools inspect "$tag" --format '{{json .Manifest.Digest}}' | jq -er 'select(test("^sha256:[0-9a-f]{64}$"))')
  printf '%s=%s@%s\n' "$key" "${tag%:*}" "$digest" >> "$bundle/release.env"
}
pin PKV_POSTGRES_IMAGE postgres:16-alpine
pin PKV_REDIS_IMAGE redis:7.4-alpine
pin PKV_S3_IMAGE ghcr.io/shyim/local-s3:latest

# Test this exact bundle and its schema in disposable storage on the runner.
scratch="$RUNNER_TEMP/pkv-smoke"
mkdir -p "$scratch"/{PostgresData,RedisData,S3Data,AiService/models,GameServerData}
runtime="$RUNNER_TEMP/pkv-smoke.env"
for key in POSTGRES_PASSWORD S3_ACCESS_KEY S3_SECRET_KEY JWT_KEY AI_SERVICE_KEY PKV_GAME_SERVER_KEY; do
  printf '%s=%s\n' "$key" "$(openssl rand -hex 32)" >> "$runtime"
done
printf 'PKV_DATA_ROOT=%s\nPKV_API_BIND=127.0.0.1\n' "$scratch" >> "$runtime"
compose=(docker compose --project-name pkv-ci --env-file "$runtime" --env-file "$bundle/release.env" -f "$bundle/docker-compose.linux.yml")
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
"${compose[@]}" exec -T ai-service python -c 'import json, urllib.request; data=json.load(urllib.request.urlopen("http://localhost:8000/ready", timeout=5)); assert data["model"] == "unavailable", data'
"${compose[@]}" exec -T postgres pg_dump -U sa -d pickleverse --schema-only --no-owner --no-privileges --no-comments --no-security-labels |
  bash "$bundle/schema-fingerprint.sh" > "$bundle/schema.sha256"

(
  cd "$bundle"
  sha256sum docker-compose.linux.yml src/Database.sql release.env schema.sha256 deploy.sh schema-fingerprint.sh > SHA256SUMS
)
printf 'release=%s\n' "$release" >> "$GITHUB_OUTPUT"
