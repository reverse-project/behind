#!/usr/bin/env bash
set -euo pipefail

cd -- "$(dirname -- "${BASH_SOURCE[0]}")"

if [[ -e config/caching.json || -L config/caching.json ]]; then
    if [[ ! -f config/caching.json ]]; then
        printf '%s\n' 'config/caching.json must be a regular file. Move the existing path aside before running setup again.' >&2
        exit 1
    fi
fi

"${1:-docker}" compose run --rm core-tools

test -f config/caching.json
