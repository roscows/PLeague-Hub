using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace PLeagueHub.Api.Models;

public sealed class Match : BaseDocument
{
    [BsonElement("domacin_id")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string DomacinId { get; set; } = string.Empty;

    [BsonElement("gost_id")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string GostId { get; set; } = string.Empty;

    [BsonElement("datum")]
    public DateTime Datum { get; set; }

    [BsonElement("kolo")]
    public int Kolo { get; set; }

    [BsonElement("sezona")]
    public string Sezona { get; set; } = string.Empty;

    [BsonElement("gol_domacin")]
    public int? GolDomacin { get; set; }

    [BsonElement("gol_gost")]
    public int? GolGost { get; set; }

    [BsonElement("status")]
    public string Status { get; set; } = string.Empty;
}
