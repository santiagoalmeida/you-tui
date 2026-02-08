namespace YouTui.Shared.Models;

public class DaemonResponse
{
    public string Status { get; set; } = string.Empty;
    public string? Message { get; set; }
    public DaemonStatus? Data { get; set; }
}
