using YouTui.Daemon.Services;

Console.WriteLine("Starting you-tui daemon...");

const string socketPath = "/tmp/you-tui-daemon.sock";
const string mpvSocketPath = "/tmp/you-tui-mpv.sock";

var queue = new PlaybackQueue();
await queue.LoadHistoryAsync();

var player = new MpvPlayer(mpvSocketPath);
await player.InitializeAsync();

var engine = new PlaybackEngine(player, queue);
await engine.InitializeAsync();

var handler = new CommandHandler(queue, engine);
var server = new DaemonServer(socketPath, handler);

var cts = new CancellationTokenSource();
handler.SetShutdownTokenSource(cts); // Pass CTS to handler

await server.StartAsync();

// If there's a current track, start playing
if (queue.CurrentTrack != null)
{
    await engine.PlayCurrentAsync();
}

Console.WriteLine("Daemon started successfully. Press Ctrl+C to stop.");

Console.CancelKeyPress += (sender, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

try
{
    await Task.Delay(Timeout.Infinite, cts.Token);
}
catch (TaskCanceledException)
{
    Console.WriteLine("\nShutting down daemon...");
}

await server.StopAsync();
server.Dispose();
engine.Dispose();

await queue.SaveHistoryAsync();
Console.WriteLine("Daemon stopped.");

