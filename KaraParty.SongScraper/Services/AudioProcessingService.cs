using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

namespace KaraParty.SongScraper.Services;

public class AudioProcessingService(IConfiguration config, ILogger<AudioProcessingService> logger)
{
    private readonly string _ytDlpPath  = config["Audio:YtDlpPath"]  ?? "yt-dlp";
    private readonly string _demucsPath = config["Audio:DemucsPath"] ?? "demucs";
    private readonly string _ffmpegPath = config["Audio:FfmpegPath"] ?? "ffmpeg";

    public async Task<string?> DownloadAndSeparateAsync(
        string title, string artist, double durationSeconds, string workDir, bool checkYoutube, CancellationToken ct)
    {
        try
        {
            if (checkYoutube)
            {
                // Step 1: Try to find a karaoke version on YouTube first
                logger.LogInformation("Searching for karaoke version of '{Title}' by '{Artist}'", title, artist);
                var karaokeFile = await TryDownloadKaraokeAsync(title, artist, workDir, ct);

                if (karaokeFile != null)
                {
                    logger.LogInformation("Karaoke version found for '{Title}', trimming intro/outro", title);
                    var trimmed = await TrimAudioAsync(karaokeFile, durationSeconds, workDir, ct);
                    return trimmed ?? karaokeFile;
                }

                logger.LogInformation("No karaoke version found for '{Title}', falling back to demucs", title);
            }
            else
            {
                logger.LogInformation("YouTube check skipped for '{Title}', using demucs", title);
            }

            // Step 2: Fall back to downloading audio + demucs separation

            var query     = $"{artist} - {title} official audio";
            var audioFile = Path.Combine(workDir, "audio.mp3");

            var dlArgs = $"\"ytsearch1:{query}\" -x --audio-format mp3 -o \"{workDir}/audio.%(ext)s\" --no-playlist -q";
            logger.LogInformation("Downloading audio for '{Title}' by '{Artist}'", title, artist);
            if (!await RunProcess(_ytDlpPath, dlArgs, ct))
            {
                logger.LogWarning("yt-dlp failed for '{Title}'", title);
                return null;
            }

            if (!File.Exists(audioFile))
            {
                logger.LogWarning("yt-dlp finished but audio.mp3 not found in {WorkDir}", workDir);
                return null;
            }

            // Step 3: Separate vocals with demucs
            var separatedDir = Path.Combine(workDir, "separated");
            var demucsArgs   = $"--two-stems=vocals --mp3 \"{audioFile}\" --out \"{separatedDir}\"";
            logger.LogInformation("Running demucs for '{Title}'", title);
            if (!await RunProcess(_demucsPath, demucsArgs, ct))
            {
                logger.LogWarning("demucs failed for '{Title}'", title);
                return null;
            }

            var result = Path.Combine(separatedDir, "htdemucs", "audio", "no_vocals.mp3");
            if (!File.Exists(result))
            {
                logger.LogWarning("demucs finished but no_vocals.mp3 not found at {Path}", result);
                return null;
            }

            logger.LogInformation("Audio separation complete for '{Title}'", title);
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error during audio processing for '{Title}'", title);
            return null;
        }
    }

    private async Task<string?> TryDownloadKaraokeAsync(
        string title, string artist, string workDir, CancellationToken ct)
    {
        var query      = $"{artist} {title} karaoke";
        var outputPath = Path.Combine(workDir, "karaoke.mp3");

        // --match-filter ensures we only download if the title actually contains "karaoke"
        var args = $"\"ytsearch1:{query}\" --match-filter \"title~=(?i)karaoke\" " +
                   $"-x --audio-format mp3 -o \"{workDir}/karaoke.%(ext)s\" --no-playlist -q";

        await RunProcess(_ytDlpPath, args, ct); // exit code ignored — file presence is the signal

        return File.Exists(outputPath) ? outputPath : null;
    }

    private async Task<string?> TrimAudioAsync(
        string inputPath, double durationSeconds, string workDir, CancellationToken ct)
    {
        // Detect silence at the start to find where the actual music begins
        var detectArgs = $"-i \"{inputPath}\" -af silencedetect=n=-50dB:d=0.3 -f null -";
        var (_, output) = await RunProcessWithOutput(_ffmpegPath, detectArgs, ct);

        double musicStart = 0;
        var match = Regex.Match(output, @"silence_end:\s*([\d.]+)");
        if (match.Success && double.TryParse(
                match.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var silenceEnd))
        {
            musicStart = silenceEnd;
            logger.LogInformation("Detected music start at {Start:F2}s", musicStart);
        }

        var outputPath = Path.Combine(workDir, "instrumental.mp3");
        var ss   = musicStart.ToString(CultureInfo.InvariantCulture);
        var t    = durationSeconds.ToString(CultureInfo.InvariantCulture);
        var trimArgs = $"-ss {ss} -t {t} -i \"{inputPath}\" -y \"{outputPath}\"";

        if (!await RunProcess(_ffmpegPath, trimArgs, ct))
        {
            logger.LogWarning("ffmpeg trim failed for '{Path}'", inputPath);
            return null;
        }

        return File.Exists(outputPath) ? outputPath : null;
    }

    private async Task<bool> RunProcess(string command, string args, CancellationToken ct)
    {
        var (success, _) = await RunProcessWithOutput(command, args, ct);
        return success;
    }

    private async Task<(bool success, string output)> RunProcessWithOutput(
        string command, string args, CancellationToken ct)
    {
        var parts    = command.Split(' ', 2);
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

        // Read both streams concurrently to avoid deadlocks
        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);
        await Task.WhenAll(stdoutTask, stderrTask);
        await process.WaitForExitAsync(ct);

        var stderr = stderrTask.Result;
        if (process.ExitCode != 0 && !string.IsNullOrWhiteSpace(stderr))
            logger.LogWarning("Process '{Command}' stderr: {Stderr}", command, stderr);

        return (process.ExitCode == 0, stdoutTask.Result + stderr);
    }
}
