using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using YouTui.Shared.Models;

namespace YouTui.Client.Services;

public class DaemonClient : IDisposable
{
    private readonly string _socketPath;

    public DaemonClient(string socketPath = "/tmp/you-tui-daemon.sock")
    {
        _socketPath = socketPath;
    }

    public async Task<DaemonResponse> SendCommandAsync(string command, object? data = null)
    {
        try
        {
            using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            var endpoint = new UnixDomainSocketEndPoint(_socketPath);
            await socket.ConnectAsync(endpoint);

            using var stream = new NetworkStream(socket);
            // Use UTF8Encoding(false) for writing (no BOM), Encoding.UTF8 with BOM detection for reading
            using var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

            var cmd = new DaemonCommand
            {
                Command = command,
                Data = data
            };

            var json = JsonSerializer.Serialize(cmd);
            await writer.WriteLineAsync(json);

            var response = await reader.ReadLineAsync();
            if (response == null)
                return ErrorResponse("No response from daemon");

            var options = new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            };
            
            return JsonSerializer.Deserialize<DaemonResponse>(response, options) 
                ?? ErrorResponse("Invalid response format");
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionRefused || 
                                          ex.SocketErrorCode == SocketError.AddressNotAvailable)
        {
            return ErrorResponse("Daemon not running");
        }
        catch (Exception ex)
        {
            return ErrorResponse($"Error communicating with daemon: {ex.Message}");
        }
    }

    public async Task<DaemonStatus?> GetStatusAsync()
    {
        var response = await SendCommandAsync("GetStatus");
        return response.Status == "success" ? response.Data : null;
    }

    public async Task<bool> AddTrackAsync(Track track)
    {
        var response = await SendCommandAsync("AddTrack", new AddTrackData { Track = track });
        return response.Status == "success";
    }

    public async Task<bool> AddTracksAsync(List<Track> tracks)
    {
        var response = await SendCommandAsync("AddTracks", new AddTracksData { Tracks = tracks });
        return response.Status == "success";
    }

    public async Task<bool> PlayAsync()
    {
        var response = await SendCommandAsync("Play");
        return response.Status == "success";
    }

    public async Task<bool> PauseAsync()
    {
        var response = await SendCommandAsync("Pause");
        return response.Status == "success";
    }

    public async Task<bool> NextAsync()
    {
        var response = await SendCommandAsync("Next");
        return response.Status == "success";
    }

    public async Task<bool> PreviousAsync()
    {
        var response = await SendCommandAsync("Previous");
        return response.Status == "success";
    }

    public async Task<bool> JumpToAsync(int index)
    {
        var response = await SendCommandAsync("JumpTo", new JumpToData { Index = index });
        return response.Status == "success";
    }

    public async Task<bool> ClearQueueAsync()
    {
        var response = await SendCommandAsync("ClearQueue");
        return response.Status == "success";
    }

    public async Task<bool> StopDaemonAsync()
    {
        var response = await SendCommandAsync("Stop");
        return response.Status == "success";
    }

    public async Task<bool> IsDaemonRunningAsync()
    {
        if (!File.Exists(_socketPath))
            return false;

        try
        {
            var response = await SendCommandAsync("GetStatus");
            return response.Status != "error" || !response.Message?.Contains("Daemon not running") == true;
        }
        catch
        {
            return false;
        }
    }

    private DaemonResponse ErrorResponse(string message)
    {
        return new DaemonResponse
        {
            Status = "error",
            Message = message
        };
    }

    public void Dispose()
    {
        // Nothing to dispose in current implementation
    }
}
