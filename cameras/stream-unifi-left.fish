function stream-unifi-left --description 'Publish the unifi webcam into PipeWire at 1920x1080@30fps RGBA'
    # exec, not a plain call: replaces this function's process with
    # gst-launch-1.0 rather than running it as a child, so a caller that
    # backgrounds `stream-c920 ... &` gets gst-launch-1.0's own PID back
    # (via $last_pid) instead of an intermediate fish process - needed for
    # start-cameras.fish to write a PID file that's actually killable.
    exec gst-launch-1.0 rtspsrc location="rtsps://192.168.0.1:7441/QyXqjn6v6DvsmaFZ" \
        latency=200 tls-validation-flags=0 protocols=tcp \
        ! rtph265depay ! h265parse ! vah265dec \
        ! vapostproc ! 'video/x-raw(memory:VAMemory),format=NV12' \
        ! tee name=t \
        t. ! queue ! vapostproc ! video/x-raw,width=960,height=540 \
        ! waylandsink \
        t. ! queue ! videocrop left=1350 right=1050 top=1000 bottom=80 \
        ! vapostproc ! video/x-raw,width=400,height=300,format=RGBA \
        ! queue leaky=downstream max-size-buffers=1 \
        ! pipewiresink client-name="cozy-camera" mode=provide sync=false qos=true \
        t. ! queue ! videocrop left=1650 right=750 top=80 bottom=1000 \
        ! vapostproc ! video/x-raw,width=400,height=300,format=RGBA \
        ! queue leaky=downstream max-size-buffers=1 \
        ! pipewiresink client-name="detail-camera-left-small" mode=provide sync=false qos=true
end
