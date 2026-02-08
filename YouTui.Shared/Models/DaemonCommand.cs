namespace YouTui.Shared.Models;

public class DaemonCommand
{
    public string Command { get; set; } = string.Empty;
    public object? Data { get; set; }
}

public class AddTrackData
{
    public Track? Track { get; set; }
}

public class AddTracksData
{
    public List<Track> Tracks { get; set; } = new();
}

public class JumpToData
{
    public int Index { get; set; }
}
