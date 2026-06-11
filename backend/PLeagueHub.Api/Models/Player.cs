using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace PLeagueHub.Api.Models;

public sealed class Player : BaseDocument
{
    [BsonElement("teamId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string TeamId { get; set; } = string.Empty;

    [BsonElement("ime")]
    public string Ime { get; set; } = string.Empty;

    [BsonElement("prezime")]
    public string Prezime { get; set; } = string.Empty;

    [BsonElement("pozicija")]
    public string Pozicija { get; set; } = string.Empty;

    [BsonElement("nacionalnost")]
    public string Nacionalnost { get; set; } = string.Empty;

    [BsonElement("golovi")]
    public int Golovi { get; set; }

    [BsonElement("asistencije")]
    public int Asistencije { get; set; }

    [BsonElement("ocena")]
    public double Ocena { get; set; }
}
