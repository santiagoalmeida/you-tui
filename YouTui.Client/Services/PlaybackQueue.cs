using System.Text.Json;
using YouTui.Shared.Models;

namespace YouTui.Client.Services;

public class PlaybackQueue
{
    private readonly List<Track> _tracks = new();
    private readonly string _historyFile;
    private int _currentIndex = -1;

    public Track? CurrentTrack => _currentIndex >= 0 && _currentIndex < _tracks.Count 
        ? _tracks[_currentIndex] 
        : null;
    
    public int Count => _tracks.Count - (_currentIndex + 1);
    public int TotalCount => _tracks.Count;

    public PlaybackQueue(string historyFile = "~/.config/you-tui/history.json")
    {
        _historyFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".config", "you-tui", "history.json"
        );
    }

    public void Enqueue(Track track)
    {
        _tracks.Add(track);
    }

    public void EnqueueRange(IEnumerable<Track> tracks)
    {
        _tracks.AddRange(tracks);
    }

    public Track? Next()
    {
        _currentIndex++;
        if (_currentIndex < _tracks.Count)
        {
            return _tracks[_currentIndex];
        }
        _currentIndex = _tracks.Count - 1;
        return null;
    }

    public Track? JumpTo(int index)
    {
        if (index >= 0 && index < _tracks.Count)
        {
            _currentIndex = index;
            return _tracks[_currentIndex];
        }
        return null;
    }

    public void Clear()
    {
        _tracks.Clear();
        _currentIndex = -1;
    }

    public IEnumerable<Track> GetAll() => _tracks.Skip(_currentIndex + 1).ToList();
    
    public IEnumerable<Track> GetAllTracks() => _tracks.ToList();

    public async Task SaveHistoryAsync()
    {
        var directory = Path.GetDirectoryName(_historyFile);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var state = new
        {
            CurrentIndex = _currentIndex,
            Tracks = _tracks
        };

        var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_historyFile, json);
    }

    public async Task LoadHistoryAsync()
    {
        if (!File.Exists(_historyFile))
            return;

        try
        {
            var json = await File.ReadAllTextAsync(_historyFile);
            var doc = JsonSerializer.Deserialize<JsonElement>(json);
            
            if (doc.TryGetProperty("Tracks", out var tracksElement))
            {
                var tracks = JsonSerializer.Deserialize<List<Track>>(tracksElement.GetRawText());
                if (tracks != null)
                {
                    _tracks.AddRange(tracks);
                }
            }
            
            if (doc.TryGetProperty("CurrentIndex", out var indexElement))
            {
                _currentIndex = indexElement.GetInt32();
            }
        }
        catch (Exception)
        {
            // Ignore corrupted history
        }
    }
}
