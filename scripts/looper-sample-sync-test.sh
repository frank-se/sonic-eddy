#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
bin_dir="${repo_root}/fr-sonic/build/src"

looper_bin="${bin_dir}/se-looper"
signal_bin="${bin_dir}/se-signal"
record_bin="${bin_dir}/se-record"

looper_name="${LOOPER_NAME:-se.test_looper_sample_sync}"
looper_tag="${LOOPER_TAG:-test-looper-sample-sync}"
duration="${DURATION:-18}"
record_duration="${RECORD_DURATION:-12}"
signal_duration="${SIGNAL_DURATION:-12}"
rate="${RATE:-48000}"
tolerance_frames="${TOLERANCE_FRAMES:-512}"
start_schedule_index="${START_SCHEDULE_INDEX:-2}"
play_schedule_index="${PLAY_SCHEDULE_INDEX:--1}"

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
  tail -n 140 "${looper_log}" 2>/dev/null || true
  echo
  echo "signal:"
  tail -n 60 "${signal_log}" 2>/dev/null || true
  echo
  echo "record:"
  tail -n 80 "${record_log}" 2>/dev/null || true
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
  tail -n 140 "${file}" >&2 || true
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

scheduled_beat_field_at() {
  local object_id="$1"
  local index="$2"
  local field="$3"
  local schedule
  schedule="$(sync_schedule_json "${object_id}")"
  jq -r --argjson index "${index}" --argjson field "${field}" \
    '.[$index][$field] // .[-1][$field] // empty' <<<"${schedule}"
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
  -n se.test_record_sample_sync \
  -c "${looper_name}.playback" \
  -d "${record_duration}" \
  -r "${rate}" \
  --json \
  --transitions-json \
  >"${record_log}" 2>&1 &
pids+=("$!")

sleep 1

echo "starting beat-alternating signal into ${looper_name}.capture"
"${signal_bin}" \
  -n se.test_signal_sample_sync \
  -p "${looper_name}.capture" \
  -m beat-alternating \
  --value 0 \
  --high-value 1 \
  -d "${signal_duration}" \
  -r "${rate}" \
  --json \
  >"${signal_log}" 2>&1 &
pids+=("$!")

wait_for_log '"source":"beat-alternating"' "${signal_log}" 8

sync_id="$(sync_master_id)"
if [[ -z "${sync_id}" ]]; then
  echo "failed to find sync master node" >&2
  exit 1
fi

start_beat="$(scheduled_beat_field_at "${sync_id}" "${start_schedule_index}" 0)"
if [[ -z "${start_beat}" || "${start_beat}" == "null" ]]; then
  echo "failed to choose transport start beat" >&2
  exit 1
fi

echo "scheduling transport start at beat ${start_beat}"
pw-cli set-param "${sync_id}" Props \
  "{ params = [ \"beat.params\" \"{\\\"transport_state\\\":[[${start_beat},\\\"start_scheduled\\\"]]}\" ] }"

wait_for_log "recording aligned to transport beat=${start_beat}" "${looper_log}" 8

sleep 1

play_beat="$(scheduled_beat_field_at "${sync_id}" "${play_schedule_index}" 0)"
play_nsec="$(scheduled_beat_field_at "${sync_id}" "${play_schedule_index}" 1)"
if [[ -z "${play_beat}" || "${play_beat}" == "null" ||
      -z "${play_nsec}" || "${play_nsec}" == "null" ]]; then
  echo "failed to choose play beat" >&2
  exit 1
fi

cut_beat=$((play_beat - 3))
if (( cut_beat % 2 == 0 )); then
  cut_beat=$((cut_beat - 1))
fi
if (( cut_beat < start_beat )); then
  echo "cut beat ${cut_beat} is before transport start beat ${start_beat}" >&2
  exit 1
fi

echo "cutting high beat ${cut_beat} and playing loop 0 at beat ${play_beat} on looper capture object.id=${capture_id}"
pw-cli set-param "${capture_id}" Props \
  "{ params = [ \"commands\" \"[[${play_beat},\\\"cut ${cut_beat} ${cut_beat} 0\\\"],[${play_beat},\\\"play 0\\\"]]\" ] }"

wait_for_log "playing loop=0" "${looper_log}" 10
wait_for_log '"type":"transition"' "${record_log}" 10

tolerance_nsec=$((tolerance_frames * 1000000000 / rate))
earliest_transition_nsec=$((play_nsec - tolerance_nsec))

transition_nsec="$(jq -Rr --argjson earliest_nsec "${earliest_transition_nsec}" '
  fromjson? |
  select(.type == "transition" and .high == true and .nsec >= $earliest_nsec) |
  .nsec' "${record_log}" | head -n 1)"

if [[ -z "${transition_nsec}" || "${transition_nsec}" == "null" ]]; then
  echo "validation failed: no high transition found at or after play beat ${play_beat}" >&2
  dump_logs
  exit 1
fi

diff_nsec=$((transition_nsec - play_nsec))
if (( diff_nsec < 0 )); then
  diff_nsec=$((-diff_nsec))
fi

if (( diff_nsec > tolerance_nsec )); then
  echo "validation failed: transition diff ${diff_nsec}ns exceeds tolerance ${tolerance_nsec}ns (${tolerance_frames} frames)" >&2
  echo "play beat ${play_beat} nsec=${play_nsec}, transition nsec=${transition_nsec}" >&2
  dump_logs
  exit 1
fi

echo "validation passed: transition aligned within ${diff_nsec}ns (${tolerance_frames} frame tolerance)"
echo "play beat ${play_beat} nsec=${play_nsec}, transition nsec=${transition_nsec}"
dump_logs
