#!/usr/bin/env sh
set -eu

connection_string="${ConnectionStrings__DefaultConnection:-${DEFAULT_CONNECTION_STRING:-}}"

if [ -z "$connection_string" ]; then
  echo "Missing ConnectionStrings__DefaultConnection or DEFAULT_CONNECTION_STRING." >&2
  exit 1
fi

exec /app/efbundle --connection "$connection_string"
