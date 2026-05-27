#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

tests=(
  "passthrough:scripts/looper-passthrough-test.sh"
  "mix:scripts/looper-mix-test.sh"
  "mix-change:scripts/looper-mix-change-test.sh"
  "cut-play:scripts/looper-cut-play-test.sh"
  "sync-cut-play:scripts/looper-sync-cut-play-test.sh"
  "recording-transport:scripts/looper-recording-transport-test.sh"
  "sample-sync:scripts/looper-sample-sync-test.sh"
)

tmp_dir="$(mktemp -d)"
keep_logs="${KEEP_LOGS:-0}"

cleanup() {
  if [[ "${keep_logs}" != "1" ]]; then
    rm -rf "${tmp_dir}"
  fi
}
trap cleanup EXIT

pass_count=0
fail_count=0
failed_tests=()

echo "running ${#tests[@]} looper integration tests"
echo "logs: ${tmp_dir}"
echo

for test_entry in "${tests[@]}"; do
  name="${test_entry%%:*}"
  script="${test_entry#*:}"
  log_file="${tmp_dir}/${name}.log"

  printf "%-24s" "${name}"
  if (cd "${repo_root}" && "./${script}") >"${log_file}" 2>&1; then
    echo "PASS"
    pass_count=$((pass_count + 1))
  else
    echo "FAIL"
    fail_count=$((fail_count + 1))
    failed_tests+=("${name}")
    echo "  log: ${log_file}"
    tail -n 80 "${log_file}" | sed 's/^/  | /'
  fi
  sleep "${TEST_SETTLE_SECONDS:-1}"
done

echo
echo "summary: PASS=${pass_count} FAIL=${fail_count}"

if ((fail_count > 0)); then
  echo "failed: ${failed_tests[*]}"
  echo "logs retained: ${tmp_dir}"
  keep_logs=1
  exit 1
fi

if [[ "${KEEP_LOGS:-0}" == "1" ]]; then
  echo "logs retained: ${tmp_dir}"
fi
