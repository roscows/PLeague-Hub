using MongoDB.Bson.Serialization.Attributes;

namespace PLeagueHub.Api.Models;

public sealed class PlayerSeasonStatDocument : BaseDocument
{
    [BsonElement("sezona")]
    public string Sezona { get; set; } = string.Empty;

    [BsonElement("provider_id")]
    public int ProviderId { get; set; }

    [BsonElement("ime")]
    public string Ime { get; set; } = string.Empty;

    [BsonElement("team_naziv")]
    public string TeamNaziv { get; set; } = string.Empty;

    [BsonElement("team_logo")]
    public string TeamLogoUrl { get; set; } = string.Empty;

    [BsonElement("golovi")]
    public int Golovi { get; set; }

    [BsonElement("asistencije")]
    public int Asistencije { get; set; }

    [BsonElement("odigrano")]
    public int Odigrano { get; set; }
}
