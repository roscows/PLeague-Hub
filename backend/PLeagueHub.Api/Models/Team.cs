using MongoDB.Bson.Serialization.Attributes;

namespace PLeagueHub.Api.Models;

public sealed class Team : BaseDocument
{
    [BsonElement("provider_id")]
    [BsonIgnoreIfNull]
    public int? ProviderId { get; set; }

    [BsonElement("naziv")]
    public string Naziv { get; set; } = string.Empty;

    [BsonElement("skracenica")]
    public string Skracenica { get; set; } = string.Empty;

    [BsonElement("stadion")]
    public string Stadion { get; set; } = string.Empty;

    [BsonElement("osnovan")]
    public int Osnovan { get; set; }

    [BsonElement("logo_url")]
    public string LogoUrl { get; set; } = string.Empty;

    [BsonElement("bodovi")]
    public int Bodovi { get; set; }

    [BsonElement("pozicija")]
    public int Pozicija { get; set; }
}
