#!/usr/bin/env fish

# Stops the baseline video pipeline started by start-video-pipeline.fish,
# using the PID files it wrote to state/. Safe to run even if only some
# (or none) of the four are running.
#
# Usage: ./stop-video-pipeline.fish

set script_dir (status dirname)
set state_dir $script_dir/state

for name in compositor-a compositor-b video-blender downstream
    set pid_file $state_dir/$name.pid
    if not test -f $pid_file
        echo "$name: no pid file, nothing to stop"
        continue
    end

    set pid (cat $pid_file)
    if kill $pid 2>/dev/null
        echo "$name: stopped (pid $pid)"
    else
        echo "$name: pid $pid not running"
    end
    rm -f $pid_file
end
