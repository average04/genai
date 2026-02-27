using KaraParty.SongScraper.Models;
using System.Text.Json;

namespace KaraParty.SongScraper.Services;

public class FileOutputService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task SaveAsync(SongResult song, string outputDir = "output")
    {
        Directory.CreateDirectory(outputDir);

        var safeName  = SanitizeFileName($"{song.Artist} - {song.Title}");
        var songDir   = Path.Combine(outputDir, safeName);
        Directory.CreateDirectory(songDir);

        // metadata + AI enrichment
        await File.WriteAllTextAsync(
            Path.Combine(songDir, "metadata.json"),
            JsonSerializer.Serialize(new
            {
                song.SpotifyId,
                song.Title,
                song.Artist,
                song.Album,
                song.DurationSeconds,
                song.CoverImageUrl,
                song.Language,
                song.Mood,
                song.Genre,
                song.KaraokeDifficulty,
                song.LyricsSummary
            }, JsonOptions));

        // raw LRC file
        if (song.RawLrc is not null)
            await File.WriteAllTextAsync(Path.Combine(songDir, "lyrics.lrc"), song.RawLrc);

        // parsed LRC as JSON
        if (song.LrcLines.Count > 0)
            await File.WriteAllTextAsync(
                Path.Combine(songDir, "lyrics.json"),
                JsonSerializer.Serialize(song.LrcLines, JsonOptions));

        // pitch reference
        if (song.PitchReference is { Count: > 0 })
            await File.WriteAllTextAsync(
                Path.Combine(songDir, "pitch.json"),
                JsonSerializer.Serialize(song.PitchReference, JsonOptions));

        Console.WriteLine($"Saved to: {songDir}");
    }

    private static string SanitizeFileName(string name) =>
        string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
}
