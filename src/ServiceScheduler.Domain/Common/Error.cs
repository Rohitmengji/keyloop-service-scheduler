namespace ServiceScheduler.Domain.Common;

/// <summary>How callers should interpret a failure; the API maps each kind to exactly one HTTP status.</summary>
public enum ErrorKind
{
    Validation,
    NotFound,
    BusinessRule,
    Conflict,
}

/// <summary>An expected failure with a stable, machine-readable <see cref="Code"/>.</summary>
public sealed record Error(ErrorKind Kind, string Code, string Message);
