using Azure.Storage.Blobs;
using KaraParty.SongScraper.Services;
using MassTransit;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenAI;
using SpotifyAPI.Web;
using System.ClientModel;

await Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((_, cfg) =>
    {
        cfg.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
        cfg.AddUserSecrets<Program>(optional: true);
        cfg.AddEnvironmentVariables();
    })
    .ConfigureServices((ctx, services) =>
    {
        var config = ctx.Configuration;

        // ── Spotify Client (with automatic token refresh) ────────────────────
        var spotifyClientId     = config["Spotify:ClientId"]!;
        var spotifyClientSecret = config["Spotify:ClientSecret"]!;
        services.AddSingleton(_ =>
        {
            var authenticator = new ClientCredentialsAuthenticator(spotifyClientId, spotifyClientSecret);
            var spotifyConfig = SpotifyClientConfig.CreateDefault().WithAuthenticator(authenticator);
            return new SpotifyClient(spotifyConfig);
        });

        // ── AI Client ────────────────────────────────────────────────────────
        var githubToken    = config["GitHubModels:Token"]!;
        var githubEndpoint = config["GitHubModels:Endpoint"]!;
        var githubModel    = config["GitHubModels:Model"]!;
        services.AddSingleton<IChatClient>(_ =>
            new OpenAIClient(
                new ApiKeyCredential(githubToken),
                new OpenAIClientOptions { Endpoint = new Uri(githubEndpoint) })
                .AsChatClient(githubModel));

        // ── Azure Blob Storage ───────────────────────────────────────────────
        services.AddSingleton(_ =>
            new BlobServiceClient(config.GetConnectionString("BlobStorage")));

        // ── Domain Services ──────────────────────────────────────────────────
        services.AddTransient<AudioProcessingService>();
        services.AddTransient<SpotifyService>();
        services.AddHttpClient<LrcLibService>(client =>
            client.BaseAddress = new Uri("https://lrclib.net/api/"));
        services.AddTransient<AiEnrichmentService>();
        services.AddTransient<SongScraperService>();
        services.AddTransient<BlobUploadService>();
        services.AddTransient<WhisperService>();

        // ── MassTransit / RabbitMQ ───────────────────────────────────────────
        services.AddMassTransit(x =>
        {
            x.AddConsumer<SongScrapingRequestedConsumer>();

            x.UsingRabbitMq((ctx2, cfg) =>
            {
                cfg.Host(config["RabbitMQ:Host"], config["RabbitMQ:VirtualHost"] ?? "/", h =>
                {
                    h.Username(config["RabbitMQ:Username"]!);
                    h.Password(config["RabbitMQ:Password"]!);
                });
                cfg.ConfigureEndpoints(ctx2);
            });
        });
    })
    .Build()
    .RunAsync();
