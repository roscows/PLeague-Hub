using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using PLeagueHub.Api.Models;

namespace PLeagueHub.Api.Services.News;

public sealed partial class NewsRelevanceFilter
{
    private static readonly string[] PremierLeagueTerms =
    [
        "premier league", "english premier league", "epl",
        "arsenal", "aston villa", "afc bournemouth", "bournemouth",
        "brentford", "brighton and hove albion", "brighton", "burnley",
        "chelsea", "crystal palace", "everton", "fulham", "leeds united", "leeds",
        "liverpool", "manchester city", "man city", "manchester united", "man united", "man utd",
        "newcastle united", "newcastle", "nottingham forest", "nottm forest",
        "sunderland", "tottenham hotspur", "tottenham", "spurs",
        "west ham united", "west ham", "wolverhampton wanderers", "wolves"
    ];

    private static readonly string[] FplTerms =
    [
        "fantasy premier league", "fpl", "gameweek", "wildcard", "free hit",
        "bench boost", "triple captain", "captaincy", "price rise", "price fall",
        "bonus points system", "expected points"
    ];

    public bool IsRelevant(string text, NewsSource source)
    {
        var normalized = Normalize(text);
        if (string.IsNullOrEmpty(normalized)) return false;

        if (source.IskljuceniPojmovi.Any(term => Contains(normalized, term))) return false;
        if (source.UkljuceniPojmovi.Any(term => Contains(normalized, term))) return true;

        return FplTerms.Any(term => Contains(normalized, term))
            || PremierLeagueTerms.Any(term => Contains(normalized, term));
    }

    internal static string Normalize(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
            builder.Append(char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : ' ');
        }

        return WhitespaceRegex().Replace(builder.ToString(), " ").Trim();
    }

    private static bool Contains(string normalizedText, string term)
    {
        var normalizedTerm = Normalize(term);
        return !string.IsNullOrEmpty(normalizedTerm)
            && $" {normalizedText} ".Contains($" {normalizedTerm} ", StringComparison.Ordinal);
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
