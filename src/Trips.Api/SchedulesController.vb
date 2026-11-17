Imports Microsoft.AspNetCore.Mvc
Imports Microsoft.AspNetCore.Http
Imports System.Threading
Imports System.Threading.Tasks
Imports Trips.Application

<ApiController>
<Route("api/v1/schedules")>
Public Class SchedulesController
    Inherits ControllerBase

    Private ReadOnly _schedulingService As ISchedulingService

    Public Sub New(schedulingService As ISchedulingService)
        _schedulingService = schedulingService
    End Sub

    <HttpPost>
    <ProducesResponseType(GetType(ScheduleTemplateDto), StatusCodes.Status201Created)>
    Public Async Function Create(
        <FromBody> request As CreateScheduleRequest,
        cancellationToken As CancellationToken) As Task(Of IActionResult)
        Dim createdDto = Await _schedulingService.CreateScheduleAsync(request, cancellationToken)
        Return Created($"/api/schedules?routeId={createdDto.RouteId}", createdDto)
    End Function

    <HttpGet>
    Public Async Function GetActive(
        <FromQuery> routeId As Guid?,
        cancellationToken As CancellationToken) As Task(Of ActionResult(Of IReadOnlyList(Of ScheduleTemplateDto)))
        Return Ok(Await _schedulingService.GetSchedulesAsync(routeId, cancellationToken))
    End Function

    <HttpPut("{id:guid}")>
    Public Async Function Update(
        id As Guid,
        <FromBody> request As UpdateScheduleRequest,
        cancellationToken As CancellationToken) As Task(Of ActionResult(Of ScheduleTemplateDto))
        Return Ok(Await _schedulingService.UpdateScheduleAsync(id, request, cancellationToken))
    End Function

    <HttpDelete("{id:guid}")>
    <ProducesResponseType(StatusCodes.Status204NoContent)>
    Public Async Function Deactivate(id As Guid, cancellationToken As CancellationToken) As Task(Of NoContentResult)
        Await _schedulingService.DeactivateScheduleAsync(id, cancellationToken)
        Return NoContent()
    End Function
End Class
