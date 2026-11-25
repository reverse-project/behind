#!/usr/bin/env bash
set -euo pipefail

cd -- "$(dirname -- "${BASH_SOURCE[0]}")"

config_files=(appsettings.json security.json database.json backend.json caching.json frontend.json email.json forum.json markdownviewer.json)

for file in "${config_files[@]}"; do
    path="config/$file"
    if [[ ( -e "$path" || -L "$path" ) && ! -f "$path" ]]; then
        printf '%s\n' "$path must be a regular file. Move the existing path aside before running setup again." >&2
        exit 1
    fi
done

"${1:-docker}" compose run --rm core-tools

for file in "${config_files[@]}"; do
    if [[ ! -f "config/$file" ]]; then
        printf '%s\n' "config/$file was not generated as a regular file." >&2
        exit 1
    fi
done
