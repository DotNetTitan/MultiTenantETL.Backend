using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MultiTenantETL.Application.Common.Interfaces;
using MultiTenantETL.Application.Connectors.Commands;
using MultiTenantETL.Application.Connectors.Models;
using MultiTenantETL.Application.Connectors.Queries;
using MultiTenantETL.Application.Interfaces;
using MultiTenantETL.Domain.Constants;
using MultiTenantETL.Infrastructure.Authorization.Requirements;
using Wolverine;

namespace MultiTenantETL.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ConnectorsController : ControllerBase
{
    private readonly IMessageBus _bus;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuditService _auditService;
    private readonly IAuthorizationService _authorizationService;

    public ConnectorsController(
        IMessageBus bus,
        ICurrentUserService currentUserService,
        IAuditService auditService,
        IAuthorizationService authorizationService)
    {
        _bus = bus;
        _currentUserService = currentUserService;
        _auditService = auditService;
        _authorizationService = authorizationService;
    }

    /// <summary>
    /// Get all connectors for the current tenant
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<ConnectorListResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAll()
    {
        var authResult = await _authorizationService.AuthorizeAsync(
            User, 
            null, 
            new PermissionRequirement(Permissions.Connectors.Read));
        
        if (!authResult.Succeeded)
        {
            return Forbid();
        }

        var tenantId = _currentUserService.GetTenantId();
        var query = new GetAllConnectorsQuery(tenantId);
        var connectors = await _bus.InvokeAsync<List<ConnectorListResponse>>(query);
        return Ok(connectors);
    }

    /// <summary>
    /// Search connectors with filters and pagination
    /// </summary>
    [HttpPost("search")]
    [ProducesResponseType(typeof(PagedConnectorResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Search([FromBody] ConnectorSearchRequest request)
    {
        var authResult = await _authorizationService.AuthorizeAsync(
            User, 
            null, 
            new PermissionRequirement(Permissions.Connectors.Read));
        
        if (!authResult.Succeeded)
        {
            return Forbid();
        }

        var tenantId = _currentUserService.GetTenantId();
        var query = new SearchConnectorsQuery(
            request.Name,
            request.Type,
            request.Provider,
            request.Direction,
            request.IsActive,
            request.Page,
            request.PageSize,
            tenantId);
        
        var result = await _bus.InvokeAsync<PagedConnectorResponse>(query);
        return Ok(result);
    }

    /// <summary>
    /// Get connector by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ConnectorResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var authResult = await _authorizationService.AuthorizeAsync(
            User, 
            null, 
            new PermissionRequirement(Permissions.Connectors.Read));
        
        if (!authResult.Succeeded)
        {
            return Forbid();
        }

        var tenantId = _currentUserService.GetTenantId();
        var query = new GetConnectorByIdQuery(id, tenantId);
        var connector = await _bus.InvokeAsync<ConnectorResponse>(query);
        
        // Audit log for viewing sensitive connector details
        await _auditService.LogAsync(
            action: AuditActions.ConnectorViewed,
            resourceType: "Connector",
            resourceId: id.ToString(),
            description: $"Viewed connector '{connector.Name}'"
        );
        
        return Ok(connector);
    }

    /// <summary>
    /// Create a new connector
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ConnectorResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create([FromBody] CreateConnectorRequest request)
    {
        var authResult = await _authorizationService.AuthorizeAsync(
            User, 
            null, 
            new PermissionRequirement(Permissions.Connectors.Create));
        
        if (!authResult.Succeeded)
        {
            return Forbid();
        }

        var tenantId = _currentUserService.GetTenantId();
        var userId = _currentUserService.GetUserId();

        var command = new CreateConnectorCommand(
            request.Name,
            request.Description,
            request.Type,
            request.Provider,
            request.Direction,
            request.Config,
            request.Schema,
            tenantId,
            userId);

        var connector = await _bus.InvokeAsync<ConnectorResponse>(command);
        return CreatedAtAction(nameof(GetById), new { id = connector.Id }, connector);
    }

    /// <summary>
    /// Update an existing connector
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(ConnectorResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateConnectorRequest request)
    {
        var authResult = await _authorizationService.AuthorizeAsync(
            User, 
            null, 
            new PermissionRequirement(Permissions.Connectors.Update));
        
        if (!authResult.Succeeded)
        {
            return Forbid();
        }

        var tenantId = _currentUserService.GetTenantId();
        var userId = _currentUserService.GetUserId();

        var command = new UpdateConnectorCommand(
            id,
            request.Name,
            request.Description,
            request.Direction,
            request.Config,
            request.Schema,
            request.IsActive,
            tenantId,
            userId);

        var connector = await _bus.InvokeAsync<ConnectorResponse>(command);
        return Ok(connector);
    }

    /// <summary>
    /// Delete a connector
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var authResult = await _authorizationService.AuthorizeAsync(
            User, 
            null, 
            new PermissionRequirement(Permissions.Connectors.Delete));
        
        if (!authResult.Succeeded)
        {
            return Forbid();
        }

        var tenantId = _currentUserService.GetTenantId();
        var command = new DeleteConnectorCommand(id, tenantId);
        await _bus.InvokeAsync(command);
        return NoContent();
    }

    /// <summary>
    /// Test a new connection configuration
    /// </summary>
    [HttpPost("test-connection")]
    [ProducesResponseType(typeof(TestConnectionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> TestConnection([FromBody] TestConnectionRequest request)
    {
        var authResult = await _authorizationService.AuthorizeAsync(
            User, 
            null, 
            new PermissionRequirement(Permissions.Connectors.Test));
        
        if (!authResult.Succeeded)
        {
            return Forbid();
        }

        var tenantId = _currentUserService.GetTenantId();
        var command = new TestConnectionCommand(
            request.Type,
            request.Provider,
            request.Config,
            tenantId);

        var result = await _bus.InvokeAsync<TestConnectionResponse>(command);
        return Ok(result);
    }

    /// <summary>
    /// Test an existing connector's connection
    /// </summary>
    [HttpPost("{id}/test")]
    [ProducesResponseType(typeof(TestConnectionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> TestExistingConnection(Guid id)
    {
        var authResult = await _authorizationService.AuthorizeAsync(
            User, 
            null, 
            new PermissionRequirement(Permissions.Connectors.Test));
        
        if (!authResult.Succeeded)
        {
            return Forbid();
        }

        var tenantId = _currentUserService.GetTenantId();
        var command = new TestExistingConnectionCommand(id, tenantId);
        var result = await _bus.InvokeAsync<TestConnectionResponse>(command);
        return Ok(result);
    }

    /// <summary>
    /// Detect schema from a connector (for existing connectors)
    /// </summary>
    [HttpPost("detect-schema")]
    [ProducesResponseType(typeof(DetectSchemaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DetectSchema([FromBody] DetectSchemaRequest request)
    {
        var authResult = await _authorizationService.AuthorizeAsync(
            User, 
            null, 
            new PermissionRequirement(Permissions.Connectors.Update));
        
        if (!authResult.Succeeded)
        {
            return Forbid();
        }

        var tenantId = _currentUserService.GetTenantId();
        var command = new DetectSchemaCommand(
            request.ConnectorId,
            request.TableOrResourceName,
            tenantId);

        var result = await _bus.InvokeAsync<DetectSchemaResponse>(command);
        return Ok(result);
    }

    /// <summary>
    /// Detect schema from connection configuration (for new connectors before saving)
    /// </summary>
    [HttpPost("detect-schema-preview")]
    [ProducesResponseType(typeof(DetectSchemaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DetectSchemaPreview([FromBody] DetectSchemaPreviewRequest request)
    {
        var authResult = await _authorizationService.AuthorizeAsync(
            User, 
            null, 
            new PermissionRequirement(Permissions.Connectors.Create));
        
        if (!authResult.Succeeded)
        {
            return Forbid();
        }

        var tenantId = _currentUserService.GetTenantId();
        var command = new DetectSchemaPreviewCommand(
            request.Type,
            request.Provider,
            request.Config,
            request.TableOrResourceName,
            tenantId);

        var result = await _bus.InvokeAsync<DetectSchemaResponse>(command);
        return Ok(result);
    }
}
