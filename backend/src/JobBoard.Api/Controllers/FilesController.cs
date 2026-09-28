using JobBoard.Api.Common;
using JobBoard.Domain.Abstractions;
using JobBoard.Domain.Enums;
using JobBoard.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace JobBoard.Api.Controllers;

[Route("api/files")]
public sealed class FilesController : ApiControllerBase
{
    private readonly AppDbContext _db;
    private readonly IFileStorage _storage;

    public FilesController(AppDbContext db, IFileStorage storage)
    {
        _db = db;
        _storage = storage;
    }

    public sealed record FileDto(Guid Id, string FileName, string ContentType, long SizeBytes, DateTime CreatedAt);

    /// <summary>Worker: upload a resume (5 MB max; pdf/doc/docx/rtf only — validated server-side).</summary>
    [HttpPost("resume")]
    [Authorize(Roles = Roles.Worker)]
    [RequestSizeLimit(FileRules.MaxSizeBytes + 1024)]
    public async Task<ActionResult<FileDto>> UploadResume(IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            throw ApiException.BadRequest("file_required", "Choose a file to upload.");
        if (file.Length > FileRules.MaxSizeBytes)
            throw ApiException.BadRequest("file_too_large", $"Files must be {FileRules.MaxSizeBytes / (1024 * 1024)} MB or smaller.");
        if (!FileRules.IsAllowed(file.ContentType, file.FileName, out var error))
            throw ApiException.BadRequest("file_type_not_allowed", error!);

        await using var stream = file.OpenReadStream();
        var stored = await _storage.SaveAsync(stream, file.FileName, file.ContentType, FilePurpose.Resume, CurrentUserId, cancellationToken);

        // Convenience: attach to the worker profile when one already exists.
        var profile = await _db.WorkerProfiles.FirstOrDefaultAsync(p => p.UserId == CurrentUserId, cancellationToken);
        if (profile is not null)
        {
            profile.ResumeFileId = stored.Id;
            profile.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }

        return Ok(new FileDto(stored.Id, stored.OriginalFileName, stored.ContentType, stored.SizeBytes, stored.CreatedAt));
    }

    /// <summary>
    /// Download a stored file. Allowed for the owner, an admin, or an employer
    /// reviewing an application that references this resume.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> Download(Guid id, CancellationToken cancellationToken)
    {
        var file = await _db.StoredFiles.AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == id && f.DeletedAt == null, cancellationToken)
            ?? throw ApiException.NotFound("File");

        var userId = CurrentUserId;
        var isAdmin = User.IsInRole(Roles.Admin);
        var isOwner = file.OwnerUserId == userId;

        var isReviewer = false;
        if (!isOwner && !isAdmin && User.IsInRole(Roles.Employer))
        {
            isReviewer = await _db.Applications.AnyAsync(
                a => a.ResumeFileId == file.Id && a.Job!.EmployerProfile!.UserId == userId,
                cancellationToken);
        }

        if (!isOwner && !isAdmin && !isReviewer)
            throw ApiException.Forbidden();

        var stream = await _storage.ReadAsync(file, cancellationToken);
        var downloadName = Path.GetFileName(file.OriginalFileName);
        return File(stream, file.ContentType, downloadName);
    }
}
