using System.Diagnostics;
using YouTui.Models;

namespace YouTui.Services;

public class FzfSelector
{
    public async Task<Track?> SelectAsync(List<Track> tracks, string prompt = "Select a track")
    {
        if (tracks.Count == 0)
            return null;

        var input = string.Join('\n', tracks.Select((t, i) => $"{i}|{t}"));

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "fzf",
                Arguments = $"--prompt=\"{prompt}: \" --height=40% --reverse --border",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };

        process.Start();
        await process.StandardInput.WriteAsync(input);
        process.StandardInput.Close();

        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
            return null;

        var indexStr = output.Trim().Split('|')[0];
        if (int.TryParse(indexStr, out var index) && index >= 0 && index < tracks.Count)
        {
            return tracks[index];
        }

        return null;
    }

    public async Task<List<Track>> SelectMultipleAsync(List<Track> tracks, string prompt = "Select tracks (TAB to select)")
    {
        if (tracks.Count == 0)
            return new List<Track>();

        var input = string.Join('\n', tracks.Select((t, i) => $"{i}|{t}"));

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "fzf",
                Arguments = $"--multi --prompt=\"{prompt}: \" --height=40% --reverse --border",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };

        process.Start();
        await process.StandardInput.WriteAsync(input);
        process.StandardInput.Close();

        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
            return new List<Track>();

        var selected = new List<Track>();
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var indexStr = line.Trim().Split('|')[0];
            if (int.TryParse(indexStr, out var index) && index >= 0 && index < tracks.Count)
            {
                selected.Add(tracks[index]);
            }
        }

        return selected;
    }
}
