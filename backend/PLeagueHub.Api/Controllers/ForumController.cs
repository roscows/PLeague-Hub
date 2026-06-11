using Microsoft.AspNetCore.Mvc;
using PLeagueHub.Api.Models;
using PLeagueHub.Api.Repositories;

namespace PLeagueHub.Api.Controllers;

[ApiController]
[Route("api/forum")]
public sealed class ForumController : ControllerBase
{
    private readonly IRepository<Post> _postsRepository;

    public ForumController(IRepository<Post> postsRepository)
    {
        _postsRepository = postsRepository;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<Post>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<Post>>> GetDiscussionsAsync(
        CancellationToken cancellationToken)
    {
        var posts = await _postsRepository.GetAllAsync(cancellationToken);
        var discussions = posts
            .Where(IsVisibleDiscussion)
            .OrderByDescending(post => post.DatumKreiranja)
            .ToList();

        return Ok(discussions);
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(Post), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Post>> GetDiscussionByIdAsync(
        string id,
        CancellationToken cancellationToken)
    {
        var post = await _postsRepository.GetByIdAsync(id, cancellationToken);

        if (post is null || !IsVisibleDiscussion(post))
        {
            return NotFound();
        }

        return Ok(post);
    }

    private static bool IsVisibleDiscussion(Post post)
    {
        return !post.Obrisan
            && string.Equals(post.Tip, "diskusija", StringComparison.OrdinalIgnoreCase);
    }
}
