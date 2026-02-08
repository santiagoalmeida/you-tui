using System.Diagnostics;
using YouTui.Models;

namespace YouTui.Services;

public class NotificationManager
{
    public async Task ShowNowPlayingAsync(Track track)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "notify-send",
                Arguments = $"-a \"you-tui\" -i \"media-playback-start\" \"Now Playing\" \"{track.Title}\n{track.Uploader}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        try
        {
            process.Start();
            await process.WaitForExitAsync();
        }
        catch
        {
            // notify-send not available
        }
    }

    public async Task ShowMessageAsync(string title, string message)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "notify-send",
                Arguments = $"-a \"you-tui\" \"{title}\" \"{message}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        try
        {
            process.Start();
            await process.WaitForExitAsync();
        }
        catch
        {
            // notify-send not available
        }
    }
}
