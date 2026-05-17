using BuilderPlatform.API.DTOs;
using BuilderPlatform.Domain.Entities;
using BuilderPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BuilderPlatform.API.Controllers;

[ApiController]
[Route("api/products/{productId:guid}/artifacts")]
public class ArtifactsController(AppDbContext db) : ControllerBase
{
    // GET /api/products/{productId}/artifacts
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ArtifactSummaryDto>>> GetAll(Guid productId)
    {
        var exists = await db.Products.AnyAsync(p => p.Id == productId);
        if (!exists) return NotFound();

        var artifacts = await db.Artifacts
            .Where(a => a.ProductId == productId && a.Status != ArtifactStatus.Superseded)
            .OrderByDescending(a => a.GeneratedAt)
            .Select(a => new ArtifactSummaryDto(a.Id, a.Type, a.Title, a.Version, a.Status.ToString(), a.GeneratedAt))
            .ToListAsync();

        return Ok(artifacts);
    }

    // GET /api/products/{productId}/artifacts/{artifactId}
    [HttpGet("{artifactId:guid}")]
    public async Task<ActionResult<ArtifactDto>> GetById(Guid productId, Guid artifactId)
    {
        var artifact = await db.Artifacts
            .FirstOrDefaultAsync(a => a.Id == artifactId && a.ProductId == productId);

        if (artifact is null) return NotFound();

        return new ArtifactDto(
            artifact.Id, artifact.Type, artifact.Title, artifact.Content,
            artifact.Version, artifact.Status.ToString(), artifact.GeneratedAt);
    }

    // POST /api/products/{productId}/artifacts/{artifactId}/approve
    [HttpPost("{artifactId:guid}/approve")]
    public async Task<ActionResult<ArtifactDto>> Approve(Guid productId, Guid artifactId, ApproveArtifactRequest req)
    {
        var artifact = await db.Artifacts
            .FirstOrDefaultAsync(a => a.Id == artifactId && a.ProductId == productId);

        if (artifact is null) return NotFound();

        if (artifact.Status == ArtifactStatus.Superseded)
            return BadRequest(new { error = "Cannot approve a superseded artifact" });

        artifact.Status = ArtifactStatus.Approved;

        db.ActivityEvents.Add(new ActivityEvent
        {
            ProductId  = productId,
            EventType  = ActivityType.ArtifactGenerated,
            Title      = $"Artefacto aprobado: {artifact.Title}",
            Details    = req.Note,
            ArtifactId = artifact.Id,
        });

        await db.SaveChangesAsync();

        return new ArtifactDto(
            artifact.Id, artifact.Type, artifact.Title, artifact.Content,
            artifact.Version, artifact.Status.ToString(), artifact.GeneratedAt);
    }
}
