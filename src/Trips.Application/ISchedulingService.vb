Imports Trips.Domain
Imports System.Threading
Imports System.Threading.Tasks

Public Interface ISchedulingService
    Function CreateScheduleAsync(request As CreateScheduleRequest, cancellationToken As CancellationToken) As Task(Of ScheduleTemplateDto)
    Function GetSchedulesAsync(routeId As Guid?, cancellationToken As CancellationToken) As Task(Of IReadOnlyList(Of ScheduleTemplateDto))
    Function UpdateScheduleAsync(id As Guid, request As UpdateScheduleRequest, cancellationToken As CancellationToken) As Task(Of ScheduleTemplateDto)
    Function DeactivateScheduleAsync(id As Guid, cancellationToken As CancellationToken) As Task
    Function GenerateTripsAsync(request As GenerateTripsRequest, cancellationToken As CancellationToken) As Task(Of GenerateTripsResultDto)
    Function SearchTripsAsync(routeId As Guid?, [date] As DateOnly?, shift As Shift?, cancellationToken As CancellationToken) As Task(Of IReadOnlyList(Of TripSearchDto))
    Function GetTripPriceAsync(tripId As Guid, cancellationToken As CancellationToken) As Task(Of FareQuoteDto)
    Function HoldSeatAsync(tripId As Guid, seatNumber As Integer, userId As String, minutes As Integer, cancellationToken As CancellationToken) As Task(Of FareQuoteDto)
    Function ConfirmSeatAsync(tripId As Guid, seatNumber As Integer, userId As String, cancellationToken As CancellationToken) As Task(Of FareQuoteDto)
    Function ReleaseSeatAsync(tripId As Guid, seatNumber As Integer, userId As String, cancellationToken As CancellationToken) As Task(Of FareQuoteDto)
    Function GetTripSeatsAsync(tripId As Guid, cancellationToken As CancellationToken) As Task(Of IReadOnlyList(Of TripSeatDto))
    Function ReleaseExpiredSeatHoldsAsync(now As DateTime, cancellationToken As CancellationToken) As Task(Of Integer)
End Interface
