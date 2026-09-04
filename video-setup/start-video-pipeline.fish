#!/usr/bin/env fish

# Starts the baseline video processing pipeline - the four always-on
# nodes that exist regardless of which cameras/sources are plugged into
# them:
#
#   pw-video-compositor (A) --\
#                               >-- video-blender --> downstream-compositor
#   pw-video-compositor (B) --/
#
# Two pw-video-compositor instances (A/B, one per T-bar M/E switcher panel)
# feed video-blender, which cross-dissolves them; video-blender's output
# feeds downstream-compositor's always-on baseline input for DSK/overlay
# effects. Camera/source routing (target_object entries in inputs-a.json /
# inputs-b.json, plus scene-a.json / scene-b.json's scene objects) is a
# separate, later step - today each compositor starts with an empty
# --inputs pool and an empty scene, so this just proves the pipeline
# topology is up before anything is plugged into it.
#
# All four wire up to each other automatically via target_object +
# PW_STREAM_FLAG_AUTOCONNECT (consumer side sets the target, per this
# project's convention) - no manual pw-link needed, and start order
# doesn't matter since WirePlumber retries a link on every rescan until
# both ends exist.
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

$build_dir/pw-video-compositor --instance-name A \
    --inputs $script_dir/inputs-a.json --scene $script_dir/scene-a.json \
    >$state_dir/compositor-a.log 2>&1 &
disown
echo $last_pid >$state_dir/compositor-a.pid

$build_dir/pw-video-compositor --instance-name B \
    --inputs $script_dir/inputs-b.json --scene $script_dir/scene-b.json \
    >$state_dir/compositor-b.log 2>&1 &
disown
echo $last_pid >$state_dir/compositor-b.pid

$build_dir/video-blender --width $canvas_width --height $canvas_height \
    --in0-target se.video-compositor.A.out --in1-target se.video-compositor.B.out \
    >$state_dir/video-blender.log 2>&1 &
disown
echo $last_pid >$state_dir/video-blender.pid

$build_dir/downstream-compositor --scene $script_dir/downstream-scene.json \
    --baseline-target se.video-blender.out \
    >$state_dir/downstream.log 2>&1 &
disown
echo $last_pid >$state_dir/downstream.pid

echo "started compositor-a (pid "(cat $state_dir/compositor-a.pid)", log $state_dir/compositor-a.log)"
echo "started compositor-b (pid "(cat $state_dir/compositor-b.pid)", log $state_dir/compositor-b.log)"
echo "started video-blender (pid "(cat $state_dir/video-blender.pid)", log $state_dir/video-blender.log)"
echo "started downstream-compositor (pid "(cat $state_dir/downstream.pid)", log $state_dir/downstream.log)"
echo "stop with: $script_dir/stop-video-pipeline.fish"
