using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace PLeagueHub.Api.Models;

public sealed class ModerationAction : BaseDocument
{
    [BsonElement("korisnikId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string KorisnikId { get; set; } = string.Empty;

    [BsonElement("moderatorId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string ModeratorId { get; set; } = string.Empty;

    [BsonElement("akcija")]
    public string Akcija { get; set; } = string.Empty;

    [BsonElement("tipMere")]
    public string? TipMere { get; set; }

    [BsonElement("razlog")]
    public string? Razlog { get; set; }

    [BsonElement("pocetak")]
    public DateTime? Pocetak { get; set; }

    [BsonElement("isticeAt")]
    public DateTime? IsticeAt { get; set; }

    [BsonElement("datum")]
    public DateTime Datum { get; set; } = DateTime.UtcNow;
}
