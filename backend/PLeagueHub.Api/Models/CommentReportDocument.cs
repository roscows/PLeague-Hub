using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace PLeagueHub.Api.Models;

public sealed class CommentReportDocument : BaseDocument
{
    [BsonElement("komentarId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string KomentarId { get; set; } = string.Empty;

    [BsonElement("postId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string PostId { get; set; } = string.Empty;

    [BsonElement("prijavioId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string PrijavioId { get; set; } = string.Empty;

    [BsonElement("kategorija")]
    public string Kategorija { get; set; } = string.Empty;

    [BsonElement("opis")]
    public string Opis { get; set; } = string.Empty;

    [BsonElement("status")]
    public string Status { get; set; } = "na_cekanju";

    [BsonElement("datumPrijave")]
    public DateTime DatumPrijave { get; set; } = DateTime.UtcNow;

    [BsonElement("resioId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? ResioId { get; set; }

    [BsonElement("resenoAt")]
    public DateTime? ResenoAt { get; set; }

    [BsonElement("ishod")]
    public string? Ishod { get; set; }
}
