using FluentAssertions;
using MultiTenantETL.Application.Executions.Models;
using MultiTenantETL.Application.Executions.Notifications;
using NSubstitute;

namespace MultiTenantETL.UnitTests.Hubs;

public class SignalRExecutionNotificationServiceTests
{
    [Fact]
    public async Task NotifyExecutionStatusChangedAsync_WithValidData_ShouldNotThrow()
    {
        // Arrange
        var notificationService = Substitute.For<IExecutionNotificationService>();
        var tenantId = Guid.NewGuid();
        var executionId = Guid.NewGuid();
        var update = new ExecutionStatusUpdate
        {
            ExecutionId = executionId,
            Status = "Running"
        };

        // Act
        var act = async () => await notificationService.NotifyExecutionStatusChangedAsync(tenantId, update);

        // Assert
        await act.Should().NotThrowAsync();
        await notificationService.Received(1).NotifyExecutionStatusChangedAsync(tenantId, update, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotifyExecutionProgressAsync_WithValidData_ShouldNotThrow()
    {
        // Arrange
        var notificationService = Substitute.For<IExecutionNotificationService>();
        var tenantId = Guid.NewGuid();
        var executionId = Guid.NewGuid();
        var update = new ExecutionProgressUpdate
        {
            ExecutionId = executionId,
            RecordsProcessed = 100,
            RecordsSucceeded = 95,
            RecordsFailed = 5,
            ProgressPercent = 50,
            BatchCount = 1
        };

        // Act
        var act = async () => await notificationService.NotifyExecutionProgressAsync(tenantId, update);

        // Assert
        await act.Should().NotThrowAsync();
        await notificationService.Received(1).NotifyExecutionProgressAsync(tenantId, update, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NotifyExecutionLogAddedAsync_WithValidData_ShouldNotThrow()
    {
        // Arrange
        var notificationService = Substitute.For<IExecutionNotificationService>();
        var tenantId = Guid.NewGuid();
        var executionId = Guid.NewGuid();
        var logEntry = new ExecutionLogDto
        {
            Timestamp = DateTimeOffset.UtcNow,
            Level = "Info",
            Source = "Test",
            Message = "Test log message"
        };

        // Act
        var act = async () => await notificationService.NotifyExecutionLogAddedAsync(tenantId, executionId, logEntry);

        // Assert
        await act.Should().NotThrowAsync();
        await notificationService.Received(1).NotifyExecutionLogAddedAsync(tenantId, executionId, logEntry, Arg.Any<CancellationToken>());
    }
}
