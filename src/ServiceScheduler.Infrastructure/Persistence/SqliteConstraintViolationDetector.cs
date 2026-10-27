using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ServiceScheduler.Application.Abstractions;

namespace ServiceScheduler.Infrastructure.Persistence;

internal sealed class SqliteConstraintViolationDetector : IConstraintViolationDetector
{
    // https://www.sqlite.org/rescode.html
    private const int SqliteConstraint = 19;
    private const int SqliteConstraintPrimaryKey = 1555;
    private const int SqliteConstraintUnique = 2067;

    public bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is SqliteException { SqliteErrorCode: SqliteConstraint } sqlite
        && sqlite.SqliteExtendedErrorCode is SqliteConstraintPrimaryKey or SqliteConstraintUnique;
}
