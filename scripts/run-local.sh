#!/usr/bin/env bash
set -euo pipefail

project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
if [[ ! -f "$project_root/.env" ]]; then
  echo "Missing .env. Copy .env.example to .env and set the local database credentials." >&2
  exit 1
fi

set -a
# shellcheck disable=SC1091
source "$project_root/.env"
set +a
: "${ConnectionStrings__DatabaseConnection:?Set ConnectionStrings__DatabaseConnection in .env}"

cd "$project_root"
exec dotnet run --project src/Planning.Server "$@"
