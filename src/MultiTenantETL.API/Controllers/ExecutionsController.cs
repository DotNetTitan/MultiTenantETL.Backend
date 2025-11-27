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
        try
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving executions");
            return StatusCode(500, new ErrorResponse(
                AuthErrorCode.InternalError,
                "An error occurred while retrieving executions",
                new[] { ex.Message }
            ));
        }
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
        try
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
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ErrorResponse(
                AuthErrorCode.UserNotFound,
                ex.Message
            ));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving execution {ExecutionId}", id);
            return StatusCode(500, new ErrorResponse(
                AuthErrorCode.InternalError,
                "An error occurred while retrieving the execution",
                new[] { ex.Message }
            ));
        }
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
        try
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
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ErrorResponse(
                AuthErrorCode.UserNotFound,
                ex.Message
            ));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ErrorResponse(
                AuthErrorCode.ValidationError,
                ex.Message
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling execution {ExecutionId}", id);
            return StatusCode(500, new ErrorResponse(
                AuthErrorCode.InternalError,
                "An error occurred while cancelling the execution",
                new[] { ex.Message }
            ));
        }
    }

    /// <summary>
    /// Get execution statistics
    /// </summary>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(ExecutionStatsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetStats([FromQuery] Guid? pipelineId = null)
    {
        try
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving execution stats");
            return StatusCode(500, new ErrorResponse(
                AuthErrorCode.InternalError,
                "An error occurred while retrieving execution statistics",
                new[] { ex.Message }
            ));
        }
    }
}
