# you-tui - YouTube Terminal Music Player

A terminal-based music player for YouTube with daemon/client architecture.

## Features

- 🎵 Play music from YouTube in the terminal
- 🔍 Search and add songs/playlists with fzf
- 📋 Persistent queue management
- 🎮 Background daemon keeps music playing
- 🖥️ Multiple TUI clients can connect
- ⏯️ Full playback controls (play, pause, next, previous, jump)

## Architecture

```
┌─────────────────────────────────────┐
│   you-tui-daemon (background)       │
│  - Manages MPV player               │
│  - Maintains playlist queue         │
│  - Listens on Unix socket           │
│  - Auto-plays next track            │
└─────────────────────────────────────┘
              ↕ (Unix Socket)
┌─────────────────────────────────────┐
│   you-tui (TUI client)              │
│  - Interactive menu interface       │
│  - Search and add tracks            │
│  - Control playback                 │
│  - Can exit without stopping music  │
└─────────────────────────────────────┘
```

**Benefits:**
- Music continues playing when you close the UI
- Multiple clients can control the same player
- Persistent state across sessions
- Clean separation of concerns

## Installation

### Arch Linux (AUR)

**Recommended:** Install from AUR using your favorite AUR helper:

```bash
# Using yay
yay -S you-tui

# Using paru
paru -S you-tui

# Manual installation from AUR
git clone https://aur.archlinux.org/you-tui.git
cd you-tui
makepkg -si
```

The package automatically installs all dependencies (mpv, yt-dlp, fzf, socat) and includes the .NET runtime, so you don't need to install .NET separately.

### From Source (Other Distributions)

#### Dependencies

```bash
# Debian/Ubuntu
sudo apt install mpv yt-dlp fzf socat

# Arch Linux (if not using AUR)
sudo pacman -S mpv yt-dlp fzf socat

# Install .NET 10 SDK (required for building)
# Visit: https://dotnet.microsoft.com/download
```

#### Build

```bash
# Build the project
dotnet build

# Or use the install script (copies to /usr/local/bin)
./install.sh
```

## Usage

### Quick Start

```bash
# Start the TUI client (auto-starts daemon if needed)
./YouTui.Client/bin/Debug/net10.0/you-tui

# Or if installed:
you-tui
```

### Command Line Tools

**Check daemon status:**
```bash
./you-tui-status
```

Output:
```
✅ Daemon running (PID: 12345)

🎵 ▶️ Now Playing:
   Toto - Africa (Official HD Video)
   by TOTO (04:32)

📋 Playlist: 11 tracks
   Position: 6/11
   Remaining: 5 tracks
```

**Manual daemon control:**
```bash
# Start daemon manually
./YouTui.Daemon/bin/Debug/net10.0/you-tui-daemon

# Stop daemon
echo '{"command":"Stop"}' | socat - UNIX-CONNECT:/tmp/you-tui-daemon.sock
```

### TUI Controls

In the interactive menu:

- **🔍 Search & Add** - Search YouTube and add tracks to queue
- **📜 View Playlist** - See full playlist and jump to any track
- **🗑️ Clear Playlist** - Remove all tracks from queue
- **⏯️ Pause/Resume** - Toggle playback
- **⏮️ Previous Track** - Go to previous song
- **⏭️ Next Track** - Skip to next song
- **🛑 Quit Daemon** - Stop daemon and music
- **❌ Quit** - Exit client only (music continues)

### Direct Daemon Commands

You can send JSON commands directly to the daemon:

```bash
# Get status
echo '{"command":"GetStatus"}' | socat - UNIX-CONNECT:/tmp/you-tui-daemon.sock

# Play/Pause
echo '{"command":"Play"}' | socat - UNIX-CONNECT:/tmp/you-tui-daemon.sock
echo '{"command":"Pause"}' | socat - UNIX-CONNECT:/tmp/you-tui-daemon.sock

# Next/Previous
echo '{"command":"Next"}' | socat - UNIX-CONNECT:/tmp/you-tui-daemon.sock
echo '{"command":"Previous"}' | socat - UNIX-CONNECT:/tmp/you-tui-daemon.sock

# Jump to track by index
echo '{"command":"JumpTo","data":{"index":3}}' | socat - UNIX-CONNECT:/tmp/you-tui-daemon.sock

# Clear queue
echo '{"command":"ClearQueue"}' | socat - UNIX-CONNECT:/tmp/you-tui-daemon.sock

# Stop daemon
echo '{"command":"Stop"}' | socat - UNIX-CONNECT:/tmp/you-tui-daemon.sock
```

## Configuration

- **Playlist history:** `~/.config/you-tui/history.json`
- **Daemon socket:** `/tmp/you-tui-daemon.sock`
- **MPV socket:** `/tmp/you-tui-mpv.sock`

## Systemd Service (Optional)

To run the daemon as a systemd user service:

```bash
# Install service file
cp you-tui-daemon.service ~/.config/systemd/user/

# Enable and start
systemctl --user enable you-tui-daemon
systemctl --user start you-tui-daemon

# Check status
systemctl --user status you-tui-daemon
```

## Project Structure

```
you-tui/
├── YouTui.Shared/          # Shared models and protocol
│   └── Models/
│       ├── Track.cs
│       ├── DaemonCommand.cs
│       ├── DaemonResponse.cs
│       └── DaemonStatus.cs
├── YouTui.Daemon/          # Background daemon
│   ├── Services/
│   │   ├── DaemonServer.cs      # Unix socket server
│   │   ├── PlaybackEngine.cs    # Auto-advance logic
│   │   ├── CommandHandler.cs    # Command processing
│   │   ├── PlaybackQueue.cs     # Queue management
│   │   └── MpvPlayer.cs         # MPV interface
│   └── Program.cs
├── YouTui.Client/          # TUI client
│   ├── Services/
│   │   ├── DaemonClient.cs      # Socket client
│   │   ├── YouTubeSearcher.cs   # YouTube search
│   │   ├── FzfSelector.cs       # fzf integration
│   │   └── NotificationManager.cs
│   ├── YouTuiApp.cs             # Main TUI
│   └── Program.cs
└── you-tui-status              # Status helper script
```

## Troubleshooting

**Daemon won't start:**
- Check if MPV is installed: `which mpv`
- Check daemon logs: `tail /tmp/you-tui-daemon.log`
- Remove stale socket: `rm /tmp/you-tui-daemon.sock /tmp/you-tui-mpv.sock`

**No sound:**
- Test MPV directly: `mpv --audio-display=no "https://www.youtube.com/watch?v=dQw4w9WgXcQ"`
- Check MPV socket: `ls -l /tmp/you-tui-mpv.sock`

**Client can't connect:**
- Verify daemon is running: `./you-tui-status`
- Check socket exists: `ls -l /tmp/you-tui-daemon.sock`
- Try restarting daemon

## License

This project is licensed under a dual license:

- **Free for personal and open-source use:** You can use, modify, and distribute this software freely.
- **Commercial use:** If you want to commercialize this software or derivatives, you must share at least 30% of gross revenue with the original author.

See LICENSE file for full terms.

## Credits

Built with:
- [.NET 10](https://dotnet.microsoft.com/)
- [Spectre.Console](https://spectreconsole.net/) - Beautiful terminal UIs
- [MPV](https://mpv.io/) - Media player
- [yt-dlp](https://github.com/yt-dlp/yt-dlp) - YouTube downloader
- [fzf](https://github.com/junegunn/fzf) - Fuzzy finder
