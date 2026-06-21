using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace PLeagueHub.Api.Models;

public sealed class Post : BaseDocument
{
    [BsonElement("autorId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? AutorId { get; set; }

    [BsonElement("naslov")]
    public string Naslov { get; set; } = string.Empty;

    [BsonElement("sadrzaj")]
    public string Sadrzaj { get; set; } = string.Empty;

    [BsonElement("tip")]
    public string Tip { get; set; } = "diskusija";

    [BsonElement("datumKreiranja")]
    public DateTime DatumKreiranja { get; set; } = DateTime.UtcNow;

    [BsonElement("poslednjaAktivnost")]
    public DateTime PoslednjaAktivnost { get; set; } = DateTime.MinValue;

    [BsonElement("obrisan")]
    public bool Obrisan { get; set; }

    [BsonElement("istaknut")]
    public bool Istaknut { get; set; }

    [BsonElement("sourceId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? SourceId { get; set; }

    [BsonElement("originalUrl")]
    public string? OriginalUrl { get; set; }

    [BsonElement("externalId")]
    public string? ExternalId { get; set; }

    [BsonElement("externalAuthor")]
    public string? ExternalAuthor { get; set; }

    [BsonElement("kategorija")]
    public string? Kategorija { get; set; }

    [BsonElement("pouzdanost")]
    public string? Pouzdanost { get; set; }

    [BsonElement("fingerprint")]
    public string? Fingerprint { get; set; }

    [BsonElement("publishedAt")]
    public DateTime? PublishedAt { get; set; }

    [BsonElement("fetchedAt")]
    public DateTime? FetchedAt { get; set; }

    [BsonElement("imageUrl")]
    public string? ImageUrl { get; set; }

    [BsonElement("xEmbedUrl")]
    public string? XEmbedUrl { get; set; }

    [BsonElement("uvozAutomatski")]
    public bool UvozAutomatski { get; set; }

    [BsonElement("updatedAt")]
    public DateTime? UpdatedAt { get; set; }
}
