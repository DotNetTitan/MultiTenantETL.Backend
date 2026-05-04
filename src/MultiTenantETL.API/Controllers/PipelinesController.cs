using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MultiTenantETL.Application.Common.Interfaces;
using MultiTenantETL.Application.Executions;
using MultiTenantETL.Application.Executions.Models;
using MultiTenantETL.Application.Pipelines;
using MultiTenantETL.Application.Pipelines.Models;
using MultiTenantETL.Domain.Constants;
using MultiTenantETL.Infrastructure.Authorization.Requirements;

namespace MultiTenantETL.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PipelinesController : ControllerBase
{
    private readonly IPipelineService _pipelineService;
    private readonly IExecutionService _executionService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuthorizationService _authorizationService;
    private readonly ILogger<PipelinesController> _logger;

    public PipelinesController(
        IPipelineService pipelineService,
        IExecutionService executionService,
        ICurrentUserService currentUserService,
        IAuthorizationService authorizationService,
        ILogger<PipelinesController> logger)
    {
        _pipelineService = pipelineService;
        _executionService = executionService;
        _currentUserService = currentUserService;
        _authorizationService = authorizationService;
        _logger = logger;
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

        var result = await _pipelineService.GetAllAsync(request);
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

        var pipeline = await _pipelineService.GetByIdAsync(id);
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

        var pipeline = await _pipelineService.CreateAsync(request);
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

        var pipeline = await _pipelineService.UpdateAsync(id, request);
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

        await _pipelineService.DeleteAsync(id);
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

        var pipeline = await _pipelineService.ToggleStatusAsync(id);
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

        var userId = _currentUserService.GetUserId();
        var execution = await _executionService.StartExecutionAsync(id, "Manual", userId);

        return Ok(execution);
    }
}
