using KaraParty.SongScraper.Models;
using Microsoft.Extensions.AI;
using System.Text.Json;

namespace KaraParty.SongScraper.Services;

public class AiEnrichmentService(IChatClient ai)
{
    public async Task<AiEnrichment> EnrichAsync(string title, string artist, List<LrcLine> lyrics)
    {
        var lyricsText = string.Join("\n", lyrics.Select(l => l.Text));

        var prompt = $$"""
            You are a music analyst. Analyze the following song and return a JSON object.

            Song: "{{title}}" by {{artist}}
            Lyrics:
            {{lyricsText}}

            Return ONLY a valid JSON object with these exact fields:
            {
              "language": "detected language of the lyrics (e.g. English, Filipino, Spanish)",
              "mood": "overall mood (e.g. Sad, Happy, Romantic, Energetic, Melancholic)",
              "genre": "music genre (e.g. Pop, OPM, R&B, Rock, Ballad)",
              "karaokeDifficulty": "Easy, Medium, or Hard based on vocal range and complexity",
              "lyricsSummary": "one sentence summary of what the song is about"
            }
            """;

        var result = await ai.GetResponseAsync([new ChatMessage(ChatRole.User, prompt)]);
        var json   = result.Message.Text!.Trim().Trim('`').Replace("json\n", "").Trim();

        try
        {
            var enrichment = JsonSerializer.Deserialize<AiEnrichment>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            return enrichment ?? new AiEnrichment();
        }
        catch
        {
            return new AiEnrichment();
        }
    }

    public async Task<List<PitchPoint>> GeneratePitchReferenceAsync(
        string title, string artist, List<LrcLine> lyrics)
    {
        var lyricsWithTimestamps = string.Join("\n", lyrics.Select(l => $"[{l.TimestampSeconds:F2}s] {l.Text}"));

        var prompt = $$"""
            You are a music theory expert. Generate a pitch reference for the karaoke song "{{title}}" by {{artist}}.

            The pitch reference maps timestamps to expected musical notes for the melody.
            Based on the lyric timestamps below, generate pitch points at regular intervals.

            Lyrics with timestamps:
            {{lyricsWithTimestamps}}

            Return ONLY a valid JSON array of pitch points. Each point must have:
            - "t": timestamp in seconds (number)
            - "note": musical note name (e.g. "A4", "C5", "G3")
            - "hz": frequency in Hz (number, accurate for the note)

            Generate approximately one pitch point every 0.5 seconds, covering the full song duration.
            Return the first 30 points only to keep the response concise.
            """;

        var result = await ai.GetResponseAsync([new ChatMessage(ChatRole.User, prompt)]);
        var json   = result.Message.Text!.Trim().Trim('`').Replace("json\n", "").Trim();

        try
        {
            return JsonSerializer.Deserialize<List<PitchPoint>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? [];
        }
        catch
        {
            return [];
        }
    }
}

public class AiEnrichment
{
    public string? Language          { get; set; }
    public string? Mood              { get; set; }
    public string? Genre             { get; set; }
    public string? KaraokeDifficulty { get; set; }
    public string? LyricsSummary     { get; set; }
}
