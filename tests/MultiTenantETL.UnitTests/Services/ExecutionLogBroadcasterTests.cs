using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Executions;
using MultiTenantETL.Infrastructure.Hubs;
using MultiTenantETL.Infrastructure.Services;
using NSubstitute;

namespace MultiTenantETL.UnitTests.Services;

public class ExecutionLogBroadcasterTests
{
    private readonly IHubContext<PipelineExecutionHub> _hubContext;
    private readonly ILogger<ExecutionLogBroadcaster> _logger;
    private readonly IHubClients _hubClients;
    private readonly IClientProxy _clientProxy;
    private readonly ExecutionLogBroadcaster _sut;

    public ExecutionLogBroadcasterTests()
    {
        _hubContext = Substitute.For<IHubContext<PipelineExecutionHub>>();
        _logger = Substitute.For<ILogger<ExecutionLogBroadcaster>>();
        _hubClients = Substitute.For<IHubClients>();
        _clientProxy = Substitute.For<IClientProxy>();

        _hubContext.Clients.Returns(_hubClients);
        _hubClients.Group(Arg.Any<string>()).Returns(_clientProxy);

        _sut = new ExecutionLogBroadcaster(_hubContext, _logger);
    }

    [Fact]
    public async Task BroadcastLogEntryAsync_ShouldCallSignalRWithCorrectParameters()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var logEntry = new ExecutionLogBroadcastDto
        {
            Id = Guid.NewGuid(),
            ExecutionId = executionId,
            Timestamp = DateTimeOffset.UtcNow,
            Level = "Info",
            Source = "Test",
            Message = "Test message"
        };

        // Act
        await _sut.BroadcastLogEntryAsync(executionId, logEntry);

        // Assert
        await _clientProxy.Received(1).SendCoreAsync(
            "ReceiveLogEntry",
            Arg.Is<object[]>(args => args.Length == 1 && args[0] == logEntry),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BroadcastExecutionStatusAsync_ShouldCallSignalRWithCorrectParameters()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var status = new ExecutionStatusBroadcastDto
        {
            ExecutionId = executionId,
            Status = "Running",
            RecordsProcessed = 100,
            RecordsSucceeded = 95,
            RecordsFailed = 5,
            ProgressPercent = 50.5m,
            BatchCount = 2
        };

        // Act
        await _sut.BroadcastExecutionStatusAsync(executionId, status);

        // Assert
        await _clientProxy.Received(1).SendCoreAsync(
            "ReceiveExecutionStatus",
            Arg.Is<object[]>(args => args.Length == 1 && args[0] == status),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BroadcastLogEntryAsync_WhenExceptionThrown_ShouldNotThrow()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var logEntry = new ExecutionLogBroadcastDto
        {
            Id = Guid.NewGuid(),
            ExecutionId = executionId,
            Timestamp = DateTimeOffset.UtcNow,
            Level = "Error",
            Source = "Test",
            Message = "Test error"
        };

        _clientProxy.SendCoreAsync(
            Arg.Any<string>(),
            Arg.Any<object[]>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new Exception("SignalR error")));

        // Act
        Func<Task> act = async () => await _sut.BroadcastLogEntryAsync(executionId, logEntry);

        // Assert
        await act.Should().NotThrowAsync();
        _logger.Received().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task BroadcastExecutionStatusAsync_WhenExceptionThrown_ShouldNotThrow()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var status = new ExecutionStatusBroadcastDto
        {
            ExecutionId = executionId,
            Status = "Failed",
            RecordsProcessed = 50,
            RecordsSucceeded = 40,
            RecordsFailed = 10,
            ProgressPercent = 100m,
            BatchCount = 1
        };

        _clientProxy.SendCoreAsync(
            Arg.Any<string>(),
            Arg.Any<object[]>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new Exception("SignalR error")));

        // Act
        Func<Task> act = async () => await _sut.BroadcastExecutionStatusAsync(executionId, status);

        // Assert
        await act.Should().NotThrowAsync();
        _logger.Received().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task BroadcastLogEntryAsync_ShouldUseCorrectGroupName()
    {
        // Arrange
        var executionId = Guid.NewGuid();
        var expectedGroupName = $"execution_{executionId}";
        var logEntry = new ExecutionLogBroadcastDto
        {
            Id = Guid.NewGuid(),
            ExecutionId = executionId,
            Timestamp = DateTimeOffset.UtcNow,
            Level = "Info",
            Source = "Test",
            Message = "Test message"
        };

        // Act
        await _sut.BroadcastLogEntryAsync(executionId, logEntry);

        // Assert
        _hubClients.Received(1).Group(expectedGroupName);
    }
}
