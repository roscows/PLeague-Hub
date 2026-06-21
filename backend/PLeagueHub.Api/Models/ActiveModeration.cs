using MongoDB.Bson.Serialization.Attributes;

namespace PLeagueHub.Api.Models;

public sealed class ActiveModeration
{
    [BsonElement("tip")]
    public string Tip { get; set; } = string.Empty;

    [BsonElement("razlog")]
    public string Razlog { get; set; } = string.Empty;

    [BsonElement("pocetak")]
    public DateTime Pocetak { get; set; }

    [BsonElement("isticeAt")]
    public DateTime? IsticeAt { get; set; }

    [BsonElement("moderatorId")]
    public string ModeratorId { get; set; } = string.Empty;
}
