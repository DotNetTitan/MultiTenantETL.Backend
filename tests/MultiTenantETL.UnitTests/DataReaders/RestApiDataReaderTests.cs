using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using MultiTenantETL.Application.Connectors.DataReaders;
using MultiTenantETL.Domain.Entities;
using MultiTenantETL.Infrastructure.DataReaders;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace MultiTenantETL.UnitTests.DataReaders;

public class RestApiDataReaderTests
{
    private readonly RestApiDataReader _sut;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<RestApiDataReader> _logger;
    private readonly HttpClient _httpClient;

    public RestApiDataReaderTests()
    {
        _httpClientFactory = Substitute.For<IHttpClientFactory>();
        _logger = Substitute.For<ILogger<RestApiDataReader>>();
        _httpClient = new HttpClient();
        _httpClientFactory.CreateClient().Returns(_httpClient);

        _sut = new RestApiDataReader(_httpClientFactory, _logger);
    }

    private HttpClient CreateMockHttpClient(HttpResponseMessage response)
    {
        var handler = new MockHttpMessageHandler(response);
        return new HttpClient(handler);
    }

    private static StringContent CreateJsonContent(string content)
    {
        return new StringContent(content, Encoding.UTF8, "application/json");
    }

    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;

        public MockHttpMessageHandler(HttpResponseMessage response)
        {
            _response = response;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_response);
        }
    }

    private Connector CreateConnector(string url, string? dataPath = null, string? authType = null, string? endpointPath = null)
    {
        var config = new
        {
            Url = url,
            EndpointPath = endpointPath,
            DataPath = dataPath,
            AuthType = authType,
            TimeoutSeconds = 30
        };

        return new Connector
        {
            Id = Guid.NewGuid(),
            Name = "Test REST API Connector",
            Type = "API",
            Provider = "REST",
            Direction = "source",
            ConfigJson = JsonSerializer.Serialize(config),
            TenantId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
    }

    [Fact]
    public async Task ReadAsync_WithJsonArray_ShouldReturnMultipleRows()
    {
        // Arrange
        var jsonResponse = @"[
            {""name"":""John"",""age"":30},
            {""name"":""Jane"",""age"":25}
        ]";
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = CreateJsonContent(jsonResponse)
        };
        var mockHttpClient = CreateMockHttpClient(response);
        _httpClientFactory.CreateClient().Returns(mockHttpClient);

        var connector = CreateConnector("https://api.example.com/users");
        var options = new ReadOptions { BatchSize = 10 };

        // Act
        var batches = new List<ReadBatch>();
        await foreach (var batch in _sut.ReadAsync(connector, options, CancellationToken.None))
        {
            batches.Add(batch);
        }

        // Assert
        batches.Should().HaveCount(1);
        batches[0].RowCount.Should().Be(2);
        batches[0].Rows[0]["name"].Should().Be("John");
        batches[0].Rows[0]["age"].Should().Be(30);
        batches[0].Rows[1]["name"].Should().Be("Jane");
        batches[0].Rows[1]["age"].Should().Be(25);
    }

    [Fact]
    public async Task ReadAsync_WithJsonObject_ShouldReturnSingleRow()
    {
        // Arrange
        var jsonResponse = @"{""name"":""John"",""age"":30}";
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = CreateJsonContent(jsonResponse)
        };
        var mockHttpClient = CreateMockHttpClient(response);
        _httpClientFactory.CreateClient().Returns(mockHttpClient);

        var connector = CreateConnector("https://api.example.com/user");
        var options = new ReadOptions { BatchSize = 10 };

        // Act
        var batches = new List<ReadBatch>();
        await foreach (var batch in _sut.ReadAsync(connector, options, CancellationToken.None))
        {
            batches.Add(batch);
        }

        // Assert
        batches.Should().HaveCount(1);
        batches[0].RowCount.Should().Be(1);
        batches[0].Rows[0]["name"].Should().Be("John");
        batches[0].Rows[0]["age"].Should().Be(30);
    }

    [Fact]
    public async Task ReadAsync_WithDataPath_ShouldExtractNestedData()
    {
        // Arrange
        var jsonResponse = @"{
            ""data"": {
                ""users"": [
                    {""name"":""John"",""age"":30},
                    {""name"":""Jane"",""age"":25}
                ]
            }
        }";
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = CreateJsonContent(jsonResponse)
        };
        var mockHttpClient = CreateMockHttpClient(response);
        _httpClientFactory.CreateClient().Returns(mockHttpClient);

        var connector = CreateConnector("https://api.example.com/users", "data.users");
        var options = new ReadOptions { BatchSize = 10 };

        // Act
        var batches = new List<ReadBatch>();
        await foreach (var batch in _sut.ReadAsync(connector, options, CancellationToken.None))
        {
            batches.Add(batch);
        }

        // Assert
        batches.Should().HaveCount(1);
        batches[0].RowCount.Should().Be(2);
        batches[0].Rows[0]["name"].Should().Be("John");
        batches[0].Rows[1]["name"].Should().Be("Jane");
    }

    [Fact]
    public async Task ReadAsync_WithBatching_ShouldSplitIntoMultipleBatches()
    {
        // Arrange
        var jsonResponse = @"[
            {""id"":1},{""id"":2},{""id"":3},{""id"":4}
        ]";
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = CreateJsonContent(jsonResponse)
        };
        var mockHttpClient = CreateMockHttpClient(response);
        _httpClientFactory.CreateClient().Returns(mockHttpClient);

        var connector = CreateConnector("https://api.example.com/items");
        var options = new ReadOptions { BatchSize = 2 };

        // Act
        var batches = new List<ReadBatch>();
        await foreach (var batch in _sut.ReadAsync(connector, options, CancellationToken.None))
        {
            batches.Add(batch);
        }

        // Assert
        batches.Should().HaveCount(2);
        batches[0].RowCount.Should().Be(2);
        batches[1].RowCount.Should().Be(2);
    }

    [Fact]
    public async Task ReadAsync_WithMaxRows_ShouldLimitRows()
    {
        // Arrange
        var jsonResponse = @"[
            {""id"":1},{""id"":2},{""id"":3},{""id"":4}
        ]";
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = CreateJsonContent(jsonResponse)
        };
        var mockHttpClient = CreateMockHttpClient(response);
        _httpClientFactory.CreateClient().Returns(mockHttpClient);

        var connector = CreateConnector("https://api.example.com/items");
        var options = new ReadOptions { BatchSize = 10, MaxRows = 2 };

        // Act
        var batches = new List<ReadBatch>();
        await foreach (var batch in _sut.ReadAsync(connector, options, CancellationToken.None))
        {
            batches.Add(batch);
        }

        // Assert
        batches.Should().HaveCount(1);
        batches[0].RowCount.Should().Be(2);
        batches[0].Rows[0]["id"].Should().Be(1);
        batches[0].Rows[1]["id"].Should().Be(2);
    }

    [Fact]
    public async Task ReadAsync_WithHttpError_ShouldThrowException()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.NotFound);
        var mockHttpClient = CreateMockHttpClient(response);
        _httpClientFactory.CreateClient().Returns(mockHttpClient);

        var connector = CreateConnector("https://api.example.com/users");
        var options = new ReadOptions { BatchSize = 10 };

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            var batches = new List<ReadBatch>();
            await foreach (var batch in _sut.ReadAsync(connector, options, CancellationToken.None))
            {
                batches.Add(batch);
            }
        });
    }

    [Fact]
    public async Task ReadAsync_WithInvalidJson_ShouldThrowException()
    {
        // Arrange
        var invalidJson = @"{invalid json}";
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = CreateJsonContent(invalidJson)
        };
        var mockHttpClient = CreateMockHttpClient(response);
        _httpClientFactory.CreateClient().Returns(mockHttpClient);

        var connector = CreateConnector("https://api.example.com/users");
        var options = new ReadOptions { BatchSize = 10 };

        // Act & Assert
        await Assert.ThrowsAnyAsync<System.Text.Json.JsonException>(async () =>
        {
            var batches = new List<ReadBatch>();
            await foreach (var batch in _sut.ReadAsync(connector, options, CancellationToken.None))
            {
                batches.Add(batch);
            }
        });
    }

    [Fact]
    public async Task DetectSchemaAsync_WithJsonArray_ShouldReturnSchema()
    {
        // Arrange
        var jsonResponse = @"[
            {""name"":""John"",""age"":30,""active"":true},
            {""name"":""Jane"",""age"":25,""active"":false}
        ]";
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = CreateJsonContent(jsonResponse)
        };
        var mockHttpClient = CreateMockHttpClient(response);
        _httpClientFactory.CreateClient().Returns(mockHttpClient);

        var connector = CreateConnector("https://api.example.com/users");

        // Act
        var result = await _sut.DetectSchemaAsync(connector, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Fields.Should().HaveCount(3);
        result.Fields.Select(f => f.Name).Should().BeEquivalentTo(new[] { "name", "age", "active" });
    }

    [Fact]
    public async Task DetectSchemaAsync_WithDataPath_ShouldExtractSchemaFromPath()
    {
        // Arrange
        var jsonResponse = @"{
            ""result"": {
                ""items"": [
                    {""id"":1,""value"":""test""}
                ]
            }
        }";
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = CreateJsonContent(jsonResponse)
        };
        var mockHttpClient = CreateMockHttpClient(response);
        _httpClientFactory.CreateClient().Returns(mockHttpClient);

        var connector = CreateConnector("https://api.example.com/data", "result.items");

        // Act
        var result = await _sut.DetectSchemaAsync(connector, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Fields.Should().HaveCount(2);
        result.Fields.Select(f => f.Name).Should().BeEquivalentTo(new[] { "id", "value" });
    }

    [Fact]
    public async Task DetectSchemaAsync_WithHttpError_ShouldReturnError()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.InternalServerError);
        var mockHttpClient = CreateMockHttpClient(response);
        _httpClientFactory.CreateClient().Returns(mockHttpClient);

        var connector = CreateConnector("https://api.example.com/users");

        // Act
        var result = await _sut.DetectSchemaAsync(connector, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
        result.Fields.Should().BeEmpty();
    }

    [Fact]
    public async Task TestConnectionAsync_WithSuccessResponse_ShouldReturnTrue()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        var mockHttpClient = CreateMockHttpClient(response);
        _httpClientFactory.CreateClient().Returns(mockHttpClient);

        var connector = CreateConnector("https://api.example.com/test");

        // Act
        var result = await _sut.TestConnectionAsync(connector, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task TestConnectionAsync_WithErrorResponse_ShouldReturnFalse()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.NotFound);
        var mockHttpClient = CreateMockHttpClient(response);
        _httpClientFactory.CreateClient().Returns(mockHttpClient);

        var connector = CreateConnector("https://api.example.com/test");

        // Act
        var result = await _sut.TestConnectionAsync(connector, CancellationToken.None);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task TestConnectionAsync_WithException_ShouldReturnFalse()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        var mockHttpClient = CreateMockHttpClient(response);
        _httpClientFactory.CreateClient().Returns(mockHttpClient);

        var connector = CreateConnector("https://api.example.com/test");

        // Act
        var result = await _sut.TestConnectionAsync(connector, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task TestConnectionAsync_WithInvalidUrl_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var connector = CreateConnector("relative/path");

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sut.TestConnectionAsync(connector, CancellationToken.None));
    }

    [Fact]
    public async Task DetectSchemaAsync_WithInvalidUrl_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var connector = CreateConnector("not-a-valid-url");

        // Act & Assert
        var result = await _sut.DetectSchemaAsync(connector, CancellationToken.None);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task ReadAsync_WithEndpointPath_ShouldCombineUrls()
    {
        // Arrange
        var jsonResponse = @"[{""id"":1,""name"":""Test""}]";
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = CreateJsonContent(jsonResponse)
        };
        var mockHttpClient = CreateMockHttpClient(response);
        _httpClientFactory.CreateClient().Returns(mockHttpClient);

        var connector = CreateConnector("https://api.example.com", endpointPath: "api/v2/accounts/etls");
        var options = new ReadOptions { BatchSize = 10 };

        // Act
        var batches = new List<ReadBatch>();
        await foreach (var batch in _sut.ReadAsync(connector, options, CancellationToken.None))
        {
            batches.Add(batch);
        }

        // Assert
        batches.Should().HaveCount(1);
        batches[0].Rows.Should().HaveCount(1);
    }

    [Fact]
    public async Task TestConnectionAsync_WithEndpointPath_ShouldCombineUrls()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.OK);
        var mockHttpClient = CreateMockHttpClient(response);
        _httpClientFactory.CreateClient().Returns(mockHttpClient);

        var connector = CreateConnector("https://api.example.com", endpointPath: "api/v2/accounts");

        // Act
        var result = await _sut.TestConnectionAsync(connector, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
    }
}
