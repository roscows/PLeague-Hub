using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace PLeagueHub.Api.Models;

public sealed class Statistic : BaseDocument
{
    [BsonElement("matchId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string MatchId { get; set; } = string.Empty;

    [BsonElement("playerId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string PlayerId { get; set; } = string.Empty;

    [BsonElement("golovi")]
    public int Golovi { get; set; }

    [BsonElement("asistencije")]
    public int Asistencije { get; set; }

    [BsonElement("kartoni")]
    public int Kartoni { get; set; }

    [BsonElement("minutiIgre")]
    public int MinutiIgre { get; set; }

    [BsonElement("ocena")]
    public double Ocena { get; set; }
}
