namespace YouTui.Shared.Models;

public class DaemonStatus
{
    public Track? CurrentTrack { get; set; }
    public List<Track> Queue { get; set; } = new();
    public int CurrentIndex { get; set; }
    public bool IsPlaying { get; set; }
    public int QueueLength { get; set; }
    public int PendingCount { get; set; }
    public double TimePosition { get; set; } // Current playback position in seconds
    public double Duration { get; set; } // Total duration in seconds
}
