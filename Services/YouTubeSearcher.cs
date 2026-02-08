using System.Diagnostics;
using System.Text.Json;
using YouTui.Models;

namespace YouTui.Services;

public class YouTubeSearcher
{
    public async Task<List<Track>> SearchAsync(string query, int maxResults = 20)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "yt-dlp",
                Arguments = $"\"ytsearch{maxResults}:{query}\" --dump-json --flat-playlist",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        var tracks = new List<Track>();
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            try
            {
                var json = JsonSerializer.Deserialize<JsonElement>(line);
                
                var durationSeconds = 0;
                if (json.TryGetProperty("duration", out var durProp) && durProp.ValueKind != JsonValueKind.Null)
                {
                    if (durProp.ValueKind == JsonValueKind.Number)
                    {
                        if (durProp.TryGetInt32(out var durInt))
                        {
                            durationSeconds = durInt;
                        }
                        else if (durProp.TryGetDouble(out var durDouble))
                        {
                            durationSeconds = (int)durDouble;
                        }
                    }
                }

                tracks.Add(new Track
                {
                    Id = json.GetProperty("id").GetString() ?? "",
                    Title = json.GetProperty("title").GetString() ?? "Unknown",
                    Uploader = json.TryGetProperty("uploader", out var uploader) 
                        ? uploader.GetString() ?? "Unknown" 
                        : "Unknown",
                    Duration = FormatDuration(durationSeconds),
                    Url = $"https://www.youtube.com/watch?v={json.GetProperty("id").GetString()}",
                    Thumbnail = json.TryGetProperty("thumbnail", out var thumb) 
                        ? thumb.GetString() ?? "" 
                        : ""
                });
            }
            catch
            {
                // Skip malformed entries
            }
        }

        return tracks;
    }

    public async Task<List<Track>> GetPlaylistAsync(string playlistUrl)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "yt-dlp",
                Arguments = $"\"{playlistUrl}\" --dump-json --flat-playlist",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        var tracks = new List<Track>();
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            try
            {
                var json = JsonSerializer.Deserialize<JsonElement>(line);
                
                var durationSeconds = 0;
                if (json.TryGetProperty("duration", out var durProp) && durProp.ValueKind != JsonValueKind.Null)
                {
                    if (durProp.ValueKind == JsonValueKind.Number)
                    {
                        if (durProp.TryGetInt32(out var durInt))
                        {
                            durationSeconds = durInt;
                        }
                        else if (durProp.TryGetDouble(out var durDouble))
                        {
                            durationSeconds = (int)durDouble;
                        }
                    }
                }

                tracks.Add(new Track
                {
                    Id = json.GetProperty("id").GetString() ?? "",
                    Title = json.GetProperty("title").GetString() ?? "Unknown",
                    Uploader = json.TryGetProperty("uploader", out var uploader) 
                        ? uploader.GetString() ?? "Unknown" 
                        : "Unknown",
                    Duration = FormatDuration(durationSeconds),
                    Url = $"https://www.youtube.com/watch?v={json.GetProperty("id").GetString()}",
                    Thumbnail = json.TryGetProperty("thumbnail", out var thumb) 
                        ? thumb.GetString() ?? "" 
                        : ""
                });
            }
            catch
            {
                // Skip malformed entries
            }
        }

        return tracks;
    }

    private static string FormatDuration(int seconds)
    {
        if (seconds == 0) return "LIVE";
        var ts = TimeSpan.FromSeconds(seconds);
        return ts.Hours > 0 
            ? $"{ts.Hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}"
            : $"{ts.Minutes:D2}:{ts.Seconds:D2}";
    }
}
