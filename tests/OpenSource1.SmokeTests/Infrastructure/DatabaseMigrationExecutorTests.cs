using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OpenSource1.Infrastructure.Data;

namespace OpenSource1.SmokeTests.Infrastructure;

public sealed class DatabaseMigrationExecutorTests
{
    [Fact]
    public void IsPendingModelChangesException_ReturnsTrue_ForExpectedMessage()
    {
        var exception = new InvalidOperationException(
            "An error was generated for warning 'Microsoft.EntityFrameworkCore.Migrations.PendingModelChangesWarning': pending changes.");

        Assert.True(DatabaseMigrationExecutor.IsPendingModelChangesException(exception));
    }

    [Fact]
    public void IsPendingModelChangesException_ReturnsFalse_ForOtherInvalidOperation()
    {
        var exception = new InvalidOperationException("Other failure");

        Assert.False(DatabaseMigrationExecutor.IsPendingModelChangesException(exception));
    }
}
