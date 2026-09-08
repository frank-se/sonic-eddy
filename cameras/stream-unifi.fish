function stream-unifi --description 'Publish the unifi webcam into PipeWire at 1920x1080@30fps RGBA'
    # exec, not a plain call: replaces this function's process with
    # gst-launch-1.0 rather than running it as a child, so a caller that
    # backgrounds `stream-c920 ... &` gets gst-launch-1.0's own PID back
    # (via $last_pid) instead of an intermediate fish process - needed for
    # start-cameras.fish to write a PID file that's actually killable.
    exec gst-launch-1.0 rtspsrc location="rtsps://192.168.0.1:7441/Aco1Fvv3N3ctZ6t4" \
        latency=200 tls-validation-flags=0 protocols=tcp \
        ! rtph265depay ! h265parse ! vah265dec \
        ! videocrop right=400 bottom=360 ! vapostproc ! 'video/x-raw(memory:VAMemory),format=NV12' \
        ! tee name=t \
        t. ! queue ! vapostproc ! video/x-raw,width=1920,height=1080,format=RGBA \
        ! queue leaky=downstream max-size-buffers=1 \
        ! pipewiresink client-name="overview-camera" mode=provide sync=false qos=true \
        t. ! queue ! vapostproc ! video/x-raw,width=1920,height=1080 \
        ! videocrop left=400 right=240 top=160 bottom=0 \
        ! vapostproc ! video/x-raw,width=1280,height=920,format=RGBA \
        ! queue leaky=downstream max-size-buffers=1 \
        ! pipewiresink client-name="overview-camera-medium" mode=provide sync=false qos=true \
        t. ! queue ! vapostproc ! video/x-raw,width=960,height=540 \
        ! waylandsink
end
