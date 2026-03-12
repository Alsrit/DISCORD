#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PUBLISH_DIR="${ROOT_DIR}/publish"

echo "==> Restoring tools"
dotnet tool restore

echo "==> Building solution"
dotnet build "${ROOT_DIR}/SecureLicensePlatform.sln" -c Release

echo "==> Publishing API"
dotnet publish "${ROOT_DIR}/src/Platform.Api/Platform.Api.csproj" -c Release -o "${PUBLISH_DIR}/api"

echo "==> Publishing Admin"
dotnet publish "${ROOT_DIR}/src/Platform.Admin/Platform.Admin.csproj" -c Release -o "${PUBLISH_DIR}/admin"

echo "==> Publishing Worker"
dotnet publish "${ROOT_DIR}/src/Platform.Worker/Platform.Worker.csproj" -c Release -o "${PUBLISH_DIR}/worker"

echo "==> Done"
echo "Publish output:"
echo "  ${PUBLISH_DIR}/api"
echo "  ${PUBLISH_DIR}/admin"
echo "  ${PUBLISH_DIR}/worker"
echo
echo "Next steps:"
echo "  1. docker compose -f deploy/docker-compose.platform.yml up -d"
echo "  2. dotnet ef database update --project src/Platform.Infrastructure/Platform.Infrastructure.csproj --startup-project src/Platform.Api/Platform.Api.csproj"
echo "  3. Install systemd units from deploy/systemd/"
