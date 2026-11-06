#!/usr/bin/env bash

set -e

cd "$(dirname "${BASH_SOURCE[0]}")"

for script in publish-*.sh; do
    if [ "$script" != "$(basename "${BASH_SOURCE[0]}")" ]; then
        bash "$script"
    fi
done