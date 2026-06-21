using MongoDB.Bson.Serialization.Attributes;

namespace PLeagueHub.Api.Models;

public sealed class NewsSource : BaseDocument
{
    [BsonElement("naziv")]
    public string Naziv { get; set; } = string.Empty;

    [BsonElement("feedUrl")]
    public string FeedUrl { get; set; } = string.Empty;

    [BsonElement("siteUrl")]
    public string SiteUrl { get; set; } = string.Empty;

    [BsonElement("tip")]
    public string Tip { get; set; } = "rss";

    [BsonElement("podrazumevanaKategorija")]
    public string PodrazumevanaKategorija { get; set; } = "premier_league";

    [BsonElement("podrazumevanaPouzdanost")]
    public string PodrazumevanaPouzdanost { get; set; } = "pouzdan_izvor";

    [BsonElement("ukljuceniPojmovi")]
    public List<string> UkljuceniPojmovi { get; set; } = [];

    [BsonElement("iskljuceniPojmovi")]
    public List<string> IskljuceniPojmovi { get; set; } = [];

    [BsonElement("aktivan")]
    public bool Aktivan { get; set; } = true;

    [BsonElement("pauziranRazlog")]
    public string? PauziranRazlog { get; set; }

    [BsonElement("uzastopneGreske")]
    public int UzastopneGreske { get; set; }

    [BsonElement("etag")]
    public string? Etag { get; set; }

    [BsonElement("lastModified")]
    public DateTimeOffset? LastModified { get; set; }

    [BsonElement("poslednjaProveraAt")]
    public DateTime? PoslednjaProveraAt { get; set; }

    [BsonElement("poslednjiUspehAt")]
    public DateTime? PoslednjiUspehAt { get; set; }

    [BsonElement("createdBy")]
    public string CreatedBy { get; set; } = string.Empty;

    [BsonElement("updatedBy")]
    public string UpdatedBy { get; set; } = string.Empty;

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; }
}
