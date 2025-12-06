using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MultiTenantETL.Application.Common.Models;
using MultiTenantETL.Application.Scheduling;
using MultiTenantETL.Application.Scheduling.Models;
using MultiTenantETL.Domain.Constants;
using MultiTenantETL.Domain.Enums;
using MultiTenantETL.Infrastructure.Authorization.Requirements;

namespace MultiTenantETL.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SchedulesController : ControllerBase
{
    private readonly IScheduleService _scheduleService;
    private readonly IAuthorizationService _authorizationService;
    private readonly ILogger<SchedulesController> _logger;

    public SchedulesController(
        IScheduleService scheduleService,
        IAuthorizationService authorizationService,
        ILogger<SchedulesController> logger)
    {
        _scheduleService = scheduleService;
        _authorizationService = authorizationService;
        _logger = logger;
    }

    /// <summary>
    /// Get all schedules with optional filters and pagination
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedScheduleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAll([FromQuery] ScheduleSearchRequest request)
    {
        var authResult = await _authorizationService.AuthorizeAsync(
            User,
            null,
            new PermissionRequirement(Permissions.Pipelines.Read));

        if (!authResult.Succeeded)
        {
            return Forbid();
        }

        var result = await _scheduleService.GetAllAsync(request);
        return Ok(result);
    }

    /// <summary>
    /// Get schedule by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ScheduleResponse), StatusCodes.Status200OK)]
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

        var schedule = await _scheduleService.GetByIdAsync(id);
        return Ok(schedule);
    }

    /// <summary>
    /// Get schedule by pipeline ID
    /// </summary>
    [HttpGet("pipeline/{pipelineId}")]
    [ProducesResponseType(typeof(ScheduleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetByPipelineId(Guid pipelineId)
    {
        var authResult = await _authorizationService.AuthorizeAsync(
            User,
            null,
            new PermissionRequirement(Permissions.Pipelines.Read));

        if (!authResult.Succeeded)
        {
            return Forbid();
        }

        var schedule = await _scheduleService.GetByPipelineIdAsync(pipelineId);
        if (schedule == null)
        {
            return NotFound();
        }

        return Ok(schedule);
    }

    /// <summary>
    /// Create a new schedule
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ScheduleResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create([FromBody] CreateScheduleRequest request)
    {
        var authResult = await _authorizationService.AuthorizeAsync(
            User,
            null,
            new PermissionRequirement(Permissions.Pipelines.Update));

        if (!authResult.Succeeded)
        {
            return Forbid();
        }

        var schedule = await _scheduleService.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = schedule.Id }, schedule);
    }

    /// <summary>
    /// Update an existing schedule
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(ScheduleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateScheduleRequest request)
    {
        var authResult = await _authorizationService.AuthorizeAsync(
            User,
            null,
            new PermissionRequirement(Permissions.Pipelines.Update));

        if (!authResult.Succeeded)
        {
            return Forbid();
        }

        var schedule = await _scheduleService.UpdateAsync(id, request);
        return Ok(schedule);
    }

    /// <summary>
    /// Delete a schedule
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
            new PermissionRequirement(Permissions.Pipelines.Update));

        if (!authResult.Succeeded)
        {
            return Forbid();
        }

        await _scheduleService.DeleteAsync(id);
        return NoContent();
    }

    /// <summary>
    /// Enable a schedule
    /// </summary>
    [HttpPost("{id}/enable")]
    [ProducesResponseType(typeof(ScheduleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Enable(Guid id)
    {
        var authResult = await _authorizationService.AuthorizeAsync(
            User,
            null,
            new PermissionRequirement(Permissions.Pipelines.Update));

        if (!authResult.Succeeded)
        {
            return Forbid();
        }

        var schedule = await _scheduleService.EnableAsync(id);
        return Ok(schedule);
    }

    /// <summary>
    /// Disable a schedule
    /// </summary>
    [HttpPost("{id}/disable")]
    [ProducesResponseType(typeof(ScheduleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Disable(Guid id)
    {
        var authResult = await _authorizationService.AuthorizeAsync(
            User,
            null,
            new PermissionRequirement(Permissions.Pipelines.Update));

        if (!authResult.Succeeded)
        {
            return Forbid();
        }

        var schedule = await _scheduleService.DisableAsync(id);
        return Ok(schedule);
    }

    /// <summary>
    /// Trigger a schedule immediately (manual execution)
    /// </summary>
    [HttpPost("{id}/trigger")]
    [ProducesResponseType(typeof(ScheduleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> TriggerNow(Guid id)
    {
        var authResult = await _authorizationService.AuthorizeAsync(
            User,
            null,
            new PermissionRequirement(Permissions.Pipelines.Execute));

        if (!authResult.Succeeded)
        {
            return Forbid();
        }

        var schedule = await _scheduleService.TriggerNowAsync(id);
        return Ok(schedule);
    }

    /// <summary>
    /// Validate a cron expression
    /// </summary>
    [HttpPost("validate-cron")]
    [ProducesResponseType(typeof(CronValidationResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ValidateCron([FromBody] ValidateCronRequest request)
    {
        var result = await _scheduleService.ValidateCronExpressionAsync(
            request.CronExpression, 
            request.Timezone);
        return Ok(result);
    }
}

public record ValidateCronRequest
{
    public required string CronExpression { get; init; }
    public required string Timezone { get; init; }
}
