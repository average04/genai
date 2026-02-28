using KaraParty.SongScraper.Models;

namespace KaraParty.SongScraper.Services;

public class SongScraperService(
    SpotifyService spotify,
    LrcLibService lrcLib,
    AiEnrichmentService ai,
    AudioProcessingService audio,
    WhisperService whisper)
{
    public async Task<SongResult> ScrapeAsync(string spotifyUrl, bool checkYoutube = true, CancellationToken ct = default)
    {
        // 1. Spotify metadata
        Console.WriteLine("Fetching track metadata from Spotify...");
        var song = await spotify.GetTrackAsync(spotifyUrl);

        // 2. Synced lyrics from LRCLIB
        Console.WriteLine("Fetching synced lyrics from LRCLIB...");
        var (found, rawLrc, lrcLines) = await lrcLib.GetLyricsAsync(
            song.Title, song.Artist, song.DurationSeconds);

        song.HasSyncedLyrics = found;
        song.RawLrc          = rawLrc;
        song.LrcLines        = lrcLines;

        if (!found)
            Console.WriteLine("  No synced lyrics found on LRCLIB.");
        else if (song.LrcLines.All(l => l.Words is null))
        {
            Console.WriteLine("Generating per-word timestamps with AI (Tier 3)...");
            song.LrcLines = await ai.GenerateWordTimingsAsync(song.Title, song.Artist, song.LrcLines);
        }

        // 3. Audio separation (optional — song is still usable without it)
        Console.WriteLine("Downloading and separating audio (this may take several minutes)...");
        try
        {
            var workDir = Path.Combine(Path.GetTempPath(), "karaparty", Guid.NewGuid().ToString());
            Directory.CreateDirectory(workDir);
            song.InstrumentalFilePath = await audio.DownloadAndSeparateAsync(
                song.Title, song.Artist, song.DurationSeconds, workDir, checkYoutube, ct);

            if (song.InstrumentalFilePath is not null)
            {
                var vocalsPath = Path.Combine(
                    Path.GetDirectoryName(song.InstrumentalFilePath)!, "vocals.mp3");
                if (File.Exists(vocalsPath))
                    song.VocalsFilePath = vocalsPath;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Audio separation failed (non-fatal): {ex.Message}");
        }

        // 4. Whisper fallback — generate synced lyrics from vocals if LRCLIB had none
        if (!song.HasSyncedLyrics && song.VocalsFilePath is not null)
        {
            Console.WriteLine("Generating synced lyrics with Whisper...");
            var transcription = await whisper.TranscribeAsync(song.VocalsFilePath, ct);
            if (transcription is not null)
            {
                song.HasSyncedLyrics = true;
                song.RawLrc          = transcription.Value.rawLrc;
                song.LrcLines        = transcription.Value.lines;
                Console.WriteLine($"  Whisper generated {song.LrcLines.Count} lines.");
            }
        }

        // 5. AI enrichment
        Console.WriteLine("Running AI enrichment...");
        var enrichment = await ai.EnrichAsync(song.Title, song.Artist, song.LrcLines);
        song.Language          = enrichment.Language;
        song.Mood              = enrichment.Mood;
        song.Genre             = enrichment.Genre;
        song.KaraokeDifficulty = enrichment.KaraokeDifficulty;
        song.LyricsSummary     = enrichment.LyricsSummary;

        // 6. AI pitch reference generation
        if (song.HasSyncedLyrics && song.LrcLines.Count > 0)
        {
            Console.WriteLine("Generating pitch reference...");
            song.PitchReference = await ai.GeneratePitchReferenceAsync(
                song.Title, song.Artist, song.LrcLines);
        }

        return song;
    }
}
