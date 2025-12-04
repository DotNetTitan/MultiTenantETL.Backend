using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MultiTenantETL.Application.Common.Interfaces;
using MultiTenantETL.Application.Executions.Models;
using MultiTenantETL.Application.Pipelines.Commands;
using MultiTenantETL.Application.Pipelines.Models;
using MultiTenantETL.Application.Pipelines.Queries;
using MultiTenantETL.Domain.Constants;
using MultiTenantETL.Infrastructure.Authorization.Requirements;
using Wolverine;

namespace MultiTenantETL.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PipelinesController : ControllerBase
{
    private readonly IMessageBus _bus;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuthorizationService _authorizationService;

    public PipelinesController(
        IMessageBus bus,
        ICurrentUserService currentUserService,
        IAuthorizationService authorizationService)
    {
        _bus = bus;
        _currentUserService = currentUserService;
        _authorizationService = authorizationService;
    }

    /// <summary>
    /// Get all pipelines with optional filters and pagination
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedPipelineResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAll([FromQuery] PipelineSearchRequest request)
    {
        var authResult = await _authorizationService.AuthorizeAsync(
            User,
            null,
            new PermissionRequirement(Permissions.Pipelines.Read));

        if (!authResult.Succeeded)
        {
            return Forbid();
        }

        var tenantId = _currentUserService.GetTenantId();
        var query = new SearchPipelinesQuery(
            request.Name,
            request.Status,
            request.Search,
            request.IsScheduled,
            request.IsActive,
            request.SortBy,
            request.Page,
            request.PageSize,
            tenantId);

        var result = await _bus.InvokeAsync<PagedPipelineResponse>(query);
        return Ok(result);
    }

    /// <summary>
    /// Get pipeline by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(PipelineResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var authResult = await _authorizationService.AuthorizeAsync(
            User,
            null,
            new PermissionRequirement(Permissions.Pipelines.Read));

        if (!authResult.Succeeded)
        {
            return Forbid();
        }

        var tenantId = _currentUserService.GetTenantId();
        var query = new GetPipelineByIdQuery(id, tenantId);
        var pipeline = await _bus.InvokeAsync<PipelineResponse>(query);
        return Ok(pipeline);
    }

    /// <summary>
    /// Create a new pipeline
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(PipelineResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create([FromBody] CreatePipelineRequest request)
    {
        var authResult = await _authorizationService.AuthorizeAsync(
            User,
            null,
            new PermissionRequirement(Permissions.Pipelines.Create));

        if (!authResult.Succeeded)
        {
            return Forbid();
        }

        var tenantId = _currentUserService.GetTenantId();
        var userId = _currentUserService.GetUserId();

        var command = new CreatePipelineCommand(
            request.Name,
            request.Description,
            request.SourceConnectorId,
            request.DestinationConnectorId,
            request.FieldMappings,
            request.Schedule,
            request.IsScheduled,
            tenantId,
            userId);

        var pipeline = await _bus.InvokeAsync<PipelineResponse>(command);
        return CreatedAtAction(nameof(GetById), new { id = pipeline.Id }, pipeline);
    }

    /// <summary>
    /// Update an existing pipeline
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(PipelineResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePipelineRequest request)
    {
        var authResult = await _authorizationService.AuthorizeAsync(
            User,
            null,
            new PermissionRequirement(Permissions.Pipelines.Update));

        if (!authResult.Succeeded)
        {
            return Forbid();
        }

        var tenantId = _currentUserService.GetTenantId();
        var userId = _currentUserService.GetUserId();

        var command = new UpdatePipelineCommand(
            id,
            request.Name,
            request.Description,
            request.FieldMappings,
            request.Schedule,
            request.IsScheduled,
            request.IsActive,
            tenantId,
            userId);

        var pipeline = await _bus.InvokeAsync<PipelineResponse>(command);
        return Ok(pipeline);
    }

    /// <summary>
    /// Delete a pipeline
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
            new PermissionRequirement(Permissions.Pipelines.Delete));

        if (!authResult.Succeeded)
        {
            return Forbid();
        }

        var tenantId = _currentUserService.GetTenantId();
        var command = new DeletePipelineCommand(id, tenantId);
        await _bus.InvokeAsync(command);
        return NoContent();
    }

    /// <summary>
    /// Toggle pipeline active status
    /// </summary>
    [HttpPost("{id}/toggle-status")]
    [ProducesResponseType(typeof(PipelineResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ToggleStatus(Guid id)
    {
        var authResult = await _authorizationService.AuthorizeAsync(
            User,
            null,
            new PermissionRequirement(Permissions.Pipelines.Update));

        if (!authResult.Succeeded)
        {
            return Forbid();
        }

        var tenantId = _currentUserService.GetTenantId();
        var userId = _currentUserService.GetUserId();
        var command = new TogglePipelineStatusCommand(id, tenantId, userId);
        var pipeline = await _bus.InvokeAsync<PipelineResponse>(command);
        return Ok(pipeline);
    }

    /// <summary>
    /// Execute a pipeline
    /// </summary>
    [HttpPost("{id}/execute")]
    [ProducesResponseType(typeof(ExecutionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Execute(Guid id)
    {
        var authResult = await _authorizationService.AuthorizeAsync(
            User,
            null,
            new PermissionRequirement(Permissions.Pipelines.Execute));

        if (!authResult.Succeeded)
        {
            return Forbid();
        }

        var tenantId = _currentUserService.GetTenantId();
        var userId = _currentUserService.GetUserId();
        var command = new ExecutePipelineCommand(id, tenantId, userId);
        var execution = await _bus.InvokeAsync<ExecutionResponse>(command);
        return Ok(execution);
    }
}
