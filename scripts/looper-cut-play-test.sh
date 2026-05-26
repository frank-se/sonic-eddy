#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
bin_dir="${repo_root}/fr-sonic/build/src"

looper_bin="${bin_dir}/se-looper"
signal_bin="${bin_dir}/se-signal"
record_bin="${bin_dir}/se-record"

looper_name="${LOOPER_NAME:-se.test_looper_cut_play}"
looper_tag="${LOOPER_TAG:-test-looper-cut-play}"
duration="${DURATION:-12}"
record_duration="${RECORD_DURATION:-8}"
signal_value="${SIGNAL_VALUE:-0.7}"
rms_tolerance="${RMS_TOLERANCE:-0.05}"
start_schedule_index="${START_SCHEDULE_INDEX:-2}"
cut_schedule_index="${CUT_SCHEDULE_INDEX:-3}"

for binary in "${looper_bin}" "${signal_bin}" "${record_bin}"; do
  if [[ ! -x "${binary}" ]]; then
    echo "missing executable: ${binary}" >&2
    echo "build first with: ninja -C fr-sonic/build" >&2
    exit 1
  fi
done

for binary in jq pw-cli pw-dump; do
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

dump_logs() {
  echo
  echo "looper:"
  cat "${looper_log}" 2>/dev/null || true
  echo
  echo "signal:"
  cat "${signal_log}" 2>/dev/null || true
  echo
  echo "record:"
  cat "${record_log}" 2>/dev/null || true
}

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

sync_master_id() {
  pw-dump | jq -r '
    [.[] |
      select(.type == "PipeWire:Interface:Node") |
      select(.info.props["node.name"] == "se.sync_master" or
             .info.props["se.role"] == "sync-master") |
      {
        id: .info.props["object.id"],
        serial: (.info.props["object.serial"] | tonumber? // 0)
      }]
    | sort_by(.serial)
    | last.id // empty'
}

sync_schedule_json() {
  local object_id="$1"
  pw-cli enum-params "${object_id}" Props | awk '
    found {
      if ($1 == "String") {
        sub(/^[[:space:]]*String "/, "")
        sub(/"$/, "")
        print
        exit
      }
    }
    $0 ~ /String "beat\.schedule"/ { found = 1 }
  '
}

scheduled_beat_at() {
  local object_id="$1"
  local index="$2"
  local schedule
  schedule="$(sync_schedule_json "${object_id}")"
  jq -r --argjson index "${index}" '.[$index][0] // .[-1][0] // empty' \
    <<<"${schedule}"
}

echo "starting looper: ${looper_name}"
"${looper_bin}" \
  -n "${looper_name}" \
  -t "${looper_tag}" \
  --mix 1 \
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
  -n se.test_record_cut_play \
  -c "${looper_name}.playback" \
  -d "${record_duration}" \
  --json \
  >"${record_log}" 2>&1 &
pids+=("$!")

sleep 1

echo "starting constant signal into ${looper_name}.capture"
"${signal_bin}" \
  -n se.test_signal_cut_play \
  -p "${looper_name}.capture" \
  -m constant \
  --value "${signal_value}" \
  -d "${record_duration}" \
  --json \
  >"${signal_log}" 2>&1 &
pids+=("$!")

wait_for_log '"rms":0.7' "${signal_log}" 8

sync_id="$(sync_master_id)"
if [[ -z "${sync_id}" ]]; then
  echo "failed to find sync master node" >&2
  exit 1
fi

start_beat="$(scheduled_beat_at "${sync_id}" "${start_schedule_index}")"
if [[ -z "${start_beat}" || "${start_beat}" == "null" ]]; then
  echo "failed to choose transport start beat" >&2
  exit 1
fi

echo "scheduling transport start at beat ${start_beat}"
pw-cli set-param "${sync_id}" Props \
  "{ params = [ \"beat.params\" \"{\\\"transport_state\\\":[[${start_beat},\\\"start_scheduled\\\"]]}\" ] }"

wait_for_log "recording aligned to transport beat=${start_beat}" "${looper_log}" 8

sleep 1

target_beat="$(scheduled_beat_at "${sync_id}" "${cut_schedule_index}")"
if [[ -z "${target_beat}" || "${target_beat}" == "null" ]]; then
  echo "failed to choose target beat" >&2
  exit 1
fi

echo "cutting and playing loop 0 at synced beat ${target_beat} on looper capture object.id=${capture_id}"
pw-cli set-param "${capture_id}" Props \
  "{ params = [ \"commands\" \"[[${target_beat},\\\"cut 1 0\\\"],[${target_beat},\\\"play 0\\\"]]\" ] }"

wait "${pids[1]}"
wait "${pids[2]}"

record_windows="${tmp_dir}/record-windows.tsv"
jq -Rr 'fromjson? | select(.type == "stats" and .scope == "window") | [.frames, .rms, .peak, .min, .max] | @tsv' \
  "${record_log}" >"${record_windows}"

if ! awk -v expected="${signal_value}" \
    -v tolerance="${rms_tolerance}" \
    -v min_initial_silent="3" '
  {
    count += 1
    rms[count] = $2
    peak[count] = $3
  }
  END {
    if (count < 4) {
      print "validation failed: not enough record windows" > "/dev/stderr"
      exit 1
    }
    for (i = 1; i <= min_initial_silent && i <= count; ++i) {
      if (rms[i] > tolerance) {
        printf("validation failed: initial record window %d rms %.9f expected near 0 within %.9f\n",
               i, rms[i], tolerance) > "/dev/stderr"
        exit 1
      }
    }
    matched = 0
    for (i = min_initial_silent + 1; i <= count; ++i) {
      diff = rms[i] - expected
      if (diff < 0)
        diff = -diff
      if (diff <= tolerance)
        matched = 1
    }
    if (!matched) {
      printf("validation failed: no later record window reached expected loop playback rms %.9f within %.9f\n",
             expected, tolerance) > "/dev/stderr"
      exit 1
    }
    printf("validation passed: initial output was silent, then loop playback reached rms %.6f within %.6f\n",
           expected, tolerance)
  }
' "${record_windows}"; then
  dump_logs
  exit 1
fi

dump_logs
