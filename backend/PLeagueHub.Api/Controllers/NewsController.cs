using Microsoft.AspNetCore.Mvc;
using PLeagueHub.Api.Models;
using PLeagueHub.Api.Repositories;

namespace PLeagueHub.Api.Controllers;

[ApiController]
[Route("api/news")]
public sealed class NewsController : ControllerBase
{
    private readonly IRepository<Post> _postsRepository;

    public NewsController(IRepository<Post> postsRepository)
    {
        _postsRepository = postsRepository;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<Post>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<Post>>> GetNewsAsync(
        CancellationToken cancellationToken)
    {
        var posts = await _postsRepository.GetAllAsync(cancellationToken);
        var news = posts
            .Where(post => !post.Obrisan)
            .Where(post => string.Equals(post.Tip, "vest", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(post => post.DatumKreiranja)
            .ToList();

        return Ok(news);
    }
}
