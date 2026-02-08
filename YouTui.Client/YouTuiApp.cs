using System.Text;
using Spectre.Console;
using YouTui.Shared.Models;
using YouTui.Client.Services;
using SpectreColor = Spectre.Console.Color;

namespace YouTui.Client;

public class YouTuiApp
{
    private readonly DaemonClient _daemonClient;
    private readonly YouTubeSearcher _searcher;
    private readonly FzfSelector _selector;
    private readonly NotificationManager _notifications;
    private bool _isRunning;
    private DaemonStatus? _lastStatus;
    private int _scrollOffset = 0;
    private DateTime _lastScrollUpdate = DateTime.Now;
    private const int TRACK_NAME_WIDTH = 25; // Width for scrolling track name (adjusted for 64-char panel)
    private const double SCROLL_SPEED = 0.3; // seconds per character
    private CancellationTokenSource? _updateCancellation;
    private Task? _updateTask;

    public YouTuiApp()
    {
        _daemonClient = new DaemonClient();
        _searcher = new YouTubeSearcher();
        _selector = new FzfSelector();
        _notifications = new NotificationManager();
    }

    public async Task RunAsync()
    {
        try
        {
            await InitializeAsync();
            await MainLoopAsync();
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
        }
        finally
        {
            await CleanupAsync();
        }
    }

    private async Task InitializeAsync()
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(new FigletText("you-tui").Centered().Color(SpectreColor.Cyan1));
        AnsiConsole.MarkupLine("[grey]YouTube Music Player[/]".PadLeft(50));
        AnsiConsole.WriteLine();

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Initializing...", async ctx =>
            {
                ctx.Status("Checking dependencies...");
                await CheckDependenciesAsync();

                ctx.Status("Connecting to daemon...");
                var isRunning = await _daemonClient.IsDaemonRunningAsync();
                
                if (!isRunning)
                {
                    AnsiConsole.MarkupLine("[yellow]Daemon not running, trying to start it...[/]");
                    ctx.Status("Starting daemon...");
                    await StartDaemonAsync();
                    await Task.Delay(1000);
                }
                else
                {
                    AnsiConsole.MarkupLine("[green]✓ Connected to running daemon[/]");
                }

                _lastStatus = await _daemonClient.GetStatusAsync();
                _isRunning = true;
            });

        if (_lastStatus != null && _lastStatus.QueueLength > 0)
        {
            AnsiConsole.MarkupLine($"[green]✓[/] Loaded {_lastStatus.QueueLength} tracks");
            if (_lastStatus.CurrentTrack != null && _lastStatus.IsPlaying)
            {
                await _notifications.ShowNowPlayingAsync(_lastStatus.CurrentTrack);
            }
            await Task.Delay(1500);
        }
        else
        {
            AnsiConsole.MarkupLine("[yellow]📋 Playlist is empty[/]");
            await Task.Delay(1500);
        }
    }

    private async Task StartDaemonAsync()
    {
        // Try to find daemon executable
        string? daemonPath = null;
        
        // Check if installed in system
        if (File.Exists("/usr/local/bin/you-tui-daemon"))
        {
            daemonPath = "/usr/local/bin/you-tui-daemon";
        }
        else
        {
            // Development mode - search upward from current directory
            var currentDir = Directory.GetCurrentDirectory();
            var searchDir = currentDir;
            
            for (int i = 0; i < 10; i++)
            {
                var candidate = Path.Combine(searchDir, "YouTui.Daemon", "bin", "Debug", "net10.0", "you-tui-daemon");
                if (File.Exists(candidate))
                {
                    daemonPath = candidate;
                    break;
                }
                
                var parent = Directory.GetParent(searchDir);
                if (parent == null) break;
                searchDir = parent.FullName;
            }
            
            if (daemonPath == null)
            {
                throw new Exception($"Daemon executable not found. Current dir: {currentDir}. Run 'dotnet build' or install with './install.sh'");
            }
        }

        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = daemonPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        try
        {
            System.Diagnostics.Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to start daemon from '{daemonPath}': {ex.Message}");
        }
        
        // Wait for daemon to be ready
        for (int i = 0; i < 10; i++)
        {
            await Task.Delay(500);
            if (await _daemonClient.IsDaemonRunningAsync())
                return;
        }
        
        throw new Exception("Daemon started but didn't respond in time. Check if MPV is installed.");
    }

    private async Task CheckDependenciesAsync()
    {
        var deps = new[] { "yt-dlp", "fzf" };
        foreach (var dep in deps)
        {
            var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "which",
                Arguments = dep,
                RedirectStandardOutput = true,
                UseShellExecute = false
            });

            if (process != null)
            {
                await process.WaitForExitAsync();
                if (process.ExitCode != 0)
                {
                    throw new Exception($"Required dependency '{dep}' not found. Please install it.");
                }
            }
        }
    }

    private async Task MainLoopAsync()
    {
        // Start background update task
        _updateCancellation = new CancellationTokenSource();
        _updateTask = Task.Run(async () => await UpdateLoopAsync(_updateCancellation.Token));
        
        while (_isRunning)
        {
            // Update status once before showing menu
            _lastStatus = await _daemonClient.GetStatusAsync();
            
            AnsiConsole.Clear();
            ShowCompactStatus();
            
            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[cyan]Menu:[/]")
                    .PageSize(10)
                    .AddChoices(new[]
                    {
                        "🔍 Search & Add",
                        "📜 View Playlist",
                        "📺 Live Player View",
                        "🗑️  Clear Playlist",
                        "⏯️  Pause/Resume",
                        "⏮️  Previous Track",
                        "⏭️  Next Track",
                        "🛑 Quit Daemon",
                        "❌ Quit"
                    })
            );

            switch (choice)
            {
                case "🔍 Search & Add":
                    await SearchAndAddAsync();
                    break;
                case "📜 View Playlist":
                    await ViewFullPlaylistAsync();
                    break;
                case "📺 Live Player View":
                    await LivePlayerViewAsync();
                    break;
                case "🗑️  Clear Playlist":
                    await ClearQueueAsync();
                    await Task.Delay(1000);
                    break;
                case "⏯️  Pause/Resume":
                    if (_lastStatus?.IsPlaying == true)
                        await _daemonClient.PauseAsync();
                    else
                        await _daemonClient.PlayAsync();
                    AnsiConsole.MarkupLine("[yellow]⏯️  Toggled pause[/]");
                    await Task.Delay(800);
                    break;
                case "⏮️  Previous Track":
                    await _daemonClient.PreviousAsync();
                    AnsiConsole.MarkupLine("[green]⏮️  Previous track[/]");
                    await Task.Delay(1000);
                    break;
                case "⏭️  Next Track":
                    await _daemonClient.NextAsync();
                    AnsiConsole.MarkupLine("[green]⏭️  Skipped to next track[/]");
                    await Task.Delay(1000);
                    break;
                case "🛑 Quit Daemon":
                    await _daemonClient.StopDaemonAsync();
                    AnsiConsole.MarkupLine("[yellow]🛑 Daemon stopped[/]");
                    await Task.Delay(1000);
                    _isRunning = false;
                    break;
                case "❌ Quit":
                    _isRunning = false;
                    break;
            }
        }
        
        // Stop update loop
        _updateCancellation?.Cancel();
        if (_updateTask != null)
            await _updateTask;
    }

    private async Task UpdateLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Update scroll offset
                var now = DateTime.Now;
                var elapsed = (now - _lastScrollUpdate).TotalSeconds;
                if (elapsed >= SCROLL_SPEED)
                {
                    _scrollOffset++;
                    _lastScrollUpdate = now;
                }
                
                await Task.Delay(100, cancellationToken); // Update 10 times per second
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    private void ShowCompactStatus()
    {
        var rule = new Rule("[cyan]you-tui[/]")
        {
            Style = Style.Parse("cyan dim")
        };
        AnsiConsole.Write(rule);
        AnsiConsole.WriteLine();

        if (_lastStatus?.CurrentTrack != null)
        {
            // Get previous, current, and next tracks
            Track? previousTrack = null;
            Track? nextTrack = null;
            
            if (_lastStatus.Queue != null)
            {
                if (_lastStatus.CurrentIndex > 0)
                    previousTrack = _lastStatus.Queue[_lastStatus.CurrentIndex - 1];
                
                if (_lastStatus.CurrentIndex < _lastStatus.Queue.Count - 1)
                    nextTrack = _lastStatus.Queue[_lastStatus.CurrentIndex + 1];
            }

            // Show previous track (dimmed/opaque)
            if (previousTrack != null)
            {
                var prevTitle = TruncateText(previousTrack.Title.EscapeMarkup(), 50);
                AnsiConsole.MarkupLine($"[grey dim]  ↑ {prevTitle}[/]");
            }
            else
            {
                AnsiConsole.WriteLine();
            }

            // Current track box with banner rotator effect (single line)
            var boxContent = CreateSingleLineBanner(_lastStatus.CurrentTrack, _lastStatus.IsPlaying, _lastStatus.TimePosition, _lastStatus.Duration);
            AnsiConsole.Write(boxContent);

            // Show next track (dimmed/opaque)
            if (nextTrack != null)
            {
                var nextTitle = TruncateText(nextTrack.Title.EscapeMarkup(), 50);
                AnsiConsole.MarkupLine($"[grey dim]  ↓ {nextTitle}[/]");
            }
            else
            {
                AnsiConsole.WriteLine();
            }
        }
        else
        {
            var emptyPanel = new Panel("[grey]♪ Nothing playing[/]")
            {
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(SpectreColor.Grey),
                Padding = new Padding(1, 0),
                Expand = false  // Auto-fit to content width
            };
            AnsiConsole.Write(emptyPanel);
        }

        // Show queue summary
        if (_lastStatus?.PendingCount > 0)
        {
            AnsiConsole.MarkupLine($"\n[cyan]📋 Queue: {_lastStatus.PendingCount} pending, {_lastStatus.QueueLength} total[/]");
        }
        else if ((_lastStatus?.QueueLength ?? 0) > 0)
        {
            AnsiConsole.MarkupLine($"\n[cyan]📋 Playlist: {_lastStatus.QueueLength} tracks[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("\n[grey]📋 Empty playlist - search to add tracks[/]");
        }
        
        AnsiConsole.WriteLine();
    }

    private Panel CreateSingleLineBanner(Track track, bool isPlaying, double position, double duration)
    {
        // Scroll offset is now updated in background task
        var title = track.Title;
        
        // Create scrolling text if title is longer than track name width
        var displayTitle = CreateScrollingText(title, TRACK_NAME_WIDTH);
        
        // Format time display
        var positionStr = FormatTime(position);
        var durationStr = track.Duration;
        
        // Status icon
        var statusIcon = isPlaying ? "♪" : "⏸";
        
        // Build single line: "NOW PLAYING - [scrolling track name] - 1:33/4:38"
        var singleLine = $"[cyan]{statusIcon} NOW PLAYING[/] [yellow]-[/] [bold]{displayTitle.EscapeMarkup()}[/] [yellow]-[/] [blue]{positionStr}/{durationStr}[/]";
        
        var panel = new Panel(singleLine)
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(isPlaying ? SpectreColor.Green : SpectreColor.Yellow),
            Padding = new Padding(1, 0),
            Expand = false  // Auto-fit to content width
        };
        
        return panel;
    }

    private string CreateScrollingText(string text, int width)
    {
        if (text.Length <= width)
        {
            _scrollOffset = 0;
            return text;
        }
        
        // Add spacing for loop effect
        var loopText = text + "  ★  ";
        var offset = _scrollOffset % loopText.Length;
        
        // Create infinite loop by doubling the text
        var doubledText = loopText + loopText;
        
        // Extract visible portion
        return doubledText.Substring(offset, Math.Min(width, doubledText.Length - offset));
    }

    private string TruncateText(string text, int maxLength)
    {
        if (text.Length <= maxLength)
            return text;
        return text.Substring(0, maxLength - 3) + "...";
    }

    private string FormatTime(double seconds)
    {
        var timeSpan = TimeSpan.FromSeconds(seconds);
        if (timeSpan.TotalHours >= 1)
            return timeSpan.ToString(@"h\:mm\:ss");
        return timeSpan.ToString(@"m\:ss");
    }

    private async Task SearchAndAddAsync()
    {
        var query = AnsiConsole.Ask<string>("[cyan]🔍 Search YouTube:[/]");
        if (string.IsNullOrWhiteSpace(query)) return;

        List<Track> results = new();
        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync($"Searching for '{query}'...", async ctx =>
            {
                results = await _searcher.SearchAsync(query, 20);
            });

        if (results.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No results found[/]");
            await Task.Delay(1500);
            return;
        }

        // Direct to fzf selector without showing table
        AnsiConsole.MarkupLine($"[green]✓ Found {results.Count} results[/]");
        AnsiConsole.MarkupLine("[grey]Opening selector... (TAB to select multiple, Enter to add)[/]");
        await Task.Delay(500);

        var selected = await _selector.SelectMultipleAsync(results);

        if (selected.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No tracks selected[/]");
            await Task.Delay(1000);
            return;
        }

        await _daemonClient.AddTracksAsync(selected);
        AnsiConsole.MarkupLine($"[green]✓ Added {selected.Count} track(s)[/]");
        
        if (selected.Count > 0)
        {
            await _notifications.ShowNowPlayingAsync(selected[0]);
        }
        
        await Task.Delay(1500);
    }

    private async Task ViewFullPlaylistAsync()
    {
        var logPath = "/tmp/you-tui-view-playlist.log";
        try
        {
            await File.WriteAllTextAsync(logPath, $"[{DateTime.Now:HH:mm:ss}] ViewFullPlaylistAsync started\n");
            
            var status = await _daemonClient.GetStatusAsync();
            await File.AppendAllTextAsync(logPath, $"  Status received: null? {status == null}, QueueLength: {status?.QueueLength ?? -1}\n");
            
            if (status == null || status.QueueLength == 0)
            {
                await File.AppendAllTextAsync(logPath, "  Showing 'Playlist is empty' message\n");
                AnsiConsole.MarkupLine("[yellow]Playlist is empty[/]");
                await Task.Delay(1500);
                return;
            }

            await File.AppendAllTextAsync(logPath, $"  Queue count: {status.Queue?.Count ?? -1}\n");
            
            var choices = new List<string>();
            choices.Add("[grey]← Back to menu[/]");
            
            for (int i = 0; i < status.Queue.Count; i++)
            {
                var track = status.Queue[i];
                var prefix = i == status.CurrentIndex ? "▶ " : "  ";
                var title = track.Title.EscapeMarkup();
                var uploader = track.Uploader.EscapeMarkup();
                // Use double brackets [[ ]] to escape them in Spectre.Console markup
                choices.Add($"{prefix}[[{i}]] {title} - {uploader} ({track.Duration})");
            }

            await File.AppendAllTextAsync(logPath, $"  Created {choices.Count} choices\n");
            await File.AppendAllTextAsync(logPath, "  Showing SelectionPrompt...\n");

            var selected = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title($"[cyan]Full Playlist ({status.QueueLength} tracks):[/]")
                    .PageSize(15)
                    .AddChoices(choices)
            );

            await File.AppendAllTextAsync(logPath, $"  Selected: {selected}\n");

            if (selected.Contains("Back to menu"))
            {
                await File.AppendAllTextAsync(logPath, "  User selected Back to menu\n");
                return;
            }

            var match = System.Text.RegularExpressions.Regex.Match(selected, @"\[(\d+)\]");
            if (match.Success)
            {
                var index = int.Parse(match.Groups[1].Value);
                await File.AppendAllTextAsync(logPath, $"  Jumping to index {index}\n");
                await _daemonClient.JumpToAsync(index);
                AnsiConsole.MarkupLine($"[green]✓ Jumped to track {index}[/]");
                await Task.Delay(1000);
            }
        }
        catch (Exception ex)
        {
            await File.AppendAllTextAsync(logPath, $"  EXCEPTION: {ex.GetType().Name}\n");
            await File.AppendAllTextAsync(logPath, $"  Message: {ex.Message}\n");
            await File.AppendAllTextAsync(logPath, $"  Stack: {ex.StackTrace}\n");
            
            AnsiConsole.MarkupLine($"[red]Error viewing playlist: {ex.Message}[/]");
            AnsiConsole.MarkupLine($"[grey]{ex.StackTrace}[/]");
            await Task.Delay(3000);
        }
    }

    private async Task LivePlayerViewAsync()
    {
        AnsiConsole.Clear();
        AnsiConsole.MarkupLine("[cyan]Live Player View[/] - Press any key to return to menu\n");
        
        var startTime = DateTime.Now;
        
        while (true)
        {
            // Check if key is pressed (non-blocking)
            if (Console.KeyAvailable)
            {
                Console.ReadKey(true);
                break;
            }
            
            // Update status
            _lastStatus = await _daemonClient.GetStatusAsync();
            
            // Clear and redraw
            Console.SetCursorPosition(0, 2);
            
            if (_lastStatus?.CurrentTrack != null)
            {
                // Get tracks
                Track? previousTrack = null;
                Track? nextTrack = null;
                
                if (_lastStatus.Queue != null)
                {
                    if (_lastStatus.CurrentIndex > 0)
                        previousTrack = _lastStatus.Queue[_lastStatus.CurrentIndex - 1];
                    
                    if (_lastStatus.CurrentIndex < _lastStatus.Queue.Count - 1)
                        nextTrack = _lastStatus.Queue[_lastStatus.CurrentIndex + 1];
                }

                // Previous track
                if (previousTrack != null)
                {
                    var prevTitle = TruncateText(previousTrack.Title, 50);
                    AnsiConsole.MarkupLine($"[grey dim]  ↑ {prevTitle.EscapeMarkup()}[/]");
                }
                else
                {
                    AnsiConsole.WriteLine(new string(' ', 60));
                }

                // Current track with live scroll
                var title = _lastStatus.CurrentTrack.Title;
                var displayTitle = CreateScrollingText(title, TRACK_NAME_WIDTH);
                var positionStr = FormatTime(_lastStatus.TimePosition);
                var durationStr = _lastStatus.CurrentTrack.Duration;
                var statusIcon = _lastStatus.IsPlaying ? "♪" : "⏸";
                
                var singleLine = $"[cyan]{statusIcon} NOW PLAYING[/] [yellow]-[/] [bold]{displayTitle.EscapeMarkup()}[/] [yellow]-[/] [blue]{positionStr}/{durationStr}[/]";
                
                var panel = new Panel(singleLine)
                {
                    Border = BoxBorder.Rounded,
                    BorderStyle = new Style(_lastStatus.IsPlaying ? SpectreColor.Green : SpectreColor.Yellow),
                    Padding = new Padding(1, 0),
                    Width = 64  // Fixed width (20% less than 80)
                };
                
                AnsiConsole.Write(panel);

                // Next track
                if (nextTrack != null)
                {
                    var nextTitle = TruncateText(nextTrack.Title, 50);
                    AnsiConsole.MarkupLine($"[grey dim]  ↓ {nextTitle.EscapeMarkup()}[/]");
                }
                else
                {
                    AnsiConsole.WriteLine(new string(' ', 60));
                }
                
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine($"[cyan]📋 Queue: {_lastStatus.PendingCount} pending, {_lastStatus.QueueLength} total[/]");
            }
            else
            {
                AnsiConsole.MarkupLine("[grey]♪ Nothing playing[/]");
            }
            
            // Clear rest of screen
            for (int i = 0; i < 5; i++)
                AnsiConsole.WriteLine(new string(' ', 80));
            
            await Task.Delay(500); // Update twice per second
        }
    }

    private async Task ClearQueueAsync()
    {
        var confirm = AnsiConsole.Confirm("[yellow]Clear entire playlist?[/]");
        if (confirm)
        {
            await _daemonClient.ClearQueueAsync();
            AnsiConsole.MarkupLine("[green]✓ Playlist cleared[/]");
        }
    }

    private async Task CleanupAsync()
    {
        _updateCancellation?.Cancel();
        if (_updateTask != null)
            await _updateTask;
        
        AnsiConsole.Clear();
        AnsiConsole.MarkupLine("[cyan]Goodbye![/]");
        _daemonClient.Dispose();
    }
}
