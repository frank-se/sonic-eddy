#!/usr/bin/env fish

# Stops webcam streams started by start-cameras.fish, using the PID files
# it wrote to state/. Safe to run even if only one (or neither) is running.
#
# Usage: ./stop-cameras.fish

set script_dir (status dirname)
set state_dir $script_dir/state

for name in airhug c920 unifi
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
