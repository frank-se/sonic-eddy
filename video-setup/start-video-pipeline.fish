#!/usr/bin/env fish

# Starts the baseline video processing pipeline - the always-on unified
# gpu-compositor process, which replaces the previous 4-process pipeline
# (pw-video-compositor A+B, video-blender, downstream-compositor) with one
# GPU-native (EGL/GLES, no CPU pixel loop) process. See pw-video-compositor/
# src/gpu_compositor_main.cpp's header comment for the full design
# rationale - this replacement exists because the old CPU-bound compositor's
# scalar blend loop was the root cause of repeated PipeWire XRUNs and
# whole-system freezes under real camera load (2026-09-09 investigation).
#
# A and B intentionally share the exact same inputs.json/scene set
# (intro-presentation/) - they're the two sides of one T-bar, so both need
# access to the same video source pool and the same set of scene layouts
# (to cut between different arrangements of the same sources), not two
# independently-drifting configs. intro-presentation/downstream/ is the
# separate DSK-style overlay pool ("video in can include ANYTHING" - not
# cameras specifically) - downstream has no scene-switching (single fixed
# scene.json), only live per-object edits.
#
# Camera/source routing lives in intro-presentation/inputs.json's
# target_object entries - see that file for the actual device/node names
# expected on the machine this runs on.
#
# Live control (scene switching + per-object translation/visibility/color +
# T-bar blend position) all goes through PipeWire Props on three
# control-only nodes, matching the SonicEddy frontend's existing hardcoded
# targets exactly (see Fr.Sonic/Compositor/ - no frontend changes needed):
#   se.video-compositor.A.out / se.video-compositor.B.out - "active_scene_
#     index" (int) + "object_params" (JSON) for that side's currently-active
#     scene; also publish a "scenes" (JSON list) + "active_scene_index"
#     readback.
#   se.video-blender.out - "blend_position" (float 0..1), one-way.
#   se.downstream.out (the real output node) also accepts "object_params"
#     directly - downstream has no scene list to switch, just live edits.
#
# Usage: ./start-video-pipeline.fish --audio-target <name-or-serial>
#            [--preview] [--silence-target <name-or-serial> ...]
#            [--record-target <name-or-serial> --record-out <path.mp4>]
#
# --audio-target: REQUIRED. Real-time tick source that drives every render -
# without it, nothing ever renders a single frame (see run_real_compositor's
# own comment). Must be a genuine Audio/Source (or Duplex) node that's
# actually part of a hardware-driven graph, e.g. one of the loopback
# modules' *playback* side already configured in master-out-split.conf
# (loopback_audio_sharing_source / loopback_obs_source) - NOT a Sink and NOT
# a Sink's monitor port. Found the hard way 2026-09-09: targeting "Master
# Out" (a virtual combine-Sink) directly left the audio-clock stream
# permanently unlinked/suspended, and the whole pipeline silently never
# rendered a single frame despite every other node looking healthy. Pass
# --silence-target "Master Out" alongside this so Master Out (and therefore
# the loopback fan-out downstream of it) never idle-suspends.
#
# --preview: opens gpu-compositor's own native (raylib) Program/Preview
# monitor window, always-on-top. Off by default.
#
# --silence-target: audio node (name or object.serial) to keep alive with a
# continuous silent playback stream (silence_producer), so that node never
# gets suspended for looking idle. Repeatable - pass it once per node you
# want kept alive. Typically just "Master Out" (see --audio-target above).
#
# --record-target / --record-out: gpu-compositor has no recording built in
# (task: switch to VA-API hardware encode, still pending) - av_sync_record
# is still the puller/recorder, targeting se.downstream.out exactly as
# before. Its own --target-object is a SEPARATE audio target from
# --audio-target above (same Sink-vs-Source rule applies to it too).
#
# Neither --silence-target's, --record-target's, nor --audio-target's node
# identifier is resolved/defaulted here: no PipeWire node identifier in this
# graph (name or serial) is stable or precomputable - identity depends on
# the full live system state (whatever's running, whatever's plugged in, at
# the moment this runs), which is WirePlumber's job to resolve dynamically,
# not this script's. The caller must look at the actual running graph and
# supply the current value(s) each time. (se.downstream.out itself is
# different: gpu-compositor assigns that exact literal name every time, so
# it's safe to hardcode as --record-target's video side.)
#
# Stop with: ./stop-video-pipeline.fish

argparse 'preview' 'audio-target=' 'silence-target=+' 'record-target=' 'record-out=' -- $argv
or exit 1

if not set -q _flag_audio_target
    echo "--audio-target is required - see this script's own header comment for why"
    exit 1
end
if set -q _flag_record_target; and not set -q _flag_record_out
    echo "--record-target given without --record-out - both are required together"
    exit 1
end
if set -q _flag_record_out; and not set -q _flag_record_target
    echo "--record-out given without --record-target - both are required together"
    exit 1
end

set script_dir (status dirname)
set repo_root $script_dir/..
set build_dir $repo_root/pw-video-compositor/build
set state_dir $script_dir/state
mkdir -p $state_dir
rm -f $state_dir/silence-*.pid

set canvas_width 1920
set canvas_height 1080
set out_name se.downstream.out

set intro_dir $script_dir/intro-presentation

set -l preview_flag
if set -q _flag_preview
    set preview_flag --preview
end

$build_dir/gpu-compositor --real \
    --inputs-a $intro_dir/inputs.json \
    --scene-a $intro_dir/01/scene.json --scene-a $intro_dir/02/scene.json \
    --scene-a $intro_dir/03/scene.json --scene-a $intro_dir/04/scene.json \
    --inputs-b $intro_dir/inputs.json \
    --scene-b $intro_dir/01/scene.json --scene-b $intro_dir/02/scene.json \
    --scene-b $intro_dir/03/scene.json --scene-b $intro_dir/04/scene.json \
    --downstream-inputs $intro_dir/downstream/inputs.json \
    --downstream-scene $intro_dir/downstream/scene.json \
    --audio-target $_flag_audio_target --out $out_name \
    --width $canvas_width --height $canvas_height $preview_flag \
    >$state_dir/gpu-compositor.log 2>&1 &
disown
echo $last_pid >$state_dir/gpu-compositor.pid

set silence_count 0
for target in $_flag_silence_target
    set silence_count (math $silence_count + 1)
    $build_dir/silence_producer --target-object $target \
        >$state_dir/silence-$silence_count.log 2>&1 &
    disown
    echo $last_pid >$state_dir/silence-$silence_count.pid
end

set recording 0
if set -q _flag_record_target
    $build_dir/av_sync_record --video-target $out_name \
        --target-object $_flag_record_target --out $_flag_record_out \
        --width $canvas_width --height $canvas_height \
        >$state_dir/av_sync_record.log 2>&1 &
    disown
    echo $last_pid >$state_dir/av_sync_record.pid
    set recording 1
end

# Temporarily disabled - suspected GPU hang trigger (raylib/OpenGL render
# loop runs continuously at 30fps even while unconnected) when combined
# with the VA-API camera pipelines. See start-cameras.fish.
# $build_dir/midi-cube --video-width 400 --video-height 300 \
#     >$state_dir/midi-cube.log 2>&1 &
# disown
# echo $last_pid >$state_dir/midi-cube.pid

echo "started gpu-compositor (pid "(cat $state_dir/gpu-compositor.pid)", log $state_dir/gpu-compositor.log)"
echo "midi-cube disabled (suspected GPU hang trigger, see comment above)"
if test $silence_count -gt 0
    echo "started $silence_count silence_producer(s) (logs: $state_dir/silence-*.log)"
else
    echo "no --silence-target given, no silence_producer started"
end
if test $recording -eq 1
    echo "started av_sync_record (pid "(cat $state_dir/av_sync_record.pid)", log $state_dir/av_sync_record.log, out $_flag_record_out)"
else
    echo "no --record-target/--record-out given - not recording, but gpu-compositor is still rendering internally off --audio-target's ticks ($out_name has real frames if something links to it - use --preview to watch, or av_sync_record/dump_consumer to pull it)"
end
echo "stop with: $script_dir/stop-video-pipeline.fish"
