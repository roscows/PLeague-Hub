using MongoDB.Bson.Serialization.Attributes;

namespace PLeagueHub.Api.Models;

// Persisted FootApi-derived match detail (statistics, incidents, lineups) stored
// as JSON so the detail page works offline after the first fetch.
public sealed class MatchDetailDocument : BaseDocument
{
    [BsonElement("match_id")]
    public string MatchId { get; set; } = string.Empty;

    [BsonElement("provider_id")]
    public int ProviderId { get; set; }

    [BsonElement("detail_json")]
    public string DetailJson { get; set; } = string.Empty;

    [BsonElement("fetchedAt")]
    public DateTime FetchedAt { get; set; }

    // Shema keširanih incidenata; kad se logika obrade promeni, povecava se broj
    // pa se stari zapisi automatski ponovo povuku sa FootApi-ja pri sledecem pregledu.
    [BsonElement("schema_version")]
    public int Version { get; set; }
}
