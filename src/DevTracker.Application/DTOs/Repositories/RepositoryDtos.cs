namespace DevTracker.Application.DTOs.Repositories;

// ── Requests ────────────────────────────────────────────────────────────────

public sealed record CreateRepositoryRequest(
    Guid    ProjectId,
    string  Name,
    string? Description);

public sealed record AttachRepositoryRequest(
    Guid    ProjectId,
    string  SourceAbsolutePath,
    string? Description);

// ── Responses ───────────────────────────────────────────────────────────────

public sealed record RepositorySummaryDto(
    Guid     Id,
    string   Name,
    string   RelativePath,
    string?  Description,
    DateTime CreatedAt);
