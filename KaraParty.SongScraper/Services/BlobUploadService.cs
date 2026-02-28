using Azure.Storage.Blobs;
using KaraParty.SongScraper.Models;
using Microsoft.Extensions.Configuration;
using System.Text;
using System.Text.Json;

namespace KaraParty.SongScraper.Services;

public class BlobUploadService(BlobServiceClient blobClient, IConfiguration config)
{
    private readonly string _container = config["Blob:SongsContainer"] ?? "songs";

    public async Task<(string? lyricsBlobPath, string? pitchBlobPath, string? audioBlobPath, string? vocalsBlobPath)> UploadAsync(
        Guid songId, SongResult result, CancellationToken ct = default)
    {
        var container = blobClient.GetBlobContainerClient(_container);
        await container.CreateIfNotExistsAsync();

        string? lyricsBlobPath = null;
        string? pitchBlobPath  = null;
        string? audioBlobPath  = null;
        string? vocalsBlobPath = null;

        // Upload raw LRC file
        if (result.HasSyncedLyrics && result.RawLrc is not null)
        {
            var lrcPath  = $"{songId}/lyrics.lrc";
            var lrcBlob  = container.GetBlobClient(lrcPath);
            using var lrcStream = new MemoryStream(Encoding.UTF8.GetBytes(result.RawLrc));
            await lrcBlob.UploadAsync(lrcStream, overwrite: true);

            // Upload parsed LRC lines as JSON
            var jsonLyricsPath = $"{songId}/lyrics.json";
            var lyricsBlob     = container.GetBlobClient(jsonLyricsPath);
            var lyricsJson     = JsonSerializer.Serialize(result.LrcLines, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            using var lyricsStream = new MemoryStream(Encoding.UTF8.GetBytes(lyricsJson));
            await lyricsBlob.UploadAsync(lyricsStream, overwrite: true);

            lyricsBlobPath = jsonLyricsPath;
        }

        // Upload pitch reference as JSON
        if (result.PitchReference is { Count: > 0 })
        {
            var pitchPath = $"{songId}/pitch.json";
            var pitchBlob = container.GetBlobClient(pitchPath);
            var pitchJson = JsonSerializer.Serialize(result.PitchReference, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            using var pitchStream = new MemoryStream(Encoding.UTF8.GetBytes(pitchJson));
            await pitchBlob.UploadAsync(pitchStream, overwrite: true);

            pitchBlobPath = pitchPath;
        }

        // Upload instrumental MP3
        if (result.InstrumentalFilePath is not null && File.Exists(result.InstrumentalFilePath))
        {
            var blob = container.GetBlobClient($"{songId}/instrumental.mp3");
            await using var stream = File.OpenRead(result.InstrumentalFilePath);
            await blob.UploadAsync(stream, overwrite: true, cancellationToken: ct);
            audioBlobPath = $"{songId}/instrumental.mp3";

            // Upload vocals MP3
            if (result.VocalsFilePath is not null && File.Exists(result.VocalsFilePath))
            {
                var vocalsBlob = container.GetBlobClient($"{songId}/vocals.mp3");
                await using var vocalsStream = File.OpenRead(result.VocalsFilePath);
                await vocalsBlob.UploadAsync(vocalsStream, overwrite: true, cancellationToken: ct);
                vocalsBlobPath = $"{songId}/vocals.mp3";
            }

            // cleanup temp workDir (path is: {workDir}/separated/htdemucs/audio/no_vocals.mp3)
            // string? tempDir = result.InstrumentalFilePath;
            // for (int i = 0; i < 4; i++) tempDir = Path.GetDirectoryName(tempDir);
            // if (tempDir is not null && Directory.Exists(tempDir))
            //     Directory.Delete(tempDir, recursive: true);
        }

        return (lyricsBlobPath, pitchBlobPath, audioBlobPath, vocalsBlobPath);
    }
}
