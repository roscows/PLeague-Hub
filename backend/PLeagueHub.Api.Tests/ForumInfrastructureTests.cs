using MongoDB.Bson.Serialization.Attributes;
using PLeagueHub.Api.Configuration;
using PLeagueHub.Api.Models;

namespace PLeagueHub.Api.Tests;

public sealed class ForumInfrastructureTests
{
    [Fact]
    public void ForumModels_ExposeReplyHighlightAndVotePersistenceContract()
    {
        Assert.Equal(
            "parentCommentId",
            GetBsonElementName(typeof(Comment), nameof(Comment.ParentCommentId)));
        Assert.Equal(
            "istaknut",
            GetBsonElementName(typeof(Post), nameof(Post.Istaknut)));
        Assert.Equal(
            "commentId",
            GetBsonElementName(typeof(CommentVote), nameof(CommentVote.CommentId)));
        Assert.Equal(
            "userId",
            GetBsonElementName(typeof(CommentVote), nameof(CommentVote.UserId)));
        Assert.Equal(typeof(int), typeof(CommentVote).GetProperty(nameof(CommentVote.Value))!.PropertyType);
        Assert.Equal(typeof(DateTime), typeof(CommentVote).GetProperty(nameof(CommentVote.CreatedAt))!.PropertyType);
        Assert.Equal(typeof(DateTime), typeof(CommentVote).GetProperty(nameof(CommentVote.UpdatedAt))!.PropertyType);
    }

    [Fact]
    public void MongoSettings_DefaultCommentVotesCollectionName_IsCommentVotes()
    {
        var settings = new MongoDbSettings();

        Assert.Equal("CommentVotes", settings.CommentVotesCollectionName);
    }

    private static string? GetBsonElementName(Type type, string propertyName)
    {
        return type
            .GetProperty(propertyName)!
            .GetCustomAttributes(typeof(BsonElementAttribute), inherit: false)
            .Cast<BsonElementAttribute>()
            .Single()
            .ElementName;
    }
}
