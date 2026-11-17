Imports Microsoft.AspNetCore.Mvc
Imports Microsoft.AspNetCore.Http
Imports System.Threading
Imports System.Threading.Tasks
Imports Trips.Application
Imports Trips.Domain

<ApiController>
<Route("api/v1/trips")>
Public Class TripsController
    Inherits ControllerBase

    Private ReadOnly _schedulingService As ISchedulingService

    Public Sub New(schedulingService As ISchedulingService)
        _schedulingService = schedulingService
    End Sub

    <HttpGet("{id:guid}/seats")>
    Public Async Function GetSeats(id As Guid, cancellationToken As CancellationToken) As Task(Of ActionResult(Of IReadOnlyList(Of TripSeatDto)))
        Return Ok(Await _schedulingService.GetTripSeatsAsync(id, cancellationToken))
    End Function

    <HttpPost("generate")>
    Public Async Function Generate(
        <FromBody> request As GenerateTripsRequest,
        cancellationToken As CancellationToken) As Task(Of ActionResult(Of GenerateTripsResultDto))
        Return Ok(Await _schedulingService.GenerateTripsAsync(request, cancellationToken))
    End Function

    <HttpGet>
    Public Async Function Search(
        <FromQuery> routeId As Guid?,
        <FromQuery> [date] As DateOnly?,
        <FromQuery> shift As String,
        cancellationToken As CancellationToken) As Task(Of ActionResult(Of IReadOnlyList(Of TripSearchDto)))

        Dim parsedShift As Shift?
        If Not String.IsNullOrWhiteSpace(shift) Then
            Dim value As Shift
            If Not [Enum].TryParse(shift, True, value) OrElse Not [Enum].IsDefined(GetType(Shift), value) Then
                Return BadRequest(New ProblemDetails With {
                    .Status = StatusCodes.Status400BadRequest,
                    .Title = "Turno inválido",
                    .Detail = "Shift debe ser Morning, Afternoon o Night."
                })
            End If

            parsedShift = value
        End If

        Return Ok(Await _schedulingService.SearchTripsAsync(routeId, [date], parsedShift, cancellationToken))
    End Function

    <HttpGet("{id:guid}/price")>
    Public Async Function GetPrice(id As Guid, cancellationToken As CancellationToken) As Task(Of ActionResult(Of FareQuoteDto))
        Return Ok(Await _schedulingService.GetTripPriceAsync(id, cancellationToken))
    End Function

    <HttpPost("{tripId:guid}/seats/{seatNumber:int}/hold")>
    Public Async Function HoldSeat(
        tripId As Guid,
        seatNumber As Integer,
        <FromBody> request As HoldSeatRequest,
        cancellationToken As CancellationToken) As Task(Of ActionResult(Of FareQuoteDto))
        Return Ok(Await _schedulingService.HoldSeatAsync(tripId, seatNumber, request.UserId, If(request.Minutes, 5), cancellationToken))
    End Function

    <HttpPost("{tripId:guid}/seats/{seatNumber:int}/confirm")>
    Public Async Function ConfirmSeat(
        tripId As Guid,
        seatNumber As Integer,
        <FromBody> request As SeatUserRequest,
        cancellationToken As CancellationToken) As Task(Of ActionResult(Of FareQuoteDto))
        Return Ok(Await _schedulingService.ConfirmSeatAsync(tripId, seatNumber, request.UserId, cancellationToken))
    End Function

    <HttpDelete("{tripId:guid}/seats/{seatNumber:int}/hold")>
    Public Async Function ReleaseSeat(
        tripId As Guid,
        seatNumber As Integer,
        <FromBody> request As SeatUserRequest,
        cancellationToken As CancellationToken) As Task(Of ActionResult(Of FareQuoteDto))
        Return Ok(Await _schedulingService.ReleaseSeatAsync(tripId, seatNumber, request.UserId, cancellationToken))
    End Function
End Class
