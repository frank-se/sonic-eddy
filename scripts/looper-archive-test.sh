#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 1 ]]; then
  echo "usage: $0 <archive-folder>" >&2
  exit 1
fi

archive_folder="$1"
repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
bin_dir="${repo_root}/fr-sonic/build/src"

looper_bin="${bin_dir}/se-looper"
signal_bin="${bin_dir}/se-signal"
record_bin="${bin_dir}/se-record"

looper_name="${LOOPER_NAME:-se.test_looper_archive}"
looper_tag="${LOOPER_TAG:-test-looper-archive}"
duration="${DURATION:-20}"
signal_duration="${SIGNAL_DURATION:-14}"
record_duration="${RECORD_DURATION:-14}"
signal_value="${SIGNAL_VALUE:-0.7}"
start_schedule_index="${START_SCHEDULE_INDEX:-2}"
cut_schedule_index="${CUT_SCHEDULE_INDEX:--1}"

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

mkdir -p "${archive_folder}"

tmp_dir="$(mktemp -d)"
looper_log="${tmp_dir}/looper.log"
signal_log="${tmp_dir}/signal.log"
record_log="${tmp_dir}/record.log"
pids=()

dump_logs() {
  echo
  echo "looper:"
  tail -n 120 "${looper_log}" 2>/dev/null || true
  echo
  echo "signal:"
  tail -n 40 "${signal_log}" 2>/dev/null || true
  echo
  echo "record:"
  tail -n 40 "${record_log}" 2>/dev/null || true
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
  tail -n 120 "${file}" >&2 || true
  exit 1
}

wait_for_archive_result() {
  local file="$1"
  local timeout_seconds="$2"

  for _ in $(seq 1 "${timeout_seconds}"); do
    if grep -q "archived loop=0" "${file}" 2>/dev/null; then
      return 0
    fi
    if grep -q "failed to archive loop=0" "${file}" 2>/dev/null; then
      echo "archive failed" >&2
      tail -n 120 "${file}" >&2 || true
      exit 1
    fi
    sleep 1
  done

  echo "timed out waiting for archive result in ${file}" >&2
  tail -n 120 "${file}" >&2 || true
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

archived_path() {
  local path
  path="$(sed -n "s/.*path='\([^']*\)'.*/\1/p" "${looper_log}" | tail -n 1)"
  if [[ -n "${path}" ]]; then
    echo "${path}"
    return
  fi

  find "${archive_folder}" -maxdepth 1 -type f \
    -name "${looper_name//./_}-loop-0-generation-*.flac" \
    -printf '%T@ %p\n' 2>/dev/null \
    | sort -n \
    | tail -n 1 \
    | cut -d' ' -f2-
}

echo "starting looper: ${looper_name}"
"${looper_bin}" \
  -n "${looper_name}" \
  -t "${looper_tag}" \
  --mix 1 \
  --archive-folder "${archive_folder}" \
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
  -n se.test_record_archive \
  -c "${looper_name}.playback" \
  -d "${record_duration}" \
  --json \
  >"${record_log}" 2>&1 &
pids+=("$!")

sleep 1

echo "starting constant signal into ${looper_name}.capture"
"${signal_bin}" \
  -n se.test_signal_archive \
  -p "${looper_name}.capture" \
  -m constant \
  --value "${signal_value}" \
  -d "${signal_duration}" \
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
archive_cut_beat=$((target_beat - 2))
if (( archive_cut_beat < start_beat )); then
  echo "target beat ${target_beat} is too close to transport start beat ${start_beat}" >&2
  exit 1
fi

echo "cutting beat ${archive_cut_beat} and archiving loop 0 at synced beat ${target_beat} on looper capture object.id=${capture_id}"
pw-cli set-param "${capture_id}" Props \
  "{ params = [ \"commands\" \"[[${target_beat},\\\"cut ${archive_cut_beat} ${archive_cut_beat} 0\\\"],[${target_beat},\\\"archive 0\\\"]]\" ] }"

wait_for_log "queued archive loop=0" "${looper_log}" 10
wait_for_archive_result "${looper_log}" 10

path="$(archived_path)"
if [[ -z "${path}" || ! -s "${path}" ]]; then
  echo "archive validation failed: file not found or empty: ${path}" >&2
  dump_logs
  exit 1
fi

echo "archive created: ${path}"
ls -lh "${path}"

if command -v metaflac >/dev/null 2>&1; then
  echo
  echo "metadata:"
  metaflac --list --block-type=VORBIS_COMMENT "${path}" || true
fi

dump_logs
