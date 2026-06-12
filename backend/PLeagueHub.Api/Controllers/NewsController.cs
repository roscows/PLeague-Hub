using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PLeagueHub.Api.Models;
using PLeagueHub.Api.Repositories;
using PLeagueHub.Api.Requests;

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

    [Authorize(Roles = "administrator")]
    [HttpPost]
    [ProducesResponseType(typeof(Post), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<Post>> CreateNewsAsync(
        CreateNewsRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Naslov) || string.IsNullOrWhiteSpace(request.Sadrzaj))
        {
            return BadRequest(new { message = "Naslov i sadrzaj su obavezni." });
        }

        var authorId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(authorId))
        {
            return Unauthorized();
        }

        var post = new Post
        {
            AutorId = authorId,
            Naslov = request.Naslov.Trim(),
            Sadrzaj = request.Sadrzaj.Trim(),
            Tip = "vest",
            DatumKreiranja = DateTime.UtcNow,
            Obrisan = false
        };

        var createdPost = await _postsRepository.CreateAsync(post, cancellationToken);
        return Created($"/api/news/{createdPost.Id}", createdPost);
    }

    [Authorize(Roles = "administrator")]
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteNewsAsync(
        string id,
        CancellationToken cancellationToken)
    {
        var post = await _postsRepository.GetByIdAsync(id, cancellationToken);

        if (post is null
            || post.Obrisan
            || !string.Equals(post.Tip, "vest", StringComparison.OrdinalIgnoreCase))
        {
            return NotFound();
        }

        post.Obrisan = true;
        var updated = await _postsRepository.UpdateAsync(id, post, cancellationToken);

        if (!updated)
        {
            return NotFound();
        }

        return NoContent();
    }
}
