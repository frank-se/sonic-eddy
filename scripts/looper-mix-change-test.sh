#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
bin_dir="${repo_root}/fr-sonic/build/src"

looper_bin="${bin_dir}/se-looper"
signal_bin="${bin_dir}/se-signal"
record_bin="${bin_dir}/se-record"

looper_name="${LOOPER_NAME:-se.test_looper_mix_change}"
looper_tag="${LOOPER_TAG:-test-looper-mix-change}"
duration="${DURATION:-12}"
record_duration="${RECORD_DURATION:-8}"
signal_mode="${SIGNAL_MODE:-alternating}"
signal_value="${SIGNAL_VALUE:-0.7}"
signal_high_value="${SIGNAL_HIGH_VALUE:-0.9}"
initial_mix="${INITIAL_MIX:-0}"
changed_mix="${CHANGED_MIX:-0.5}"
rms_tolerance="${RMS_TOLERANCE:-0.05}"

for binary in "${looper_bin}" "${signal_bin}" "${record_bin}"; do
  if [[ ! -x "${binary}" ]]; then
    echo "missing executable: ${binary}" >&2
    echo "build first with: ninja -C fr-sonic/build" >&2
    exit 1
  fi
done

for binary in jq pw-cli; do
  if ! command -v "${binary}" >/dev/null 2>&1; then
    echo "missing executable: ${binary}" >&2
    exit 1
  fi
done

tmp_dir="$(mktemp -d)"
looper_log="${tmp_dir}/looper.log"
signal_log="${tmp_dir}/signal.log"
record_log="${tmp_dir}/record.log"
pids=()

cleanup() {
  for pid in "${pids[@]}"; do
    if kill -0 "${pid}" 2>/dev/null; then
      kill "${pid}" 2>/dev/null || true
    fi
  done
  rm -rf "${tmp_dir}"
}
trap cleanup EXIT

wait_for_log() {
  local pattern="$1"
  local file="$2"
  local timeout_seconds="$3"

  for _ in $(seq 1 "${timeout_seconds}"); do
    if grep -q "${pattern}" "${file}" 2>/dev/null; then
      return 0
    fi
    sleep 1
  done

  echo "timed out waiting for '${pattern}' in ${file}" >&2
  cat "${file}" >&2 || true
  exit 1
}

extract_capture_id() {
  awk '
    /purpose=looper-capture/ {
      for (i = 1; i <= NF; ++i) {
        if ($i ~ /^object\.id=/) {
          split($i, parts, "=")
          print parts[2]
          exit
        }
      }
    }
  ' "${looper_log}"
}

echo "starting looper: ${looper_name}"
"${looper_bin}" \
  -n "${looper_name}" \
  -t "${looper_tag}" \
  --mix "${initial_mix}" \
  -d "${duration}" \
  >"${looper_log}" 2>&1 &
pids+=("$!")

wait_for_log "purpose=looper-capture" "${looper_log}" 8
wait_for_log "purpose=looper-playback" "${looper_log}" 8
capture_id="$(extract_capture_id)"
if [[ -z "${capture_id}" ]]; then
  echo "failed to extract looper capture object.id" >&2
  cat "${looper_log}" >&2
  exit 1
fi

echo "starting recorder from ${looper_name}.playback"
"${record_bin}" \
  -n se.test_record_mix_change \
  -c "${looper_name}.playback" \
  -d "${record_duration}" \
  --json \
  >"${record_log}" 2>&1 &
pids+=("$!")

sleep 1

echo "starting signal into ${looper_name}.capture"
"${signal_bin}" \
  -n se.test_signal_mix_change \
  -p "${looper_name}.capture" \
  -m "${signal_mode}" \
  --value "${signal_value}" \
  --high-value "${signal_high_value}" \
  -d "${record_duration}" \
  --json \
  >"${signal_log}" 2>&1 &
pids+=("$!")

sleep "$((record_duration / 2))"
echo "setting mix to ${changed_mix} on looper capture object.id=${capture_id}"
pw-cli set-param "${capture_id}" Props "{ params = [ \"mix\" ${changed_mix} ] }"

wait "${pids[1]}"
wait "${pids[2]}"

signal_windows="${tmp_dir}/signal-windows.tsv"
record_windows="${tmp_dir}/record-windows.tsv"
jq -Rr 'fromjson? | select(.type == "stats" and .scope == "window") | [.frames, .rms, .peak, .min, .max] | @tsv' \
  "${signal_log}" >"${signal_windows}"
jq -Rr 'fromjson? | select(.type == "stats" and .scope == "window") | [.frames, .rms, .peak, .min, .max] | @tsv' \
  "${record_log}" >"${record_windows}"

awk -v tolerance="${rms_tolerance}" \
    -v initial_mix="${initial_mix}" \
    -v changed_mix="${changed_mix}" \
    -v change_at_window="$((record_duration / 2))" '
  FNR == NR {
    signal_count += 1
    signal_rms[signal_count] = $2
    next
  }
  {
    record_count += 1
    record_rms[record_count] = $2
  }
  END {
    comparisons = signal_count
    if (record_count < comparisons)
      comparisons = record_count
    if (comparisons < 3) {
      print "validation failed: not enough window stats" > "/dev/stderr"
      exit 1
    }
    for (i = 2; i <= comparisons; ++i) {
      if (i == change_at_window || i == change_at_window + 1)
        continue
      mix = i < change_at_window ? initial_mix : changed_mix
      expected = signal_rms[i] * (1.0 - mix)
      diff = expected - record_rms[i]
      if (diff < 0)
        diff = -diff
      if (diff > tolerance) {
        printf("validation failed: signal window %d expected record rms %.9f with mix %.6f vs record window %d rms %.9f diff %.9f tolerance %.9f\n",
               i, expected, mix, i, record_rms[i], diff, tolerance) > "/dev/stderr"
        exit 1
      }
    }
    printf("validation passed: mix changed from %.6f to %.6f; compared same-window RMS values with tolerance %.6f\n",
           initial_mix, changed_mix, tolerance)
  }
' "${signal_windows}" "${record_windows}"

echo
echo "looper:"
cat "${looper_log}"
echo
echo "signal:"
cat "${signal_log}"
echo
echo "record:"
cat "${record_log}"
