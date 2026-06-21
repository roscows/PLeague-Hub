using PLeagueHub.Api.Repositories;

namespace PLeagueHub.Api.Tests;

public sealed class ForumRepositoryTests
{
    [Theory]
    [InlineData(null, 0, 0, null, 1, 20)]
    [InlineData("  arsenal  ", 2, 100, "arsenal", 2, 50)]
    public void ForumQuery_Create_NormalizesSearchAndPaging(
        string? search,
        int page,
        int pageSize,
        string? expectedSearch,
        int expectedPage,
        int expectedPageSize)
    {
        var query = ForumQuery.Create(search, page, pageSize);

        Assert.Equal(expectedSearch, query.Search);
        Assert.Equal(expectedPage, query.Page);
        Assert.Equal(expectedPageSize, query.PageSize);
        Assert.Equal((expectedPage - 1) * expectedPageSize, query.Skip);
    }

    [Fact]
    public void ForumRepository_ExposesBoundedReadAndVoteOperations()
    {
        var methodNames = typeof(IForumRepository)
            .GetMethods()
            .Select(method => method.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("GetTopicsAsync", methodNames);
        Assert.Contains("GetCommentsAsync", methodNames);
        Assert.Contains("GetVotesAsync", methodNames);
        Assert.Contains("UpsertVoteAsync", methodNames);
        Assert.Contains("DeleteVoteAsync", methodNames);
    }
}
