#!/bin/bash
cd /home/santyalmeida/workspace/you-tui

# Check if daemon is running
if ! pgrep -f you-tui-daemon > /dev/null; then
    echo "Starting daemon..."
    ./YouTui.Daemon/bin/Debug/net10.0/you-tui-daemon &
    sleep 3
fi

# Run client and capture output
echo "Testing View Playlist..."
echo "Press Down Arrow once, then Enter to select View Playlist"
./YouTui.Client/bin/Debug/net10.0/you-tui 2>&1
