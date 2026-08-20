#!/usr/bin/env fish

# Starts both webcam streams at once, at the standardized 1920x1080@30fps
# RGBA format. Sources stream-airhug.fish / stream-c920.fish itself (they're
# fish functions, not scripts) and runs both in the background.
#
# Backgrounding a fish *function* call directly (`stream-airhug ... &`)
# doesn't actually background it - fish runs it synchronously in the
# current process regardless of the `&` (confirmed on fish 4.8.1). Spawning
# a `fish -c` subprocess per camera and backgrounding that instead works,
# since backgrounding an external process is unaffected by that quirk.
#
# Both functions end with `exec gst-launch-1.0 ...`, so the backgrounded
# `fish -c` process becomes gst-launch-1.0 itself (same PID) rather than
# staying an intermediate wrapper - $last_pid below is then the real,
# killable stream PID, written to state/*.pid so stop-cameras.fish can
# find and kill it later without you having to hunt through `ps`.
#
# Device paths (/dev/videoN) are not stable across reboots/reconnects -
# check `v4l2-ctl --list-devices` first and pass the current ones in.
#
# Usage: ./start-cameras.fish /dev/video0 /dev/video2
#          (airhug device, c920 device)
# Stop with: ./stop-cameras.fish

if test (count $argv) -ne 2
    echo "usage: start-cameras.fish <airhug device, e.g. /dev/video0> <c920 device, e.g. /dev/video2>"
    exit 1
end

set script_dir (status dirname)
set state_dir $script_dir/state
mkdir -p $state_dir

fish -c "source $script_dir/stream-airhug.fish; stream-airhug $argv[1]" > $state_dir/airhug.log 2>&1 &
disown
echo $last_pid > $state_dir/airhug.pid

fish -c "source $script_dir/stream-c920.fish; stream-c920 $argv[2]" > $state_dir/c920.log 2>&1 &
disown
echo $last_pid > $state_dir/c920.pid

echo "started airhug (pid "(cat $state_dir/airhug.pid)", log $state_dir/airhug.log)"
echo "started c920 (pid "(cat $state_dir/c920.pid)", log $state_dir/c920.log)"
echo "stop with: $script_dir/stop-cameras.fish"
