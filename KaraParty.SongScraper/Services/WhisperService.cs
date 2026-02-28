using KaraParty.SongScraper.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KaraParty.SongScraper.Services;

public class WhisperService(IConfiguration config, ILogger<WhisperService> logger)
{
    private readonly string _whisperPath = config["Audio:WhisperPath"] ?? "py -3.11 -m whisper";
    private readonly string _model       = config["Audio:WhisperModel"] ?? "base";

    public async Task<(string rawLrc, List<LrcLine> lines)?> TranscribeAsync(
        string vocalsPath, CancellationToken ct)
    {
        try
        {
            var outputDir = Path.GetDirectoryName(vocalsPath)!;
            var args = $"\"{vocalsPath}\" --model {_model} --output_format json --word_timestamps True --output_dir \"{outputDir}\"";

            logger.LogInformation("Running Whisper on '{Path}' with model '{Model}'", vocalsPath, _model);
            if (!await RunProcess(args, ct))
            {
                logger.LogWarning("Whisper failed for '{Path}'", vocalsPath);
                return null;
            }

            var jsonPath = Path.Combine(outputDir,
                Path.GetFileNameWithoutExtension(vocalsPath) + ".json");

            if (!File.Exists(jsonPath))
            {
                logger.LogWarning("Whisper finished but output JSON not found at '{Path}'", jsonPath);
                return null;
            }

            var json   = await File.ReadAllTextAsync(jsonPath, ct);
            var result = JsonSerializer.Deserialize<WhisperResult>(json);

            if (result?.Segments is null or { Count: 0 })
                return null;

            var lines = result.Segments
                .Where(s => !string.IsNullOrWhiteSpace(s.Text))
                .Select(s =>
                {
                    List<LrcWord>? words = null;
                    if (s.Words is { Count: > 0 })
                    {
                        words = s.Words
                            .Where(w => !string.IsNullOrWhiteSpace(w.Word))
                            .Select(w => new LrcWord(w.Start, w.End, w.Word.Trim()))
                            .ToList();
                    }
                    return new LrcLine(s.Start, s.Text.Trim(), words);
                })
                .ToList();

            var rawLrc = BuildLrc(lines);
            logger.LogInformation("Whisper transcription complete: {Count} lines", lines.Count);
            return (rawLrc, lines);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error during Whisper transcription");
            return null;
        }
    }

    private static string BuildLrc(List<LrcLine> lines)
    {
        var sb = new StringBuilder();
        foreach (var line in lines)
        {
            var minutes = (int)(line.TimestampSeconds / 60);
            var seconds = line.TimestampSeconds % 60;
            sb.AppendLine($"[{minutes:D2}:{seconds:00.00}] {line.Text}");
        }
        return sb.ToString();
    }

    private async Task<bool> RunProcess(string args, CancellationToken ct)
    {
        var parts    = _whisperPath.Split(' ', 2);
        var fileName = parts[0];
        var fullArgs = parts.Length > 1 ? $"{parts[1]} {args}" : args;

        var psi = new ProcessStartInfo
        {
            FileName               = fileName,
            Arguments              = fullArgs,
            RedirectStandardError  = true,
            RedirectStandardOutput = true,
            UseShellExecute        = false,
            CreateNoWindow         = true,
        };

        using var process = new Process { StartInfo = psi };
        process.Start();

        var stderr = await process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0 && !string.IsNullOrWhiteSpace(stderr))
            logger.LogWarning("Whisper stderr: {Stderr}", stderr);

        return process.ExitCode == 0;
    }

    private class WhisperResult
    {
        [JsonPropertyName("segments")] public List<WhisperSegment>? Segments { get; set; }
    }

    private class WhisperSegment
    {
        [JsonPropertyName("start")] public double             Start { get; set; }
        [JsonPropertyName("text")]  public string             Text  { get; set; } = "";
        [JsonPropertyName("words")] public List<WhisperWord>? Words { get; set; }
    }

    private class WhisperWord
    {
        [JsonPropertyName("word")]  public string Word  { get; set; } = "";
        [JsonPropertyName("start")] public double Start { get; set; }
        [JsonPropertyName("end")]   public double End   { get; set; }
    }
}
