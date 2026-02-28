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

        try
        {
            var result = await ai.GetResponseAsync([new ChatMessage(ChatRole.User, prompt)]);
            var json   = result.Message.Text!.Trim().Trim('`').Replace("json\n", "").Trim();
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

    public async Task<List<LrcLine>> GenerateWordTimingsAsync(
        string title, string artist, List<LrcLine> lines)
    {
        // Build a compact representation: index, start, end, text
        var lineDescriptions = lines.Select((l, i) =>
        {
            var nextStart = i + 1 < lines.Count ? lines[i + 1].TimestampSeconds : l.TimestampSeconds + 5;
            return $"{i}: [{l.TimestampSeconds:F2}s–{nextStart:F2}s] {l.Text}";
        });
        var linesBlock = string.Join("\n", lineDescriptions);

        var prompt = $$"""
            You are a music timing expert. Given lyric lines with known start and end timestamps,
            estimate when each word starts within its line for the song "{{title}}" by {{artist}}.
            Words must fall within their line's time range. Account for musical phrasing and natural
            speech rhythm — short function words are typically brief, stressed syllables are longer.

            Lyric lines (index: [start–end] text):
            {{linesBlock}}

            Return ONLY a valid JSON array. One object per line, in order:
            [
              { "words": [{ "text": "word", "start": 0.00 }, ...] },
              ...
            ]
            """;

        try
        {
            var result = await ai.GetResponseAsync([new ChatMessage(ChatRole.User, prompt)]);
            var json   = result.Message.Text!.Trim().Trim('`').Replace("json\n", "").Trim();

            var aiLines = JsonSerializer.Deserialize<List<AiWordLine>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (aiLines is null || aiLines.Count != lines.Count)
                return lines;

            return lines.Select((line, i) =>
            {
                var aiWords = aiLines[i].Words;
                if (aiWords is not { Count: > 0 }) return line;

                var lrcWords = aiWords.Select((w, wi) => new LrcWord(
                    w.Start,
                    wi + 1 < aiWords.Count ? aiWords[wi + 1].Start : (double?)null,
                    w.Text
                )).ToList();

                return line with { Words = lrcWords };
            }).ToList();
        }
        catch
        {
            return lines;
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

        try
        {
            var result = await ai.GetResponseAsync([new ChatMessage(ChatRole.User, prompt)]);
            var json   = result.Message.Text!.Trim().Trim('`').Replace("json\n", "").Trim();
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

file class AiWordLine
{
    public List<AiWord>? Words { get; set; }
}

file class AiWord
{
    public string Text  { get; set; } = "";
    public double Start { get; set; }
}
