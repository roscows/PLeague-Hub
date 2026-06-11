using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace PLeagueHub.Api.Models;

public sealed class Post : BaseDocument
{
    [BsonElement("autorId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string AutorId { get; set; } = string.Empty;

    [BsonElement("naslov")]
    public string Naslov { get; set; } = string.Empty;

    [BsonElement("sadrzaj")]
    public string Sadrzaj { get; set; } = string.Empty;

    [BsonElement("tip")]
    public string Tip { get; set; } = "diskusija";

    [BsonElement("datumKreiranja")]
    public DateTime DatumKreiranja { get; set; } = DateTime.UtcNow;

    [BsonElement("obrisan")]
    public bool Obrisan { get; set; }
}
