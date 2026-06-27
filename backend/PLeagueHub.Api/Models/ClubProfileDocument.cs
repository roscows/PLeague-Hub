using MongoDB.Bson.Serialization.Attributes;

namespace PLeagueHub.Api.Models;

public sealed class ClubProfileDocument : BaseDocument
{
    [BsonElement("provider_id")]
    public int ProviderId { get; set; }

    [BsonElement("stadion")]
    public string Stadion { get; set; } = string.Empty;

    [BsonElement("osnovan")]
    public int Osnovan { get; set; }

    [BsonElement("trener")]
    public string Trener { get; set; } = string.Empty;

    [BsonElement("drzava")]
    public string Drzava { get; set; } = string.Empty;

    [BsonElement("roster")]
    public List<ClubRosterEntry> Roster { get; set; } = [];

    [BsonElement("fetched_at")]
    public DateTime FetchedAt { get; set; }
}

public sealed class ClubRosterEntry
{
    [BsonElement("provider_id")]
    public int ProviderId { get; set; }

    [BsonElement("ime")]
    public string Ime { get; set; } = string.Empty;

    [BsonElement("pozicija")]
    public string Pozicija { get; set; } = string.Empty;

    [BsonElement("broj")]
    public int Broj { get; set; }

    [BsonElement("drzava")]
    public string Drzava { get; set; } = string.Empty;
}
