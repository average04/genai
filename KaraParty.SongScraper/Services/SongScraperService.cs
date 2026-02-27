using KaraParty.SongScraper.Models;

namespace KaraParty.SongScraper.Services;

public class SongScraperService(
    SpotifyService spotify,
    LrcLibService lrcLib,
    AiEnrichmentService ai)
{
    public async Task<SongResult> ScrapeAsync(string spotifyUrl)
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
            Console.WriteLine("  No synced lyrics found — AI enrichment will be limited.");

        // 3. AI enrichment
        Console.WriteLine("Running AI enrichment...");
        var enrichment = await ai.EnrichAsync(song.Title, song.Artist, lrcLines);
        song.Language          = enrichment.Language;
        song.Mood              = enrichment.Mood;
        song.Genre             = enrichment.Genre;
        song.KaraokeDifficulty = enrichment.KaraokeDifficulty;
        song.LyricsSummary     = enrichment.LyricsSummary;

        // 4. AI pitch reference generation
        if (found && lrcLines.Count > 0)
        {
            Console.WriteLine("Generating pitch reference...");
            song.PitchReference = await ai.GeneratePitchReferenceAsync(
                song.Title, song.Artist, lrcLines);
        }

        return song;
    }
}
