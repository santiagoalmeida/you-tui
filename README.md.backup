# you-tui

🎵 **YouTube Music Player for Terminal** - A terminal-based music/video player for YouTube

[![License](https://img.shields.io/badge/License-Custom%20Dual-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/)

## Features

- 🎵 **Play YouTube music/videos** directly from your terminal
- 🔍 **Interactive search** with fuzzy selection (fzf)
- 📋 **Persistent playlist** - never lose your queue
- 🎯 **Jump to any track** - navigate freely through your playlist
- 🔔 **Desktop notifications** - see what's playing
- ⌨️  **Clean UI** - compact and keyboard-driven
- 💾 **Auto-save** - resumes where you left off

## Screenshots

```
─────────────────────────────────────────── you-tui ────────────────────────────────────────────
♪ Now: Best of lofi hip hop 2024 [beats to relax/study to]
  by Lofi Girl (6:10:58)

📋 Queue (15 total, 12 pending):
  1. 1 A.M Study Session 📚 [lofi hip hop] (1:01:14)
  2. Chill Drive - Aesthetic Music ~ Lofi hip hop mix (2:53:52)
  3. lofi hip hop radio – beats to sleep/study/relax to ☕ (LIVE)
  ... and 9 more

Menu:
> 🔍 Search & Add
  📜 View Playlist
  🗑️  Clear Playlist
  ⏯️  Pause/Resume
  ⏭️  Next Track
  ❌ Quit
```

## Dependencies

Make sure you have these installed:

- `mpv` - Media player
- `yt-dlp` - YouTube downloader
- `fzf` - Fuzzy finder
- `notify-send` (libnotify) - Desktop notifications
- `.NET 10.0` SDK

### Installation of dependencies

**Arch Linux:**
```bash
sudo pacman -S mpv yt-dlp fzf libnotify dotnet-sdk
```

**Ubuntu/Debian:**
```bash
sudo apt install mpv yt-dlp fzf libnotify-bin
# Install .NET SDK from: https://dotnet.microsoft.com/download
```

**Fedora:**
```bash
sudo dnf install mpv yt-dlp fzf libnotify dotnet-sdk-10.0
```

## Installation

```bash
git clone https://github.com/santyalmeida/you-tui.git
cd you-tui
dotnet build -c Release
```

## Usage

```bash
dotnet run
```

Or build and run the executable:

```bash
dotnet build -c Release
./bin/Release/net10.0/you-tui
```

### Controls

1. **Search & Add** - Search YouTube and select tracks to add
2. **View Playlist** - See all tracks and jump to any one
3. **Clear Playlist** - Remove all tracks
4. **Pause/Resume** - Toggle playback
5. **Next Track** - Skip to next song
6. **Quit** - Save and exit

### Tips

- Use **TAB** in search results to select multiple tracks
- Press **ESC** to cancel selection
- The playlist persists between sessions
- Desktop notifications show current track

## Project Structure

```
you-tui/
├── Models/
│   └── Track.cs              # Track data model
├── Services/
│   ├── PlaybackQueue.cs      # Playlist management with persistence
│   ├── MpvPlayer.cs          # MPV control via IPC socket
│   ├── YouTubeSearcher.cs    # YouTube search with yt-dlp
│   ├── FzfSelector.cs        # Interactive fuzzy selector
│   └── NotificationManager.cs # Desktop notifications
├── YouTuiApp.cs              # Main application logic
├── Program.cs                # Entry point
└── README.md
```

## Configuration

Playlist history is saved at:
```
~/.config/you-tui/history.json
```

## License

This project uses a **Custom Dual License**:

- ✅ **Free for personal/non-commercial use** - Use it, modify it, share it!
- 💰 **Commercial use requires revenue sharing** - If you make money with it, you must share a minimum of 30% of gross revenue with the author.

See [LICENSE](LICENSE) for full details.

## Contributing

Contributions are welcome! Please note that by contributing, you agree that your contributions will be licensed under the same dual license.

## Author

Created by **Santiago Almeida**

## Acknowledgments

- Built with [Spectre.Console](https://spectreconsole.net/)
- Powered by [mpv](https://mpv.io/), [yt-dlp](https://github.com/yt-dlp/yt-dlp), and [fzf](https://github.com/junegunn/fzf)

---

**Enjoy your music!** 🎶

If you find this useful, consider ⭐ starring the repo!

