using System.Text.Json;
using YouTui.Shared.Models;

namespace YouTui.Daemon.Services;

public class CommandHandler
{
    private readonly PlaybackQueue _queue;
    private readonly PlaybackEngine _engine;
    private CancellationTokenSource? _shutdownTokenSource;

    public CommandHandler(PlaybackQueue queue, PlaybackEngine engine)
    {
        _queue = queue;
        _engine = engine;
    }

    public void SetShutdownTokenSource(CancellationTokenSource cts)
    {
        _shutdownTokenSource = cts;
    }

    public async Task<DaemonResponse> HandleCommandAsync(DaemonCommand command)
    {
        try
        {
            switch (command.Command)
            {
                case "AddTrack":
                    var addTrackData = DeserializeData<AddTrackData>(command.Data);
                    if (addTrackData?.Track != null)
                    {
                        _queue.Enqueue(addTrackData.Track);
                        await _queue.SaveHistoryAsync();
                        await _engine.AddTrackToPlaylistAsync(addTrackData.Track);
                        await _engine.EnsurePlayingAsync();
                    }
                    return SuccessResponse();

                case "AddTracks":
                    var addTracksData = DeserializeData<AddTracksData>(command.Data);
                    if (addTracksData?.Tracks != null)
                    {
                        foreach (var track in addTracksData.Tracks)
                        {
                            _queue.Enqueue(track);
                            await _engine.AddTrackToPlaylistAsync(track);
                        }
                        await _queue.SaveHistoryAsync();
                        await _engine.EnsurePlayingAsync();
                    }
                    return SuccessResponse();

                case "Play":
                    await _engine.PlayAsync();
                    return SuccessResponse();

                case "Pause":
                    await _engine.PauseAsync();
                    return SuccessResponse();

                case "Next":
                    await _engine.NextAsync();
                    return SuccessResponse();

                case "Previous":
                    await _engine.PreviousAsync();
                    return SuccessResponse();

                case "JumpTo":
                    var jumpData = DeserializeData<JumpToData>(command.Data);
                    if (jumpData != null)
                    {
                        _queue.JumpTo(jumpData.Index);
                        await _queue.SaveHistoryAsync();
                        await _engine.PlayCurrentAsync();
                    }
                    return SuccessResponse();

                case "GetStatus":
                    return GetStatusResponse();

                case "ClearQueue":
                    _queue.Clear();
                    await _queue.SaveHistoryAsync();
                    return SuccessResponse();

                case "Stop":
                    // Trigger graceful shutdown instead of Environment.Exit
                    _shutdownTokenSource?.Cancel();
                    return SuccessResponse();

                default:
                    return ErrorResponse($"Unknown command: {command.Command}");
            }
        }
        catch (Exception ex)
        {
            return ErrorResponse(ex.Message);
        }
    }

    private T? DeserializeData<T>(object? data)
    {
        if (data == null) return default;
        
        var json = data is JsonElement element 
            ? element.GetRawText() 
            : JsonSerializer.Serialize(data);
            
        return JsonSerializer.Deserialize<T>(json);
    }

    private DaemonResponse SuccessResponse()
    {
        return new DaemonResponse
        {
            Status = "success",
            Data = GetStatus()
        };
    }

    private DaemonResponse GetStatusResponse()
    {
        return new DaemonResponse
        {
            Status = "success",
            Data = GetStatus()
        };
    }

    private DaemonResponse ErrorResponse(string message)
    {
        return new DaemonResponse
        {
            Status = "error",
            Message = message
        };
    }

    private DaemonStatus GetStatus()
    {
        // Get playback position asynchronously
        var timePos = _engine.GetTimePositionAsync().GetAwaiter().GetResult();
        var duration = _engine.GetDurationAsync().GetAwaiter().GetResult();
        
        return new DaemonStatus
        {
            CurrentTrack = _queue.CurrentTrack,
            Queue = _queue.GetAllTracks().ToList(),
            CurrentIndex = _queue.GetCurrentIndex(),
            IsPlaying = _engine.IsPlaying,
            QueueLength = _queue.TotalCount,
            PendingCount = _queue.Count,
            TimePosition = timePos,
            Duration = duration
        };
    }
}
