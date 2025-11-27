using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MultiTenantETL.Application.Common.Models;
using MultiTenantETL.Application.Pipelines;
using MultiTenantETL.Application.Pipelines.Models;
using MultiTenantETL.Domain.Constants;
using MultiTenantETL.Domain.Enums;
using MultiTenantETL.Infrastructure.Authorization.Requirements;

namespace MultiTenantETL.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PipelinesController : ControllerBase
{
    private readonly IPipelineService _pipelineService;
    private readonly IAuthorizationService _authorizationService;
    private readonly ILogger<PipelinesController> _logger;

    public PipelinesController(
        IPipelineService pipelineService,
        IAuthorizationService authorizationService,
        ILogger<PipelinesController> logger)
    {
        _pipelineService = pipelineService;
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

            var result = await _pipelineService.GetAllAsync(request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving pipelines");
            return StatusCode(500, new ErrorResponse(
                AuthErrorCode.InternalError,
                "An error occurred while retrieving pipelines",
                new[] { ex.Message }
            ));
        }
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

            var pipeline = await _pipelineService.GetByIdAsync(id);
            return Ok(pipeline);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ErrorResponse(
                AuthErrorCode.UserNotFound,
                ex.Message
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving pipeline {PipelineId}", id);
            return StatusCode(500, new ErrorResponse(
                AuthErrorCode.InternalError,
                "An error occurred while retrieving the pipeline",
                new[] { ex.Message }
            ));
        }
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
        try
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
        catch (KeyNotFoundException ex)
        {
            return BadRequest(new ErrorResponse(
                AuthErrorCode.ValidationError,
                ex.Message
            ));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ErrorResponse(
                AuthErrorCode.ValidationError,
                ex.Message
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating pipeline");
            return StatusCode(500, new ErrorResponse(
                AuthErrorCode.InternalError,
                "An error occurred while creating the pipeline",
                new[] { ex.Message }
            ));
        }
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
        try
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
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ErrorResponse(
                AuthErrorCode.UserNotFound,
                ex.Message
            ));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ErrorResponse(
                AuthErrorCode.ValidationError,
                ex.Message
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating pipeline {PipelineId}", id);
            return StatusCode(500, new ErrorResponse(
                AuthErrorCode.InternalError,
                "An error occurred while updating the pipeline",
                new[] { ex.Message }
            ));
        }
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
        try
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
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ErrorResponse(
                AuthErrorCode.UserNotFound,
                ex.Message
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting pipeline {PipelineId}", id);
            return StatusCode(500, new ErrorResponse(
                AuthErrorCode.InternalError,
                "An error occurred while deleting the pipeline",
                new[] { ex.Message }
            ));
        }
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
        try
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
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ErrorResponse(
                AuthErrorCode.UserNotFound,
                ex.Message
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling pipeline status {PipelineId}", id);
            return StatusCode(500, new ErrorResponse(
                AuthErrorCode.InternalError,
                "An error occurred while toggling the pipeline status",
                new[] { ex.Message }
            ));
        }
    }
}
