using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MultiTenantETL.Application.Connectors;
using MultiTenantETL.Application.Connectors.Models;
using MultiTenantETL.Application.Common.Interfaces;
using MultiTenantETL.Domain.Constants;
using System.Security.Claims;

namespace MultiTenantETL.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ConnectorsController : ControllerBase
{
    private readonly IConnectorService _connectorService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<ConnectorsController> _logger;

    public ConnectorsController(
        IConnectorService connectorService,
        ICurrentUserService currentUserService,
        ILogger<ConnectorsController> logger)
    {
        _connectorService = connectorService;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    /// <summary>
    /// Get all connectors for the current tenant
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<ConnectorListResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var tenantId = _currentUserService.GetTenantId();
        var connectors = await _connectorService.GetAllAsync(tenantId);
        return Ok(connectors);
    }

    /// <summary>
    /// Search connectors with filters and pagination
    /// </summary>
    [HttpPost("search")]
    [ProducesResponseType(typeof(PagedConnectorResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromBody] ConnectorSearchRequest request)
    {
        var tenantId = _currentUserService.GetTenantId();
        var result = await _connectorService.SearchAsync(request, tenantId);
        return Ok(result);
    }

    /// <summary>
    /// Get connector by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ConnectorResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var tenantId = _currentUserService.GetTenantId();
        try
        {
            var connector = await _connectorService.GetByIdAsync(id, tenantId);
            return Ok(connector);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = $"Connector with ID {id} not found" });
        }
    }

    /// <summary>
    /// Create a new connector
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ConnectorResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateConnectorRequest request)
    {
        var tenantId = _currentUserService.GetTenantId();
        var userId = _currentUserService.GetUserId();

        try
        {
            var connector = await _connectorService.CreateAsync(request, tenantId, userId);
            return CreatedAtAction(nameof(GetById), new { id = connector.Id }, connector);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Update an existing connector
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(ConnectorResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateConnectorRequest request)
    {
        var tenantId = _currentUserService.GetTenantId();
        var userId = _currentUserService.GetUserId();

        try
        {
            var connector = await _connectorService.UpdateAsync(id, request, tenantId, userId);
            return Ok(connector);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = $"Connector with ID {id} not found" });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Delete a connector
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var tenantId = _currentUserService.GetTenantId();
        try
        {
            await _connectorService.DeleteAsync(id, tenantId);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = $"Connector with ID {id} not found" });
        }
    }

    /// <summary>
    /// Test a new connection configuration
    /// </summary>
    [HttpPost("test-connection")]
    [ProducesResponseType(typeof(TestConnectionResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> TestConnection([FromBody] TestConnectionRequest request)
    {
        var tenantId = _currentUserService.GetTenantId();
        var result = await _connectorService.TestConnectionAsync(request, tenantId);
        return Ok(result);
    }

    /// <summary>
    /// Test an existing connector's connection
    /// </summary>
    [HttpPost("{id}/test")]
    [ProducesResponseType(typeof(TestConnectionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> TestExistingConnection(Guid id)
    {
        var tenantId = _currentUserService.GetTenantId();
        try
        {
            var result = await _connectorService.TestExistingConnectionAsync(id, tenantId);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = $"Connector with ID {id} not found" });
        }
    }

    /// <summary>
    /// Detect schema from a connector
    /// </summary>
    [HttpPost("detect-schema")]
    [ProducesResponseType(typeof(DetectSchemaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DetectSchema([FromBody] DetectSchemaRequest request)
    {
        var tenantId = _currentUserService.GetTenantId();
        try
        {
            var result = await _connectorService.DetectSchemaAsync(request, tenantId);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = $"Connector with ID {request.ConnectorId} not found" });
        }
    }
}
