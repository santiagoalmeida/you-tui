using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using YouTui.Shared.Models;

namespace YouTui.Client.Services;

public class MpvPlayer : IDisposable
{
    private Process? _mpvProcess;
    private readonly string _socketPath;
    private UnixDomainSocketEndPoint? _endpoint;
    private bool _isRunning;

    public event Action<Track>? OnTrackChanged;
    public event Action? OnPlaybackEnded;

    public MpvPlayer(string socketPath = "/tmp/you-tui-mpv.sock")
    {
        _socketPath = socketPath;
    }

    public async Task InitializeAsync()
    {
        if (File.Exists(_socketPath))
            File.Delete(_socketPath);

        _mpvProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "mpv",
                Arguments = $"--no-video --idle=yes --input-ipc-server={_socketPath} --keep-open=no",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        _mpvProcess.Start();
        _isRunning = true;

        // Wait for socket to be created
        for (int i = 0; i < 50; i++)
        {
            if (File.Exists(_socketPath))
            {
                _endpoint = new UnixDomainSocketEndPoint(_socketPath);
                break;
            }
            await Task.Delay(100);
        }

        if (_endpoint == null)
            throw new Exception("Failed to initialize MPV socket");
    }

    public async Task PlayAsync(Track track)
    {
        await SendCommandAsync(new { command = new[] { "loadfile", track.Url, "replace" } });
    }

    public async Task AddToPlaylistAsync(Track track)
    {
        await SendCommandAsync(new { command = new[] { "loadfile", track.Url, "append-play" } });
    }

    public async Task PauseAsync()
    {
        await SendCommandAsync(new { command = new[] { "cycle", "pause" } });
    }

    public async Task StopAsync()
    {
        await SendCommandAsync(new { command = new[] { "stop" } });
    }

    public async Task NextAsync()
    {
        await SendCommandAsync(new { command = new[] { "playlist-next" } });
    }

    private async Task SendCommandAsync(object command)
    {
        if (_endpoint == null || !_isRunning) return;

        try
        {
            using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            await socket.ConnectAsync(_endpoint);

            var json = JsonSerializer.Serialize(command) + "\n";
            var bytes = Encoding.UTF8.GetBytes(json);
            await socket.SendAsync(bytes, SocketFlags.None);
        }
        catch (Exception)
        {
            // Socket error, mpv might have closed
        }
    }

    public void Dispose()
    {
        _isRunning = false;

        try
        {
            SendCommandAsync(new { command = new[] { "quit" } }).Wait(1000);
        }
        catch { }

        _mpvProcess?.Kill(true);
        _mpvProcess?.Dispose();

        if (File.Exists(_socketPath))
            File.Delete(_socketPath);
    }
}
