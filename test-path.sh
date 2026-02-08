#!/bin/bash
BASE="/home/santyalmeida/workspace/you-tui/YouTui.Client/bin/Debug/net10.0"
cd "$BASE"
DAEMON="$BASE/../../../../../YouTui.Daemon/bin/Debug/net10.0/you-tui-daemon"
NORMALIZED=$(realpath "$DAEMON")
echo "Daemon path: $NORMALIZED"
ls -la "$NORMALIZED"
