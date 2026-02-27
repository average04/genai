using KaraParty.SongScraper.Models;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace KaraParty.SongScraper.Services;

public class LrcLibService
{
    private readonly HttpClient _http = new() { BaseAddress = new Uri("https://lrclib.net/api/") };

    public async Task<(bool found, string? rawLrc, List<LrcLine> lines)> GetLyricsAsync(
        string title, string artist, double durationSeconds)
    {
        var query = $"search?q={Uri.EscapeDataString($"{title} {artist}")}";
        var results = await _http.GetFromJsonAsync<List<LrcLibResult>>(query);

        var match = results?.FirstOrDefault(r =>
            r.SyncedLyrics != null &&
            Math.Abs(r.Duration - durationSeconds) < 10);

        if (match?.SyncedLyrics is null)
            return (false, null, []);

        var lines = ParseLrc(match.SyncedLyrics);
        return (true, match.SyncedLyrics, lines);
    }

    private static List<LrcLine> ParseLrc(string lrc)
    {
        var lines  = new List<LrcLine>();
        foreach (var line in lrc.Split('\n'))
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith('[')) continue;
            var close = trimmed.IndexOf(']');
            if (close == -1) continue;

            var timestamp = trimmed[1..close];
            var text      = trimmed[(close + 1)..].Trim();
            if (string.IsNullOrEmpty(text)) continue;

            if (TryParseTimestamp(timestamp, out var seconds))
                lines.Add(new LrcLine(seconds, text));
        }
        return lines;
    }

    private static bool TryParseTimestamp(string ts, out double seconds)
    {
        seconds = 0;
        // Format: mm:ss.xx
        var parts = ts.Split(':');
        if (parts.Length != 2) return false;
        if (!int.TryParse(parts[0], out var minutes)) return false;
        if (!double.TryParse(parts[1], System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var secs)) return false;
        seconds = minutes * 60 + secs;
        return true;
    }

    private class LrcLibResult
    {
        [JsonPropertyName("syncedLyrics")] public string? SyncedLyrics { get; set; }
        [JsonPropertyName("duration")]     public double  Duration     { get; set; }
    }
}
