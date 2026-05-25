#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

export LOOPER_NAME="${LOOPER_NAME:-se.test_looper_mix}"
export LOOPER_TAG="${LOOPER_TAG:-test-looper-mix}"
export MIX="${MIX:-0.5}"

exec "${repo_root}/scripts/looper-passthrough-test.sh"
