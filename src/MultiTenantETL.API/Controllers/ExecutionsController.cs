using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MultiTenantETL.Application.Common.Interfaces;
using MultiTenantETL.Application.Common.Models;
using MultiTenantETL.Application.Executions;
using MultiTenantETL.Application.Executions.Models;
using MultiTenantETL.Domain.Constants;
using MultiTenantETL.Domain.Enums;
using MultiTenantETL.Infrastructure.Authorization.Requirements;

namespace MultiTenantETL.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ExecutionsController : ControllerBase
{
    private readonly IExecutionService _executionService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAuthorizationService _authorizationService;
    private readonly ILogger<ExecutionsController> _logger;

    public ExecutionsController(
        IExecutionService executionService,
        ICurrentUserService currentUserService,
        IAuthorizationService authorizationService,
        ILogger<ExecutionsController> logger)
    {
        _executionService = executionService;
        _currentUserService = currentUserService;
        _authorizationService = authorizationService;
        _logger = logger;
    }

    /// <summary>
    /// Get all executions with optional filters and pagination
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedExecutionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAll([FromQuery] ExecutionSearchRequest request)
    {
        var authResult = await _authorizationService.AuthorizeAsync(
            User,
            null,
            new PermissionRequirement(Permissions.Pipelines.Read));

        if (!authResult.Succeeded)
        {
            return Forbid();
        }

        var result = await _executionService.GetAllAsync(request);
        return Ok(result);
    }

    /// <summary>
    /// Get execution by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ExecutionResponse), StatusCodes.Status200OK)]
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

        var execution = await _executionService.GetByIdAsync(id);
        return Ok(execution);
    }

    /// <summary>
    /// Cancel a running execution
    /// </summary>
    [HttpPost("{id}/cancel")]
    [ProducesResponseType(typeof(ExecutionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Cancel(Guid id)
    {
        var authResult = await _authorizationService.AuthorizeAsync(
            User,
            null,
            new PermissionRequirement(Permissions.Pipelines.Execute));

        if (!authResult.Succeeded)
        {
            return Forbid();
        }

        var execution = await _executionService.CancelExecutionAsync(id);
        return Ok(execution);
    }

    /// <summary>
    /// Get execution statistics
    /// </summary>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(ExecutionStatsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetStats([FromQuery] Guid? pipelineId = null)
    {
        var authResult = await _authorizationService.AuthorizeAsync(
            User,
            null,
            new PermissionRequirement(Permissions.Pipelines.Read));

        if (!authResult.Succeeded)
        {
            return Forbid();
        }

        var stats = await _executionService.GetStatsAsync(pipelineId);
        return Ok(stats);
    }
}
