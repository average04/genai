using KaraParty.SongScraper.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI;
using SpotifyAPI.Web;
using System.ClientModel;

// ── Configuration ────────────────────────────────────────────────────────────
var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .AddUserSecrets<Program>()
    .Build();

var githubToken    = config["GitHubModels:Token"]!;
var githubEndpoint = config["GitHubModels:Endpoint"]!;
var githubModel    = config["GitHubModels:Model"]!;
var spotifyClientId     = config["Spotify:ClientId"]!;
var spotifyClientSecret = config["Spotify:ClientSecret"]!;

// ── Spotify Client ────────────────────────────────────────────────────────────
var spotifyConfig = SpotifyClientConfig.CreateDefault();
var request       = new ClientCredentialsRequest(spotifyClientId, spotifyClientSecret);
var response      = await new OAuthClient(spotifyConfig).RequestToken(request);
var spotify       = new SpotifyClient(spotifyConfig.WithToken(response.AccessToken));

// ── GitHub Models AI Client ──────────────────────────────────────────────────
IChatClient aiClient = new OpenAIClient(
    new ApiKeyCredential(githubToken),
    new OpenAIClientOptions { Endpoint = new Uri(githubEndpoint) })
    .AsChatClient(githubModel);

// ── Services ─────────────────────────────────────────────────────────────────
var spotifyService = new SpotifyService(spotify);
var lrcService     = new LrcLibService();
var aiService      = new AiEnrichmentService(aiClient);
var scraper        = new SongScraperService(spotifyService, lrcService, aiService);
var fileOutput     = new FileOutputService();

// ── Run ───────────────────────────────────────────────────────────────────────
Console.WriteLine("KaraParty Song Scraper");
Console.WriteLine("======================");
Console.Write("Paste a Spotify track URL: ");
var url = Console.ReadLine()?.Trim();

if (string.IsNullOrEmpty(url))
{
    Console.WriteLine("No URL provided.");
    return;
}

var result = await scraper.ScrapeAsync(url);

Console.WriteLine();
Console.WriteLine($"Title        : {result.Title}");
Console.WriteLine($"Artist       : {result.Artist}");
Console.WriteLine($"Album        : {result.Album}");
Console.WriteLine($"Duration     : {result.DurationSeconds}s");
Console.WriteLine($"Language     : {result.Language}");
Console.WriteLine($"Mood         : {result.Mood}");
Console.WriteLine($"Genre        : {result.Genre}");
Console.WriteLine($"Difficulty   : {result.KaraokeDifficulty}");
Console.WriteLine($"Summary      : {result.LyricsSummary}");
Console.WriteLine($"Has LRC      : {result.HasSyncedLyrics}");
Console.WriteLine($"Pitch ref pts: {result.PitchReference?.Count ?? 0}");

await fileOutput.SaveAsync(result);
