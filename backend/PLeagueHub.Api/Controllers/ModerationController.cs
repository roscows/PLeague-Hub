using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PLeagueHub.Api.Models;
using PLeagueHub.Api.Repositories;

namespace PLeagueHub.Api.Controllers;

[ApiController]
[Authorize(Roles = "moderator,administrator")]
[Route("api/moderation")]
public sealed class ModerationController : ControllerBase
{
    private readonly IRepository<Post> _postsRepository;
    private readonly IRepository<User> _usersRepository;

    public ModerationController(
        IRepository<Post> postsRepository,
        IRepository<User> usersRepository)
    {
        _postsRepository = postsRepository;
        _usersRepository = usersRepository;
    }

    [HttpDelete("posts/{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeletePostAsync(
        string id,
        CancellationToken cancellationToken)
    {
        var post = await _postsRepository.GetByIdAsync(id, cancellationToken);

        if (post is null || post.Obrisan)
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

    [HttpPut("users/{id}/suspend")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SuspendUserAsync(
        string id,
        CancellationToken cancellationToken)
    {
        var user = await _usersRepository.GetByIdAsync(id, cancellationToken);

        if (user is null)
        {
            return NotFound();
        }

        user.Aktivan = false;
        var updated = await _usersRepository.UpdateAsync(id, user, cancellationToken);

        if (!updated)
        {
            return NotFound();
        }

        return NoContent();
    }
}
