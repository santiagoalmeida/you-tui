using System.Text;
using Spectre.Console;
using YouTui.Models;
using YouTui.Services;
using SpectreColor = Spectre.Console.Color;

namespace YouTui;

public class YouTuiApp
{
    private readonly PlaybackQueue _queue;
    private readonly MpvPlayer _player;
    private readonly YouTubeSearcher _searcher;
    private readonly FzfSelector _selector;
    private readonly NotificationManager _notifications;
    private bool _isRunning;

    public YouTuiApp()
    {
        _queue = new PlaybackQueue();
        _player = new MpvPlayer();
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

                ctx.Status("Loading playlist...");
                await _queue.LoadHistoryAsync();

                ctx.Status("Starting MPV player...");
                await _player.InitializeAsync();

                _isRunning = true;
            });

        if (_queue.Count > 0 || _queue.TotalCount > 0)
        {
            AnsiConsole.MarkupLine($"[green]✓[/] Loaded {_queue.TotalCount} tracks");
            if (_queue.Count > 0 && _queue.CurrentTrack != null)
            {
                // Resume from where we left off
                await _player.PlayAsync(_queue.CurrentTrack);
                await _notifications.ShowNowPlayingAsync(_queue.CurrentTrack);
            }
            await Task.Delay(1500);
        }
        else
        {
            AnsiConsole.MarkupLine("[yellow]📋 Playlist is empty[/]");
            await Task.Delay(1500);
        }
    }

    private async Task CheckDependenciesAsync()
    {
        var deps = new[] { "mpv", "yt-dlp", "fzf" };
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
        while (_isRunning)
        {
            AnsiConsole.Clear();
            ShowCompactStatus();
            
            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[cyan]Menu:[/]")
                    .PageSize(8)
                    .AddChoices(new[]
                    {
                        "🔍 Search & Add",
                        "📜 View Playlist",
                        "🗑️  Clear Playlist",
                        "⏯️  Pause/Resume",
                        "⏭️  Next Track",
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
                case "🗑️  Clear Playlist":
                    await ClearQueue();
                    await Task.Delay(1000);
                    break;
                case "⏯️  Pause/Resume":
                    await _player.PauseAsync();
                    AnsiConsole.MarkupLine("[yellow]⏯️  Toggled pause[/]");
                    await Task.Delay(800);
                    break;
                case "⏭️  Next Track":
                    await PlayNextAsync();
                    await Task.Delay(1000);
                    break;
                case "❌ Quit":
                    _isRunning = false;
                    break;
            }
        }
    }

    private void ShowCompactStatus()
    {
        // Header
        var rule = new Rule("[cyan]you-tui[/]")
        {
            Style = Style.Parse("cyan dim")
        };
        AnsiConsole.Write(rule);

        // Now Playing - compact
        if (_queue.CurrentTrack != null)
        {
            var title = _queue.CurrentTrack.Title.EscapeMarkup();
            var uploader = _queue.CurrentTrack.Uploader.EscapeMarkup();
            AnsiConsole.MarkupLine($"[green]♪ Now:[/] [bold]{title}[/]");
            AnsiConsole.MarkupLine($"[grey]  by {uploader}[/] [blue]({_queue.CurrentTrack.Duration})[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[grey]♪ Nothing playing[/]");
        }

        // Queue preview - show next 3 tracks
        if (_queue.Count > 0)
        {
            AnsiConsole.MarkupLine($"\n[cyan]📋 Queue ({_queue.TotalCount} total, {_queue.Count} pending):[/]");
            var nextTracks = _queue.GetAll().Take(3).ToList();
            for (int i = 0; i < nextTracks.Count; i++)
            {
                var track = nextTracks[i];
                var trackTitle = track.Title.EscapeMarkup();
                AnsiConsole.MarkupLine($"[grey]  {i + 1}. {trackTitle} ({track.Duration})[/]");
            }
            if (_queue.Count > 3)
            {
                AnsiConsole.MarkupLine($"[grey]  ... and {_queue.Count - 3} more[/]");
            }
        }
        else
        {
            AnsiConsole.MarkupLine($"\n[cyan]📋 Playlist ({_queue.TotalCount} tracks)[/]");
            if (_queue.TotalCount > 0)
            {
                AnsiConsole.MarkupLine("[grey]  All tracks played![/]");
            }
            else
            {
                AnsiConsole.MarkupLine("[yellow]  Empty - add some music![/]");
            }
        }
        
        AnsiConsole.WriteLine();
    }

    private async Task SearchAndAddAsync()
    {
        var query = AnsiConsole.Ask<string>("[cyan]Search query (or press Enter to cancel):[/]");
        
        if (string.IsNullOrWhiteSpace(query))
        {
            AnsiConsole.MarkupLine("[yellow]Search cancelled[/]");
            await Task.Delay(800);
            return;
        }

        List<Track> results = new();
        await AnsiConsole.Status()
            .StartAsync("Searching YouTube...", async ctx =>
            {
                results = await _searcher.SearchAsync(query);
            });

        if (results.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No results found[/]");
            await Task.Delay(1200);
            return;
        }

        // Display results
        AnsiConsole.Clear();
        AnsiConsole.MarkupLine($"[cyan bold]🔍 Search Results ({results.Count} found)[/]\n");

        DisplayResults(results);

        AnsiConsole.MarkupLine("\n[grey]Tip: Use TAB to select multiple, ESC to cancel[/]");
        AnsiConsole.MarkupLine("[grey]Press any key to open selector...[/]");
        Console.ReadKey(true);

        var selectedTracks = await _selector.SelectMultipleAsync(results, "Select tracks to add");

        if (selectedTracks.Count > 0)
        {
            bool isFirstTrack = _queue.TotalCount == 0;
            
            foreach (var track in selectedTracks)
            {
                _queue.Enqueue(track);
                
                // Add to mpv playlist
                if (isFirstTrack)
                {
                    await _player.PlayAsync(track);
                    await _notifications.ShowNowPlayingAsync(track);
                    isFirstTrack = false;
                }
                else
                {
                    await _player.AddToPlaylistAsync(track);
                }
            }

            await _queue.SaveHistoryAsync();
            AnsiConsole.MarkupLine($"[green]✓[/] Added {selectedTracks.Count} track(s)");
            await Task.Delay(1000);
        }
        else
        {
            AnsiConsole.MarkupLine("[yellow]No tracks selected[/]");
            await Task.Delay(800);
        }
    }

    private void DisplayResults(List<Track> results)
    {
        var displayCount = Math.Min(results.Count, 12);
        
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(SpectreColor.Grey)
            .AddColumn(new TableColumn("#").Width(4))
            .AddColumn(new TableColumn("Title"))
            .AddColumn(new TableColumn("Uploader"))
            .AddColumn(new TableColumn("Duration").RightAligned());
        
        for (int i = 0; i < displayCount; i++)
        {
            var track = results[i];
            var title = track.Title.Length > 60 ? track.Title.Substring(0, 57) + "..." : track.Title;
            var uploader = track.Uploader.Length > 30 ? track.Uploader.Substring(0, 27) + "..." : track.Uploader;
            
            table.AddRow(
                $"[grey]{i + 1}.[/]",
                title.EscapeMarkup(),
                $"[grey]{uploader.EscapeMarkup()}[/]",
                $"[blue]{track.Duration}[/]"
            );
        }

        if (results.Count > displayCount)
        {
            table.AddEmptyRow();
            table.AddRow(
                "[grey]...[/]",
                $"[grey]and {results.Count - displayCount} more in selector[/]",
                "",
                ""
            );
        }
        
        AnsiConsole.Write(table);
    }

    private async Task ViewFullPlaylistAsync()
    {
        var tracks = _queue.GetAllTracks().ToList();
        if (tracks.Count == 0)
        {
            AnsiConsole.MarkupLine("\n[yellow]📜 Playlist is empty[/]");
            await Task.Delay(1000);
            return;
        }

        AnsiConsole.Clear();
        AnsiConsole.MarkupLine($"[cyan bold]📜 Playlist ({tracks.Count} tracks)[/]\n");

        var currentTrack = _queue.CurrentTrack;
        var choices = new List<string>();
        
        for (int i = 0; i < tracks.Count; i++)
        {
            var track = tracks[i];
            var isCurrent = currentTrack != null && track.Id == currentTrack.Id && track.Title == currentTrack.Title;
            var prefix = isCurrent ? "▶ " : "  ";
            var title = track.Title.Length > 50 ? track.Title.Substring(0, 47) + "..." : track.Title;
            // Don't escape here - Spectre.Console handles it in SelectionPrompt
            choices.Add($"{prefix}{i + 1}. {title} ({track.Duration})");
        }
        
        choices.Add("« Back to menu");

        var selection = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[cyan]Select a track to play from here:[/]")
                .PageSize(15)
                .AddChoices(choices)
                .HighlightStyle(new Style(SpectreColor.Cyan1))
        );

        if (selection == "« Back to menu")
        {
            return;
        }

        // Extract track index
        var indexStr = selection.Split('.')[0].Trim().TrimStart('▶').Trim();
        if (int.TryParse(indexStr, out var trackIndex))
        {
            await JumpToTrackAsync(trackIndex - 1);
        }
    }

    private async Task JumpToTrackAsync(int index)
    {
        var track = _queue.JumpTo(index);
        if (track != null)
        {
            await _player.PlayAsync(track);
            await _notifications.ShowNowPlayingAsync(track);
            await _queue.SaveHistoryAsync();
            var title = track.Title.EscapeMarkup();
            AnsiConsole.MarkupLine($"[green]▶[/] Playing: {title}");
            await Task.Delay(1200);
        }
    }

    private async Task ClearQueue()
    {
        if (!AnsiConsole.Confirm("[yellow]⚠️  Clear entire playlist?[/]", false))
        {
            AnsiConsole.MarkupLine("[grey]Cancelled[/]");
            await Task.Delay(600);
            return;
        }
        
        _queue.Clear();
        await _queue.SaveHistoryAsync();
        AnsiConsole.MarkupLine("[green]✓[/] Playlist cleared");
    }

    private async Task StartPlaybackAsync()
    {
        // Simply play the first/next track
        var track = _queue.CurrentTrack ?? _queue.Next();
        if (track != null)
        {
            await _player.PlayAsync(track);
            await _notifications.ShowNowPlayingAsync(track);
            await _queue.SaveHistoryAsync();
        }
    }

    private async Task PlayNextAsync()
    {
        var track = _queue.Next();
        if (track != null)
        {
            await _player.PlayAsync(track);
            await _notifications.ShowNowPlayingAsync(track);
            await _queue.SaveHistoryAsync();
            var title = track.Title.EscapeMarkup();
            AnsiConsole.MarkupLine($"[green]▶[/] Playing: {title}");
        }
        else
        {
            AnsiConsole.MarkupLine("[yellow]No more tracks in queue[/]");
        }
    }

    private async Task CleanupAsync()
    {
        AnsiConsole.Clear();
        
        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Saving playlist...", async ctx =>
            {
                await _queue.SaveHistoryAsync();
                _player.Dispose();
            });
        
        AnsiConsole.MarkupLine("[green]✓[/] Playlist saved");
        AnsiConsole.MarkupLine("[cyan]Goodbye![/]");
    }
}
