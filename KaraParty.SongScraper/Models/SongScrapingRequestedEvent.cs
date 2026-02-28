namespace KaraParty.Contracts;

public record SongScrapingRequestedEvent(Guid SongId, string SpotifyUrl, bool CheckYoutube = true);
