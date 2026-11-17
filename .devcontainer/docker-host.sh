#!/usr/bin/env bash
set -euo pipefail

endpoint="${DOCKER_HOST-unix:///run/podman/podman.sock}"
if [[ "$endpoint" == "unix://${XDG_RUNTIME_DIR-}/podman/podman.sock" ]]; then
  endpoint="unix:///run/podman/podman.sock"
fi
if [[ "$endpoint" == *$'\n'* || "$endpoint" == *$'\r'* ]]; then
  printf '%s\n' 'DOCKER_HOST must not contain newlines' >&2
  exit 1
fi

env_file="$1"
temporary_file="$(mktemp "${env_file}.XXXXXX")"
trap 'rm -f "$temporary_file"' EXIT
printf 'DOCKER_HOST=%s\n' "$endpoint" > "$temporary_file"
mv -f "$temporary_file" "$env_file"
