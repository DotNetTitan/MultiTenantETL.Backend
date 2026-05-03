using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MultiTenantETL.Application.Connectors;
using MultiTenantETL.Application.Connectors.Models;
using MultiTenantETL.Application.Common.Interfaces;
using MultiTenantETL.Application.Interfaces;
using MultiTenantETL.Domain.Constants;
using MultiTenantETL.Infrastructure.Authorization.Requirements;

namespace MultiTenantETL.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ConnectorsController : ControllerBase
{
    private readonly IConnectorService _connectorService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuthorizationService _authorizationService;
    private readonly ILogger<ConnectorsController> _logger;

    public ConnectorsController(
        IConnectorService connectorService,
        ICurrentUserService currentUserService,
        IAuthorizationService authorizationService,
        ILogger<ConnectorsController> logger)
    {
        _connectorService = connectorService;
        _currentUserService = currentUserService;
        _authorizationService = authorizationService;
        _logger = logger;
    }

    /// <summary>
    /// Get all connectors for the current tenant
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<ConnectorListResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAll()
    {
        // Check permission
        var authResult = await _authorizationService.AuthorizeAsync(
            User,
            null,
            new PermissionRequirement(Permissions.Connectors.Read));

        if (!authResult.Succeeded)
        {
            return Forbid();
        }

        var tenantId = _currentUserService.GetTenantId();
        var connectors = await _connectorService.GetAllAsync(tenantId);
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
        // Check permission
        var authResult = await _authorizationService.AuthorizeAsync(
            User,
            null,
            new PermissionRequirement(Permissions.Connectors.Read));

        if (!authResult.Succeeded)
        {
            _logger.LogWarning("Authorization failed for connectors.read");
            return Forbid();
        }

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
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetById(Guid id)
    {
        // Check permission
        var authResult = await _authorizationService.AuthorizeAsync(
            User,
            null,
            new PermissionRequirement(Permissions.Connectors.Read));

        if (!authResult.Succeeded)
        {
            return Forbid();
        }

        var tenantId = _currentUserService.GetTenantId();
        var connector = await _connectorService.GetByIdAsync(id, tenantId);

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
        // Check permission
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

        var connector = await _connectorService.CreateAsync(request, tenantId, userId);
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
        // Check permission
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

        var connector = await _connectorService.UpdateAsync(id, request, tenantId, userId);
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
        // Check permission
        var authResult = await _authorizationService.AuthorizeAsync(
            User,
            null,
            new PermissionRequirement(Permissions.Connectors.Delete));

        if (!authResult.Succeeded)
        {
            return Forbid();
        }

        var tenantId = _currentUserService.GetTenantId();
        await _connectorService.DeleteAsync(id, tenantId);
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
        // Check permission
        var authResult = await _authorizationService.AuthorizeAsync(
            User,
            null,
            new PermissionRequirement(Permissions.Connectors.Test));

        if (!authResult.Succeeded)
        {
            return Forbid();
        }

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
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> TestExistingConnection(Guid id)
    {
        // Check permission
        var authResult = await _authorizationService.AuthorizeAsync(
            User,
            null,
            new PermissionRequirement(Permissions.Connectors.Test));

        if (!authResult.Succeeded)
        {
            return Forbid();
        }

        var tenantId = _currentUserService.GetTenantId();
        var result = await _connectorService.TestExistingConnectionAsync(id, tenantId);
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
        // Check permission - requires update permission to modify schema
        var authResult = await _authorizationService.AuthorizeAsync(
            User,
            null,
            new PermissionRequirement(Permissions.Connectors.Update));

        if (!authResult.Succeeded)
        {
            return Forbid();
        }

        var tenantId = _currentUserService.GetTenantId();
        var result = await _connectorService.DetectSchemaAsync(request, tenantId);
        return Ok(result);
    }

    /// <summary>
    /// Preview the email template with the user's configuration values.
    /// Returns the exact same HTML that would be sent in the actual email.
    /// </summary>
    [HttpPost("email-preview")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult PreviewEmail([FromBody] EmailPreviewRequest request)
    {
        var html = _connectorService.GenerateEmailPreviewHtml(request);
        return Content(html, "text/html");
    }

    /// <summary>
    /// Detect schema from connection configuration (for new connectors before saving)
    /// </summary>
    [HttpPost("detect-schema-preview")]
    [ProducesResponseType(typeof(DetectSchemaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DetectSchemaPreview([FromBody] DetectSchemaPreviewRequest request)
    {
        // Check permission
        var authResult = await _authorizationService.AuthorizeAsync(
            User,
            null,
            new PermissionRequirement(Permissions.Connectors.Create));

        if (!authResult.Succeeded)
        {
            return Forbid();
        }

        var tenantId = _currentUserService.GetTenantId();
        var result = await _connectorService.DetectSchemaPreviewAsync(request, tenantId);
        return Ok(result);
    }
}
