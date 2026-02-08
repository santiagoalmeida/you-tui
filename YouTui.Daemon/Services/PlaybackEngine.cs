using YouTui.Shared.Models;

namespace YouTui.Daemon.Services;

public class PlaybackEngine
{
    private readonly MpvPlayer _player;
    private readonly PlaybackQueue _queue;
    private bool _isPlaying;

    public bool IsPlaying => _isPlaying;

    public PlaybackEngine(MpvPlayer player, PlaybackQueue queue)
    {
        _player = player;
        _queue = queue;
    }

    public async Task InitializeAsync()
    {
        await _player.InitializeAsync();
        
        // Monitor MPV events for auto-advance
        _player.OnPlaybackEnded += async () =>
        {
            await NextAsync();
        };
    }

    public async Task EnsurePlayingAsync()
    {
        if (!_isPlaying && _queue.CurrentTrack != null)
        {
            await PlayCurrentAsync();
        }
    }
    
    public async Task AddTrackToPlaylistAsync(Track track)
    {
        // Add track to MPV playlist if already playing
        if (_isPlaying)
        {
            await _player.AddToPlaylistAsync(track);
        }
    }

    public async Task PlayCurrentAsync()
    {
        var track = _queue.CurrentTrack;
        if (track != null)
        {
            // Load current track and all remaining tracks to MPV playlist
            var remainingTracks = _queue.GetAllTracks().Skip(_queue.GetCurrentIndex()).ToList();
            await _player.LoadPlaylistAsync(remainingTracks);
            _isPlaying = true;
        }
    }

    public async Task PlayAsync()
    {
        // Resume playback (MPV command: set pause false)
        await _player.SendCommandAsync(new { command = new string[] { "set_property", "pause", "no" } });
        _isPlaying = true;
    }

    public async Task PauseAsync()
    {
        // Pause playback (MPV command: set pause true)
        await _player.SendCommandAsync(new { command = new string[] { "set_property", "pause", "yes" } });
        _isPlaying = false;
    }

    public async Task NextAsync()
    {
        var nextTrack = _queue.Next();
        await _queue.SaveHistoryAsync();
        
        if (nextTrack != null)
        {
            // Use MPV's playlist-next command instead of reloading
            await _player.NextAsync();
            _isPlaying = true;
        }
        else
        {
            _isPlaying = false;
        }
    }

    public async Task PreviousAsync()
    {
        var previousTrack = _queue.Previous();
        await _queue.SaveHistoryAsync();
        
        if (previousTrack != null)
        {
            await _player.PlayAsync(previousTrack);
            _isPlaying = true;
        }
    }

    public async Task<double> GetTimePositionAsync()
    {
        return await _player.GetTimePositionAsync();
    }

    public async Task<double> GetDurationAsync()
    {
        return await _player.GetDurationAsync();
    }

    public void Dispose()
    {
        _player?.Dispose();
    }
}
