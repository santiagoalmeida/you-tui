#!/bin/bash
# Install script for you-tui daemon

set -e

echo "Installing you-tui daemon..."

# Check if dotnet is installed
if ! command -v dotnet &> /dev/null; then
    echo "Error: dotnet CLI not found. Please install .NET SDK first."
    echo "Visit: https://dotnet.microsoft.com/download"
    exit 1
fi

# Restore NuGet dependencies
echo "Restoring NuGet dependencies..."
dotnet restore

# Build in release mode
echo "Building in release mode..."
dotnet build -c Release

# Copy binaries
sudo mkdir -p /usr/local/bin
sudo cp YouTui.Daemon/bin/Release/net10.0/you-tui-daemon /usr/local/bin/
sudo cp YouTui.Daemon/bin/Release/net10.0/you-tui-daemon.dll /usr/local/bin/
sudo cp YouTui.Client/bin/Release/net10.0/you-tui /usr/local/bin/
sudo cp YouTui.Client/bin/Release/net10.0/you-tui.dll /usr/local/bin/
sudo chmod +x /usr/local/bin/you-tui-daemon
sudo chmod +x /usr/local/bin/you-tui

# Copy shared libraries
sudo cp YouTui.Shared/bin/Release/net10.0/YouTui.Shared.dll /usr/local/bin/
sudo cp YouTui.Daemon/bin/Release/net10.0/*.deps.json /usr/local/bin/ 2>/dev/null || true
sudo cp YouTui.Daemon/bin/Release/net10.0/*.runtimeconfig.json /usr/local/bin/ 2>/dev/null || true
sudo cp YouTui.Client/bin/Release/net10.0/*.deps.json /usr/local/bin/ 2>/dev/null || true
sudo cp YouTui.Client/bin/Release/net10.0/*.runtimeconfig.json /usr/local/bin/ 2>/dev/null || true

echo "✓ Binaries installed to /usr/local/bin/"

# Install systemd service (user mode)
mkdir -p ~/.config/systemd/user
cp you-tui-daemon.service ~/.config/systemd/user/

echo "✓ Systemd service installed"

# Enable and start service
systemctl --user daemon-reload

echo ""
echo "Installation complete!"
echo ""
echo "To enable auto-start on login:"
echo "  systemctl --user enable you-tui-daemon"
echo "  systemctl --user start you-tui-daemon"
echo ""
echo "To start manually:"
echo "  you-tui-daemon &"
echo ""
echo "To use the client:"
echo "  you-tui"
echo ""
