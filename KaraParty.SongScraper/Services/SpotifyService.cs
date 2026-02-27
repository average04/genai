using KaraParty.SongScraper.Models;
using SpotifyAPI.Web;

namespace KaraParty.SongScraper.Services;

public class SpotifyService(SpotifyClient client)
{
    public async Task<SongResult> GetTrackAsync(string spotifyUrl)
    {
        var trackId = ExtractTrackId(spotifyUrl);
        var track   = await client.Tracks.Get(trackId);

        return new SongResult
        {
            SpotifyId       = track.Id,
            Title           = track.Name,
            Artist          = string.Join(", ", track.Artists.Select(a => a.Name)),
            Album           = track.Album.Name,
            DurationSeconds = track.DurationMs / 1000,
            CoverImageUrl   = track.Album.Images.FirstOrDefault()?.Url
        };
    }

    private static string ExtractTrackId(string url)
    {
        // Handles:
        // https://open.spotify.com/track/4uLU6hMCjMI75M1A2tKUQC
        // spotify:track:4uLU6hMCjMI75M1A2tKUQC
        var uri = new Uri(url.StartsWith("spotify:") ? $"https://open.spotify.com/{url.Replace(":", "/").Replace("spotify/", "")}" : url);
        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var trackIndex = Array.IndexOf(segments, "track");
        if (trackIndex == -1 || trackIndex + 1 >= segments.Length)
            throw new ArgumentException($"Could not extract track ID from URL: {url}");
        return segments[trackIndex + 1].Split('?')[0];
    }
}
