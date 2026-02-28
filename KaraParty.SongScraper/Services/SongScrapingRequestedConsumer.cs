using KaraParty.Contracts;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace KaraParty.SongScraper.Services;

public class SongScrapingRequestedConsumer(
    SongScraperService  scraper,
    BlobUploadService   blobUpload,
    IPublishEndpoint    publisher,
    ILogger<SongScrapingRequestedConsumer> logger)
    : IConsumer<SongScrapingRequestedEvent>
{
    public async Task Consume(ConsumeContext<SongScrapingRequestedEvent> context)
    {
        var msg = context.Message;
        logger.LogInformation("Processing song {SongId}: {SpotifyUrl}", msg.SongId, msg.SpotifyUrl);

        var result = await scraper.ScrapeAsync(msg.SpotifyUrl, msg.CheckYoutube, context.CancellationToken);

        var (lyricsBlobPath, pitchBlobPath, audioBlobPath, vocalsBlobPath) = await blobUpload.UploadAsync(msg.SongId, result, context.CancellationToken);

        await publisher.Publish(new SongScrapedEvent(
            SongId:           msg.SongId,
            Title:            result.Title,
            Artist:           result.Artist,
            Album:            result.Album,
            DurationSeconds:  result.DurationSeconds,
            CoverImageUrl:    result.CoverImageUrl,
            Language:         result.Language,
            Mood:             result.Mood,
            Genre:            result.Genre,
            KaraokeDifficulty: result.KaraokeDifficulty,
            LyricsSummary:    result.LyricsSummary,
            HasSyncedLyrics:  result.HasSyncedLyrics,
            LyricsBlobPath:   lyricsBlobPath,
            PitchBlobPath:    pitchBlobPath,
            AudioBlobPath:    audioBlobPath,
            VocalsBlobPath:   vocalsBlobPath),
            context.CancellationToken);

        logger.LogInformation("Song {SongId} scraping complete. Published SongScrapedEvent.", msg.SongId);
    }
}
