#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."
# Only disposable databases are created by IntegrationChecks. No SMTP delivery.
test "${PKV_TEST_POSTGRES:-}" != '' || { echo 'Cần PKV_TEST_POSTGRES tới PostgreSQL development.'; exit 2; }
test "${PKV_TEST_REDIS:-}" != '' || { echo 'Cần PKV_TEST_REDIS tới Redis development.'; exit 2; }
dotnet run --project src/Testing/Backend -- --check-client-security
dotnet run --project src/Testing/Backend --no-build -- --check-api-contract
dotnet run --project src/Testing/Backend --no-build -- --check-schema
dotnet run --project src/Testing/Backend --no-build
dotnet run --project src/Testing/Backend --no-build -- --check-password-recovery
dotnet run --project src/Testing/Backend --no-build -- --check-online-social
dotnet run --project src/Testing/Backend --no-build -- --check-room-invitations
dotnet run --project src/Testing/Backend --no-build -- --check-dedicated
dotnet run --project src/Testing/Backend --no-build -- --check-hybrid
echo 'Các kiểm tra trên sử dụng database kiểm thử tạm; không gửi email thật.'
