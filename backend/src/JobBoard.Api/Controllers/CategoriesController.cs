using JobBoard.Api.Common;
using JobBoard.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace JobBoard.Api.Controllers;

[Route("api/categories")]
public sealed class CategoriesController : ApiControllerBase
{
    private readonly AppDbContext _db;

    public CategoriesController(AppDbContext db) => _db = db;

    public sealed record CategoryDto(
        Guid Id,
        string Name,
        string Slug,
        Guid? ParentId,
        string? ShortDescription,
        string? Icon,
        int SortOrder);

    /// <summary>Filter chips + post-job category picker (marketplace source of truth).</summary>
    [HttpGet]
    [AllowAnonymous]
    [EnableRateLimiting("public")]
    public async Task<ActionResult<IReadOnlyList<CategoryDto>>> List(CancellationToken cancellationToken)
    {
        var categories = await _db.Categories.AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .Select(c => new CategoryDto(c.Id, c.Name, c.Slug, c.ParentId, c.ShortDescription, c.Icon, c.SortOrder))
            .ToListAsync(cancellationToken);

        return Ok(categories);
    }
}
