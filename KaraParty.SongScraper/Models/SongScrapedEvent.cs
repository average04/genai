namespace KaraParty.Contracts;

public record SongScrapedEvent(
    Guid    SongId,
    string? Title,
    string? Artist,
    string? Album,
    double? DurationSeconds,
    string? CoverImageUrl,
    string? Language,
    string? Mood,
    string? Genre,
    string? KaraokeDifficulty,
    string? LyricsSummary,
    bool    HasSyncedLyrics,
    string? LyricsBlobPath,
    string? PitchBlobPath,
    string? AudioBlobPath,
    string? VocalsBlobPath);
