using KaraParty.SongScraper.Models;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace KaraParty.SongScraper.Services;

public class LrcLibService(HttpClient http)
{
    private readonly HttpClient _http = http;

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

    // Matches <mm:ss.xx> inline word-timestamp tokens in enhanced LRC
    private static readonly System.Text.RegularExpressions.Regex WordTokenRegex =
        new(@"<(\d{2}:\d{2}\.\d{2})>([^<\[]*)", System.Text.RegularExpressions.RegexOptions.Compiled);

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
            var rest      = trimmed[(close + 1)..];
            if (!TryParseTimestamp(timestamp, out var lineStart)) continue;

            // Enhanced LRC: rest contains <mm:ss.xx>word tokens
            if (rest.Contains('<'))
            {
                var words = new List<LrcWord>();
                foreach (System.Text.RegularExpressions.Match m in WordTokenRegex.Matches(rest))
                {
                    var wordText = m.Groups[2].Value.Trim();
                    if (string.IsNullOrEmpty(wordText)) continue;
                    if (TryParseTimestamp(m.Groups[1].Value, out var wordStart))
                        words.Add(new LrcWord(wordStart, null, wordText));
                }

                // Set End = next word's Start
                for (var i = 0; i < words.Count - 1; i++)
                    words[i] = words[i] with { End = words[i + 1].Start };

                var plainText = System.Text.RegularExpressions.Regex.Replace(rest, @"<[^>]+>", "").Trim();
                if (!string.IsNullOrEmpty(plainText) || words.Count > 0)
                    lines.Add(new LrcLine(lineStart, plainText, words.Count > 0 ? words : null));
            }
            else
            {
                var text = rest.Trim();
                if (!string.IsNullOrEmpty(text))
                    lines.Add(new LrcLine(lineStart, text));
            }
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
