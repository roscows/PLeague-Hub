namespace PLeagueHub.Api.Configuration;

public sealed class MongoDbSettings
{
    public string ConnectionString { get; init; } = string.Empty;

    public string DatabaseName { get; init; } = "PLeagueHub";

    public string TeamsCollectionName { get; init; } = "Teams";

    public string PlayersCollectionName { get; init; } = "Players";

    public string MatchesCollectionName { get; init; } = "Matches";

    public string StatisticsCollectionName { get; init; } = "Statistics";

    public string UsersCollectionName { get; init; } = "Users";

    public string PostsCollectionName { get; init; } = "Posts";

    public string CommentsCollectionName { get; init; } = "Comments";

    public string CommentVotesCollectionName { get; init; } = "CommentVotes";

    public string ModerationActionsCollectionName { get; init; } = "ModerationActions";

    public string NewsSourcesCollectionName { get; init; } = "NewsSources";

    public string EditorialAuditEventsCollectionName { get; init; } = "EditorialAuditEvents";
}
