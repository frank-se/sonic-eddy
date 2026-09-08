#!/usr/bin/env fish

# Starts the baseline video processing pipeline - the always-on nodes that
# exist regardless of which cameras/sources are plugged into them:
#
#   pw-video-compositor (A) --\
#                               >-- video-blender --> downstream-compositor
#   pw-video-compositor (B) --/
#
# Two pw-video-compositor instances (A/B, one per T-bar M/E switcher panel)
# feed video-blender, which cross-dissolves them; video-blender's output
# feeds downstream-compositor's always-on baseline input for DSK/overlay
# effects. A and B intentionally share the exact same inputs.json/scene set
# (intro-presentation/) - they're the two sides of one T-bar, so both need
# access to the same video source pool and the same set of scene layouts
# (to cut between different arrangements of the same sources), not two
# independently-drifting configs. intro-presentation/downstream/ is
# downstream-compositor's own separate overlay pool (DSK-style, "video in
# can include ANYTHING" - not cameras specifically), addressed from its own
# scene.json, which uses a different object schema (Video/Image, not
# Camera) so it can't be shared with the A/B scenes even though the two
# look similar.
#
# Camera/source routing lives in intro-presentation/inputs.json's
# target_object entries - see that file for the actual device/node names
# expected on the machine this runs on.
#
# Those four wire up to each other automatically via target_object +
# PW_STREAM_FLAG_AUTOCONNECT (consumer side sets the target, per this
# project's convention) - no manual pw-link needed, and start order
# doesn't matter since WirePlumber retries a link on every rescan until
# both ends exist.
#
# Also starts midi-cube (the 3D MIDI note/piano-roll visualizer) standalone
# - not wired into anything above yet, no --midi-target given, so it just
# registers its two nodes (se.midi-cube.midi-in, se.midi-cube.out) and sits
# idle. Output is 400x300 (not the 1920x1080 canvas) - its intended
# on-canvas slot, once assigned, is meant to be used unscaled. Connect its
# MIDI input and route its video output into a compositor slot manually
# (pw-link, or a future --midi-target/target_object entry) once you know
# what should feed it and where it should land.
#
# Usage: ./start-video-pipeline.fish
# Stop with: ./stop-video-pipeline.fish

set script_dir (status dirname)
set repo_root $script_dir/..
set build_dir $repo_root/pw-video-compositor/build
set state_dir $script_dir/state
mkdir -p $state_dir

set canvas_width 1920
set canvas_height 1080

set intro_dir $script_dir/intro-presentation

$build_dir/pw-video-compositor --instance-name A \
    --inputs $intro_dir/inputs.json \
    --scene $intro_dir/01/scene.json --scene $intro_dir/02/scene.json \
    --scene $intro_dir/03/scene.json --scene $intro_dir/04/scene.json \
    >$state_dir/compositor-a.log 2>&1 &
disown
echo $last_pid >$state_dir/compositor-a.pid

$build_dir/pw-video-compositor --instance-name B \
    --inputs $intro_dir/inputs.json \
    --scene $intro_dir/01/scene.json --scene $intro_dir/02/scene.json \
    --scene $intro_dir/03/scene.json --scene $intro_dir/04/scene.json \
    >$state_dir/compositor-b.log 2>&1 &
disown
echo $last_pid >$state_dir/compositor-b.pid

$build_dir/video-blender --width $canvas_width --height $canvas_height \
    --in0-target se.video-compositor.A.out --in1-target se.video-compositor.B.out \
    >$state_dir/video-blender.log 2>&1 &
disown
echo $last_pid >$state_dir/video-blender.pid

$build_dir/downstream-compositor --scene $intro_dir/downstream/scene.json \
    --inputs $intro_dir/downstream/inputs.json --baseline-target se.video-blender.out \
    >$state_dir/downstream.log 2>&1 &
disown
echo $last_pid >$state_dir/downstream.pid

# Temporarily disabled - suspected GPU hang trigger (raylib/OpenGL render
# loop runs continuously at 30fps even while unconnected) when combined
# with the VA-API camera pipelines. See start-cameras.fish.
# $build_dir/midi-cube --video-width 400 --video-height 300 \
#     >$state_dir/midi-cube.log 2>&1 &
# disown
# echo $last_pid >$state_dir/midi-cube.pid

echo "started compositor-a (pid "(cat $state_dir/compositor-a.pid)", log $state_dir/compositor-a.log)"
echo "started compositor-b (pid "(cat $state_dir/compositor-b.pid)", log $state_dir/compositor-b.log)"
echo "started video-blender (pid "(cat $state_dir/video-blender.pid)", log $state_dir/video-blender.log)"
echo "started downstream-compositor (pid "(cat $state_dir/downstream.pid)", log $state_dir/downstream.log)"
echo "midi-cube disabled (suspected GPU hang trigger, see comment above)"
echo "stop with: $script_dir/stop-video-pipeline.fish"
