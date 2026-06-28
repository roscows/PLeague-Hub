using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace PLeagueHub.Api.Models;

public sealed class StaffNoticeDocument : BaseDocument
{
    [BsonElement("tekst")]
    public string Tekst { get; set; } = string.Empty;

    [BsonElement("autorId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string AutorId { get; set; } = string.Empty;

    [BsonElement("pinovano")]
    public bool Pinovano { get; set; }

    [BsonElement("pinovanoAt")]
    public DateTime? PinovanoAt { get; set; }

    [BsonElement("datumKreiranja")]
    public DateTime DatumKreiranja { get; set; } = DateTime.UtcNow;
}
