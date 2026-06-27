using MongoDB.Bson.Serialization.Attributes;

namespace PLeagueHub.Api.Models;

public sealed class PlayerProfileDocument : BaseDocument
{
    [BsonElement("provider_id")]
    public int ProviderId { get; set; }

    [BsonElement("ime")]
    public string Ime { get; set; } = string.Empty;

    [BsonElement("pozicija")]
    public string Pozicija { get; set; } = string.Empty;

    [BsonElement("drzava")]
    public string Drzava { get; set; } = string.Empty;

    [BsonElement("visina")]
    public int Visina { get; set; }

    [BsonElement("datum_rodjenja")]
    public DateTime? DatumRodjenja { get; set; }

    [BsonElement("klub_naziv")]
    public string KlubNaziv { get; set; } = string.Empty;

    [BsonElement("klub_provider_id")]
    public int KlubProviderId { get; set; }

    [BsonElement("foto_url")]
    public string FotoUrl { get; set; } = string.Empty;

    [BsonElement("fetched_at")]
    public DateTime FetchedAt { get; set; }
}
