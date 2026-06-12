using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PLeagueHub.Api.Models;
using PLeagueHub.Api.Repositories;
using PLeagueHub.Api.Requests;

namespace PLeagueHub.Api.Controllers;

[ApiController]
[Route("api/forum")]
public sealed class ForumController : ControllerBase
{
    private readonly IRepository<Comment> _commentsRepository;
    private readonly IRepository<Post> _postsRepository;

    public ForumController(
        IRepository<Post> postsRepository,
        IRepository<Comment> commentsRepository)
    {
        _postsRepository = postsRepository;
        _commentsRepository = commentsRepository;
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

    [Authorize]
    [HttpPost]
    [ProducesResponseType(typeof(Post), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<Post>> CreateDiscussionAsync(
        CreateForumPostRequest request,
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
            Tip = "diskusija",
            DatumKreiranja = DateTime.UtcNow,
            Obrisan = false
        };

        var createdPost = await _postsRepository.CreateAsync(post, cancellationToken);
        return Created($"/api/forum/{createdPost.Id}", createdPost);
    }

    [HttpGet("{id}/comments")]
    [ProducesResponseType(typeof(IReadOnlyCollection<Comment>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyCollection<Comment>>> GetCommentsAsync(
        string id,
        CancellationToken cancellationToken)
    {
        var post = await _postsRepository.GetByIdAsync(id, cancellationToken);

        if (post is null || !IsVisibleDiscussion(post))
        {
            return NotFound();
        }

        var comments = await _commentsRepository.GetAllAsync(cancellationToken);
        var visibleComments = comments
            .Where(comment => comment.PostId == id)
            .Where(comment => !comment.Obrisan)
            .OrderBy(comment => comment.DatumKreiranja)
            .ToList();

        return Ok(visibleComments);
    }

    [Authorize]
    [HttpPost("{id}/comments")]
    [ProducesResponseType(typeof(Comment), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Comment>> CreateCommentAsync(
        string id,
        CreateCommentRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Tekst))
        {
            return BadRequest(new { message = "Tekst komentara je obavezan." });
        }

        var post = await _postsRepository.GetByIdAsync(id, cancellationToken);

        if (post is null || !IsVisibleDiscussion(post))
        {
            return NotFound();
        }

        var authorId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(authorId))
        {
            return Unauthorized();
        }

        var comment = new Comment
        {
            PostId = id,
            AutorId = authorId,
            Tekst = request.Tekst.Trim(),
            DatumKreiranja = DateTime.UtcNow,
            Obrisan = false
        };

        var createdComment = await _commentsRepository.CreateAsync(comment, cancellationToken);
        return Created($"/api/forum/{id}/comments/{createdComment.Id}", createdComment);
    }

    private static bool IsVisibleDiscussion(Post post)
    {
        return !post.Obrisan
            && string.Equals(post.Tip, "diskusija", StringComparison.OrdinalIgnoreCase);
    }
}
