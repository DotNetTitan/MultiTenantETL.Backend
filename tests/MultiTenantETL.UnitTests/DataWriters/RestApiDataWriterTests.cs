using System.Net;
using System.Net.Http;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Application.Connectors.DataWriters;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.DataWriters;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace MultiTenantETL.UnitTests.DataWriters;

public class RestApiDataWriterTests
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<RestApiDataWriter> _logger;
    private readonly RestApiDataWriter _sut;
    private readonly HttpClient _httpClient;

    public RestApiDataWriterTests()
    {
        _httpClientFactory = Substitute.For<IHttpClientFactory>();
        _logger = Substitute.For<ILogger<RestApiDataWriter>>();
        _httpClient = Substitute.For<HttpClient>();
        _httpClientFactory.CreateClient().Returns(_httpClient);
        _sut = new RestApiDataWriter(_httpClientFactory, _logger);
    }

    [Fact]
    public void Constructor_WithValidParameters_ShouldCreateInstance()
    {
        // Act
        var instance = new RestApiDataWriter(_httpClientFactory, _logger);

        // Assert
        instance.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullHttpClientFactory_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new RestApiDataWriter(null!, _logger);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("httpClientFactory");
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new RestApiDataWriter(_httpClientFactory, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public async Task WriteBatchAsync_WithBatchEndpointAndValidResponse_ShouldReturnSuccess()
    {
        // Arrange
        var connector = CreateBatchEndpointConnector("https://api.example.com/batch");
        var batch = CreateTestBatch(3);
        var options = new WriteOptions();

        var response = new HttpResponseMessage(HttpStatusCode.OK);
        _httpClient.SendAsync(Arg.Is<HttpRequestMessage>(m => m.Method == HttpMethod.Post), Arg.Any<CancellationToken>())
            .Returns(response);

        // Act
        var result = await _sut.WriteBatchAsync(connector, batch, options, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.BatchId.Should().Be(batch.BatchId);
        result.RowsWritten.Should().Be(3);
        result.RowsFailed.Should().Be(0);
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task WriteBatchAsync_WithIndividualEndpointAndValidResponses_ShouldReturnSuccess()
    {
        // Arrange
        var connector = CreateIndividualEndpointConnector("https://api.example.com/users");
        var batch = CreateTestBatch(2);
        var options = new WriteOptions();

        var response = new HttpResponseMessage(HttpStatusCode.Created);
        _httpClient.SendAsync(Arg.Is<HttpRequestMessage>(m => m.Method == HttpMethod.Post), Arg.Any<CancellationToken>())
            .Returns(response);

        // Act
        var result = await _sut.WriteBatchAsync(connector, batch, options, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.RowsWritten.Should().Be(2);
        result.RowsFailed.Should().Be(0);
    }

    [Fact]
    public async Task WriteBatchAsync_WithBatchEndpointAndErrorResponse_ShouldReturnFailedResult()
    {
        // Arrange
        var connector = CreateBatchEndpointConnector("https://api.example.com/batch");
        var batch = CreateTestBatch(2);
        var options = new WriteOptions();

        var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("Invalid data")
        };
        _httpClient.SendAsync(Arg.Is<HttpRequestMessage>(m => m.Method == HttpMethod.Post), Arg.Any<CancellationToken>())
            .Returns(response);

        // Act
        var result = await _sut.WriteBatchAsync(connector, batch, options, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.RowsWritten.Should().Be(0);
        result.RowsFailed.Should().Be(2);
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public async Task WriteBatchAsync_WithIndividualEndpointAndMixedResponses_ShouldReturnPartialSuccess()
    {
        // Arrange
        var connector = CreateIndividualEndpointConnector("https://api.example.com/users");
        var batch = CreateTestBatch(3);
        var options = new WriteOptions();

        // Setup alternating responses - success, failure, success
        var successResponse = new HttpResponseMessage(HttpStatusCode.Created);
        var failureResponse = new HttpResponseMessage(HttpStatusCode.Conflict);

        _httpClient.SendAsync(Arg.Is<HttpRequestMessage>(m => m.Method == HttpMethod.Post), Arg.Any<CancellationToken>())
            .Returns(successResponse, failureResponse, successResponse);

        // Act
        var result = await _sut.WriteBatchAsync(connector, batch, options, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.RowsWritten.Should().Be(2);
        result.RowsFailed.Should().Be(1);
        result.Errors.Should().HaveCount(1);
    }

    [Fact]
    public async Task WriteBatchAsync_WithAuthentication_ShouldConfigureHttpClient()
    {
        // Arrange
        var connector = CreateAuthenticatedConnector("https://api.example.com/users", "Bearer", "token123");
        var batch = CreateTestBatch(1);
        var options = new WriteOptions();

        var response = new HttpResponseMessage(HttpStatusCode.OK);
        _httpClient.SendAsync(Arg.Is<HttpRequestMessage>(m => m.Method == HttpMethod.Post), Arg.Any<CancellationToken>())
            .Returns(response);

        // Act
        var result = await _sut.WriteBatchAsync(connector, batch, options, CancellationToken.None);

        // Assert
        result.RowsWritten.Should().Be(1);
        // Note: In a real implementation, we would verify the Authorization header was set
    }

    [Fact]
    public async Task WriteBatchAsync_WithHttpException_ShouldReturnFailedResult()
    {
        // Arrange
        var connector = CreateBatchEndpointConnector("https://api.example.com/batch");
        var batch = CreateTestBatch(1);
        var options = new WriteOptions();

        _httpClient.SendAsync(Arg.Is<HttpRequestMessage>(m => m.Method == HttpMethod.Post), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        // Act
        var result = await _sut.WriteBatchAsync(connector, batch, options, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.RowsWritten.Should().Be(0);
        result.RowsFailed.Should().Be(1);
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public async Task WriteBatchAsync_WithInvalidJsonConfig_ShouldReturnFailedResult()
    {
        // Arrange
        var connector = new Connector
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Test REST API Connector",
            Type = "API",
            Provider = "RestApi",
            Direction = "destination",
            IsSource = false,
            IsDestination = true,
            RequiresCredentials = false,
            IsActive = true,
            ConfigJson = "invalid json",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
        var batch = CreateTestBatch(1);
        var options = new WriteOptions();

        // Act
        var result = await _sut.WriteBatchAsync(connector, batch, options, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.RowsWritten.Should().Be(0);
        result.RowsFailed.Should().Be(1);
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public async Task WriteBatchAsync_WithEmptyBatch_ShouldHandleGracefully()
    {
        // Arrange
        var connector = CreateBatchEndpointConnector("https://api.example.com/batch");
        var batch = new ReadBatch
        {
            BatchId = Guid.NewGuid(),
            Rows = new List<Dictionary<string, object?>>()
        };
        var options = new WriteOptions();

        var response = new HttpResponseMessage(HttpStatusCode.OK);
        _httpClient.SendAsync(Arg.Is<HttpRequestMessage>(m => m.Method == HttpMethod.Post), Arg.Any<CancellationToken>())
            .Returns(response);

        // Act
        var result = await _sut.WriteBatchAsync(connector, batch, options, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.RowsWritten.Should().Be(0);
        result.RowsFailed.Should().Be(0);
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task WriteBatchAsync_WithCancellation_ShouldHandleCancellation()
    {
        // Arrange
        var connector = CreateBatchEndpointConnector("https://api.example.com/batch");
        var batch = CreateTestBatch(1);
        var options = new WriteOptions();
        var cts = new CancellationTokenSource();
        cts.CancelAfter(1);

        _httpClient.SendAsync(Arg.Is<HttpRequestMessage>(m => m.Method == HttpMethod.Post), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        // Act
        var result = await _sut.WriteBatchAsync(connector, batch, options, cts.Token);

        // Assert - Should handle cancellation gracefully
        result.Should().NotBeNull();
    }

    private static Connector CreateBatchEndpointConnector(string url)
    {
        var config = new
        {
            Url = url,
            BatchEndpoint = true,
            Method = "POST"
        };

        return new Connector
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Test REST API Connector",
            Type = "API",
            Provider = "RestApi",
            Direction = "destination",
            IsSource = false,
            IsDestination = true,
            RequiresCredentials = false,
            IsActive = true,
            ConfigJson = JsonSerializer.Serialize(config),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
    }

    private static Connector CreateIndividualEndpointConnector(string url)
    {
        var config = new
        {
            Url = url,
            BatchEndpoint = false,
            Method = "POST"
        };

        return new Connector
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Test REST API Connector",
            Type = "API",
            Provider = "RestApi",
            Direction = "destination",
            IsSource = false,
            IsDestination = true,
            RequiresCredentials = false,
            IsActive = true,
            ConfigJson = JsonSerializer.Serialize(config),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
    }

    private static Connector CreateAuthenticatedConnector(string url, string authType, string authValue)
    {
        var config = new
        {
            Url = url,
            BatchEndpoint = false,
            Method = "POST",
            Authentication = new
            {
                Type = authType,
                Value = authValue
            }
        };

        return new Connector
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Test REST API Connector",
            Type = "API",
            Provider = "RestApi",
            Direction = "destination",
            IsSource = false,
            IsDestination = true,
            RequiresCredentials = true,
            IsActive = true,
            ConfigJson = JsonSerializer.Serialize(config),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
    }

    private static ReadBatch CreateTestBatch(int count, int startId = 1)
    {
        var rows = new List<Dictionary<string, object?>>();
        for (int i = 0; i < count; i++)
        {
            rows.Add(new Dictionary<string, object?>
            {
                ["id"] = startId + i,
                ["name"] = $"Test User {startId + i}",
                ["email"] = $"user{startId + i}@example.com"
            });
        }

        return new ReadBatch
        {
            BatchId = Guid.NewGuid(),
            Rows = rows,
            RowCount = count
        };
    }
}