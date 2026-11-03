#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."
if [[ "${1:-}" != "--reset-pickleverse-development" ]]; then
  echo 'Dùng: bash scripts/reset-development-database.sh --reset-pickleverse-development'; exit 2
fi
project_container=pickleverse_postgres
project_database=pickleverse
test "$(docker inspect -f '{{.Config.Image}}' "$project_container")" = postgres:16-alpine
test "$(docker inspect -f '{{.Config.Image}}' pickleverse_redis)" = redis:7.4-alpine
docker inspect pickleverse_api --format '{{range .Config.Env}}{{println .}}{{end}}' |
  awk '/^ConnectionStrings__DefaultConnection=/ {if ($0 ~ /Host=postgres;/ && $0 ~ /Database=pickleverse;/) found=1} /^ASPNETCORE_ENVIRONMENT=Development$/ {dev=1} /^Redis__KeyPrefix=/ && $0 != "Redis__KeyPrefix=pickleverse:" {badPrefix=1} END {exit !(found && dev && !badPrefix)}'
test "$(docker exec "$project_container" psql -U sa -d "$project_database" -Atqc 'SELECT current_database()')" = "$project_database"
echo 'Đã xác minh container pickleverse_postgres / database pickleverse của development.'
mkdir -p artifacts/development-backups
backup="artifacts/development-backups/pickleverse-$(date -u +%Y%m%d-%H%M%S).dump"
docker exec "$project_container" pg_dump -U sa -Fc "$project_database" > "$backup"
docker compose -f docker-compose.yml -f docker-compose.dedicated.yml --profile dedicated stop game-server
docker stop pickleverse_api
docker cp src/Database.sql "$project_container:/tmp/pickleverse-init.sql"
docker exec "$project_container" psql -U sa -d postgres -v ON_ERROR_STOP=1 -c 'DROP DATABASE pickleverse WITH (FORCE);'
docker exec "$project_container" psql -U sa -d postgres -v ON_ERROR_STOP=1 -c 'CREATE DATABASE pickleverse OWNER sa;'
docker exec "$project_container" psql -U sa -d "$project_database" -v ON_ERROR_STOP=1 --file=/tmp/pickleverse-init.sql
# Only this project's configured keyspace; never FLUSHDB/FLUSHALL.
docker exec pickleverse_redis redis-cli EVAL 'local c="0"; local n=0; repeat local r=redis.call("SCAN",c,"MATCH","pickleverse:*","COUNT",100); c=r[1]; if #r[2]>0 then n=n+redis.call("UNLINK",unpack(r[2])); end until c=="0"; return n' 0
echo "Schema đã tạo lại từ Database.sql. Backup: $backup. Khởi động API image mới trước khi thử client."
