#!/usr/bin/env bash

set -e

for script in publish-*.sh; do
    if [ "$script" != "$0" ]; then
        bash "$script"
    fi
done