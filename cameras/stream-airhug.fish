# Publishes the AIRHUG 02 webcam into PipeWire as a fixed 1920x1080 @
# 30fps RGBA video source, named "Webcam-RGBA-Stream" (matches the source
# name already assigned to a camera slot in Sonic Eddy's Streaming ->
# Cameras window - don't rename without updating that assignment too).
#
# Standardized on 1920x1080/MJPG/30fps because both webcams on this machine
# support it natively at full frame rate (their YUYV modes only manage
# 5fps at that resolution) - see /home/frank/Development/sonic-eddy/cameras
# for the matching C920 script, which targets the same size so
# pw-video-compositor's fixed-size inputs don't need per-camera scaling.
#
# Device paths (/dev/videoN) are not stable across reboots/reconnects -
# check `v4l2-ctl --list-devices` first and pass the current one in.
#
# Usage: stream-airhug /dev/video0

function stream-airhug --description 'Publish the AIRHUG 02 webcam into PipeWire at 1920x1080@30fps RGBA'
    if test (count $argv) -ne 1
        echo "usage: stream-airhug <device, e.g. /dev/video0>"
        return 1
    end

    set -l device $argv[1]

    # exec, not a plain call: replaces this function's process with
    # gst-launch-1.0 rather than running it as a child, so a caller that
    # backgrounds `stream-airhug ... &` gets gst-launch-1.0's own PID back
    # (via $last_pid) instead of an intermediate fish process - needed for
    # start-cameras.fish to write a PID file that's actually killable.
    exec gst-launch-1.0 v4l2src device=$device \
        ! image/jpeg,width=1920,height=1080,framerate=30/1 \
        ! jpegdec \
        ! videoconvert \
        ! video/x-raw,format=RGBA \
        ! pipewiresink client-name="Webcam-RGBA-Stream" mode=provide
end
