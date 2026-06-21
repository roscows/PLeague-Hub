using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace PLeagueHub.Api.Models;

public sealed class EditorialAuditEvent : BaseDocument
{
    [BsonElement("actorId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string ActorId { get; set; } = string.Empty;

    [BsonElement("targetType")]
    public string TargetType { get; set; } = string.Empty;

    [BsonElement("targetId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string TargetId { get; set; } = string.Empty;

    [BsonElement("akcija")]
    public string Akcija { get; set; } = string.Empty;

    [BsonElement("staro")]
    public BsonDocument? Staro { get; set; }

    [BsonElement("novo")]
    public BsonDocument? Novo { get; set; }

    [BsonElement("datum")]
    public DateTime Datum { get; set; } = DateTime.UtcNow;
}
