namespace KaraParty.SongScraper.Models;

public class SongResult
{
    public string SpotifyId      { get; set; } = default!;
    public string Title          { get; set; } = default!;
    public string Artist         { get; set; } = default!;
    public string Album          { get; set; } = default!;
    public int    DurationSeconds { get; set; }
    public string? CoverImageUrl { get; set; }

    // Lyrics
    public bool              HasSyncedLyrics { get; set; }
    public string?           RawLrc          { get; set; }
    public List<LrcLine>     LrcLines        { get; set; } = [];

    // AI enrichment
    public string? Language          { get; set; }
    public string? Mood              { get; set; }
    public string? Genre             { get; set; }
    public string? KaraokeDifficulty { get; set; }
    public string? LyricsSummary     { get; set; }

    // Pitch reference (for KaraParty scoring)
    public List<PitchPoint>? PitchReference { get; set; }
}

public record LrcLine(double TimestampSeconds, string Text);

public record PitchPoint(double T, string Note, double Hz);
