using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace PLeagueHub.Api.Models;

public sealed class Comment : BaseDocument
{
    [BsonElement("postId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string PostId { get; set; } = string.Empty;

    [BsonElement("autorId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string AutorId { get; set; } = string.Empty;

    [BsonElement("parentCommentId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? ParentCommentId { get; set; }

    [BsonElement("tekst")]
    public string Tekst { get; set; } = string.Empty;

    [BsonElement("datumKreiranja")]
    public DateTime DatumKreiranja { get; set; } = DateTime.UtcNow;

    [BsonElement("obrisan")]
    public bool Obrisan { get; set; }
}
