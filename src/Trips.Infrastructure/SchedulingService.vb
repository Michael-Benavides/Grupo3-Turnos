Imports Microsoft.EntityFrameworkCore
Imports System.Linq
Imports System.Threading
Imports System.Threading.Tasks
Imports Trips.Application
Imports Trips.Domain

Public Class SchedulingService
    Implements ISchedulingService

    Private ReadOnly _dbContext As TripsDbContext
    Private ReadOnly _routesClient As IRoutesClient
    Private ReadOnly _fareCalculator As IFareCalculator

    Public Sub New(dbContext As TripsDbContext, routesClient As IRoutesClient, fareCalculator As IFareCalculator)
        _dbContext = dbContext
        _routesClient = routesClient
        _fareCalculator = fareCalculator
    End Sub

    Public Async Function CreateScheduleAsync(request As CreateScheduleRequest, cancellationToken As CancellationToken) As Task(Of ScheduleTemplateDto) Implements ISchedulingService.CreateScheduleAsync
        ValidateSchedule(request)
        Await EnsureRouteExistsAsync(request.RouteId, cancellationToken)

        Dim template = New ScheduleTemplate With {
            .RouteId = request.RouteId,
            .BusId = request.BusId,
            .Shift = request.Shift,
            .DepartureTime = request.DepartureTime,
            .BasePrice = request.BasePrice,
            .IsActive = True
        }

        _dbContext.ScheduleTemplates.Add(template)
        Await _dbContext.SaveChangesAsync(cancellationToken)
        Return ToDto(template)
    End Function

    Public Async Function GetSchedulesAsync(routeId As Guid?, cancellationToken As CancellationToken) As Task(Of IReadOnlyList(Of ScheduleTemplateDto)) Implements ISchedulingService.GetSchedulesAsync
        Dim query = _dbContext.ScheduleTemplates.AsNoTracking().Where(Function(template) template.IsActive)
        If routeId.HasValue Then
            query = query.Where(Function(template) template.RouteId = routeId.Value)
        End If

        Dim templates = Await query.OrderBy(Function(template) template.RouteId).
            ThenBy(Function(template) template.DepartureTime).
            ToListAsync(cancellationToken)
        Return templates.Select(Function(template) ToDto(template)).ToList()
    End Function

    Public Async Function UpdateScheduleAsync(id As Guid, request As UpdateScheduleRequest, cancellationToken As CancellationToken) As Task(Of ScheduleTemplateDto) Implements ISchedulingService.UpdateScheduleAsync
        ValidateSchedule(request)
        Await EnsureRouteExistsAsync(request.RouteId, cancellationToken)

        Dim template = Await _dbContext.ScheduleTemplates.SingleOrDefaultAsync(
            Function(candidate) candidate.Id = id AndAlso candidate.IsActive,
            cancellationToken)
        If template Is Nothing Then
            Throw New ResourceNotFoundException($"No existe una plantilla activa con Id {id}.")
        End If

        template.RouteId = request.RouteId
        template.BusId = request.BusId
        template.Shift = request.Shift
        template.DepartureTime = request.DepartureTime
        template.BasePrice = request.BasePrice

        Await _dbContext.SaveChangesAsync(cancellationToken)
        Return ToDto(template)
    End Function

    Public Async Function DeactivateScheduleAsync(id As Guid, cancellationToken As CancellationToken) As Task Implements ISchedulingService.DeactivateScheduleAsync
        Dim template = Await _dbContext.ScheduleTemplates.SingleOrDefaultAsync(
            Function(candidate) candidate.Id = id AndAlso candidate.IsActive,
            cancellationToken)
        If template Is Nothing Then
            Throw New ResourceNotFoundException($"No existe una plantilla activa con Id {id}.")
        End If

        template.IsActive = False
        Await _dbContext.SaveChangesAsync(cancellationToken)
    End Function

    Public Async Function GenerateTripsAsync(request As GenerateTripsRequest, cancellationToken As CancellationToken) As Task(Of GenerateTripsResultDto) Implements ISchedulingService.GenerateTripsAsync
        If request.FechaHasta < request.FechaDesde Then
            Throw New RequestValidationException("FechaHasta debe ser igual o posterior a FechaDesde.")
        End If

        Dim firstDate = request.FechaDesde.ToDateTime(TimeOnly.MinValue)
        Dim endDateExclusive = request.FechaHasta.AddDays(1).ToDateTime(TimeOnly.MinValue)
        Dim templatesQuery = _dbContext.ScheduleTemplates.AsNoTracking().Where(Function(template) template.IsActive)
        If request.RouteId.HasValue Then
            templatesQuery = templatesQuery.Where(Function(template) template.RouteId = request.RouteId.Value)
        End If

        Dim templates = Await templatesQuery.ToListAsync(cancellationToken)
        Dim fareRules = Await _dbContext.FareRules.AsNoTracking().ToListAsync(cancellationToken)
        Dim existingKeys = (Await _dbContext.Trips.AsNoTracking().
            Where(Function(trip) trip.DepartureAt >= firstDate AndAlso trip.DepartureAt < endDateExclusive).
            Select(Function(trip) New With {trip.TemplateId, trip.DepartureAt}).
            ToListAsync(cancellationToken)).
            Select(Function(trip) (trip.TemplateId, DateOnly.FromDateTime(trip.DepartureAt))).
            ToHashSet()

        Dim result = New GenerateTripsResultDto()
        Dim newTrips = New List(Of Trip)()

        For Each template In templates
            Dim currentDate = request.FechaDesde
            While currentDate <= request.FechaHasta
                Dim departureAt = currentDate.ToDateTime(TimeOnly.MinValue).Add(template.DepartureTime)
                Dim key = (template.Id, currentDate)
                If existingKeys.Add(key) Then
                    Dim trip = New Trip With {
                        .TemplateId = template.Id,
                        .RouteId = template.RouteId,
                        .BusId = template.BusId,
                        .DepartureAt = departureAt,
                        .DepartureDate = currentDate,
                        .Shift = template.Shift,
                        .Status = TripStatus.Scheduled,
                        .CurrentPrice = _fareCalculator.Calculate(New FareCalculationRequest With {
                            .BasePrice = template.BasePrice,
                            .Shift = template.Shift,
                            .DepartureDate = currentDate,
                            .TotalSeatCount = 40,
                            .Rules = fareRules
                        }).FinalPrice
                    }

                    For seatNumber = 1 To 40
                        trip.Seats.Add(New TripSeat With {
                            .TripId = trip.Id,
                            .SeatNumber = seatNumber,
                            .Status = TripSeatStatus.Available
                        })
                    Next

                    newTrips.Add(trip)
                    result.CreatedCount += 1
                Else
                    result.SkippedCount += 1
                End If

                currentDate = currentDate.AddDays(1)
            End While
        Next

        If newTrips.Count > 0 Then
            _dbContext.Trips.AddRange(newTrips)
            Await _dbContext.SaveChangesAsync(cancellationToken)
        End If

        Return result
    End Function

    Public Async Function SearchTripsAsync(routeId As Guid?, [date] As DateOnly?, shift As Shift?, cancellationToken As CancellationToken) As Task(Of IReadOnlyList(Of TripSearchDto)) Implements ISchedulingService.SearchTripsAsync
        Dim query = _dbContext.Trips.AsNoTracking().Include(Function(trip) trip.Seats).AsQueryable()

        If routeId.HasValue Then
            query = query.Where(Function(trip) trip.RouteId = routeId.Value)
        End If

        If [date].HasValue Then
            Dim start = [date].Value.ToDateTime(TimeOnly.MinValue)
            Dim endExclusive = start.AddDays(1)
            query = query.Where(Function(trip) trip.DepartureAt >= start AndAlso trip.DepartureAt < endExclusive)
        End If

        If shift.HasValue Then
            query = query.Where(Function(trip) trip.Shift = shift.Value)
        End If

        Dim trips = Await query.OrderBy(Function(trip) trip.DepartureAt).ToListAsync(cancellationToken)
        Return trips.Select(
            Function(trip) New TripSearchDto With {
                .Id = trip.Id,
                .TemplateId = trip.TemplateId,
                .RouteId = trip.RouteId,
                .BusId = trip.BusId,
                .DepartureAt = trip.DepartureAt,
                .Shift = trip.Shift,
                .Status = trip.Status,
                .CurrentPrice = trip.CurrentPrice,
                .AvailableSeats = Enumerable.Count(Of TripSeat)(
                    trip.Seats,
                    Function(seat) seat.Status = TripSeatStatus.Available)
            }).ToList()
    End Function

    Public Async Function GetTripPriceAsync(tripId As Guid, cancellationToken As CancellationToken) As Task(Of FareQuoteDto) Implements ISchedulingService.GetTripPriceAsync
        Dim trip = Await GetTripWithSeatsAsync(tripId, cancellationToken)
        Return Await CalculateTripPriceAsync(trip, cancellationToken)
    End Function

    Public Async Function HoldSeatAsync(tripId As Guid, seatNumber As Integer, userId As String, minutes As Integer, cancellationToken As CancellationToken) As Task(Of FareQuoteDto) Implements ISchedulingService.HoldSeatAsync
        Dim trip = Await GetTripWithSeatsAsync(tripId, cancellationToken)
        Dim seat = FindSeat(trip, seatNumber)
        seat.Hold(userId, minutes)

        Dim quote = Await RecalculateTripPriceAsync(trip, cancellationToken)
        Await SaveSeatChangesAsync(trip, cancellationToken)
        Return quote
    End Function

    Public Async Function ConfirmSeatAsync(tripId As Guid, seatNumber As Integer, userId As String, cancellationToken As CancellationToken) As Task(Of FareQuoteDto) Implements ISchedulingService.ConfirmSeatAsync
        Dim trip = Await GetTripWithSeatsAsync(tripId, cancellationToken)
        Dim seat = FindSeat(trip, seatNumber)
        seat.Confirm(userId)

        Dim quote = Await RecalculateTripPriceAsync(trip, cancellationToken)
        Await SaveSeatChangesAsync(trip, cancellationToken)
        Return quote
    End Function

    Public Async Function ReleaseSeatAsync(tripId As Guid, seatNumber As Integer, userId As String, cancellationToken As CancellationToken) As Task(Of FareQuoteDto) Implements ISchedulingService.ReleaseSeatAsync
        Dim trip = Await GetTripWithSeatsAsync(tripId, cancellationToken)
        Dim seat = FindSeat(trip, seatNumber)
        Dim previousStatus = seat.Status
        seat.Release(userId)

        If seat.Status <> previousStatus Then
            Dim quote = Await RecalculateTripPriceAsync(trip, cancellationToken)
            Await SaveSeatChangesAsync(trip, cancellationToken)
            Return quote
        End If

        Return Await CalculateTripPriceAsync(trip, cancellationToken)
    End Function

    Public Async Function GetTripSeatsAsync(tripId As Guid, cancellationToken As CancellationToken) As Task(Of IReadOnlyList(Of TripSeatDto)) Implements ISchedulingService.GetTripSeatsAsync
        Dim trip = Await GetTripWithSeatsAsync(tripId, cancellationToken)
        Return trip.Seats.
            OrderBy(Function(seat) seat.SeatNumber).
            Select(Function(seat) New TripSeatDto With {
                .SeatNumber = seat.SeatNumber,
                .Status = seat.Status
            }).
            ToList()
    End Function

    Public Async Function ReleaseExpiredSeatHoldsAsync(now As DateTime, cancellationToken As CancellationToken) As Task(Of Integer) Implements ISchedulingService.ReleaseExpiredSeatHoldsAsync
        Dim trips = Await _dbContext.Trips.
            Include(Function(trip) trip.Seats).
            Where(Function(trip) trip.Seats.Any(
                Function(seat) seat.Status = TripSeatStatus.Held AndAlso seat.HeldUntil.HasValue AndAlso seat.HeldUntil.Value <= now)).
            ToListAsync(cancellationToken)

        Dim releasedCount = 0
        For Each trip In trips
            Dim changed = False
            For Each seat In trip.Seats
                If seat.Status = TripSeatStatus.Held AndAlso seat.HeldUntil.HasValue AndAlso seat.HeldUntil.Value <= now Then
                    seat.ReleaseIfExpired(now)
                    releasedCount += 1
                    changed = True
                End If
            Next

            If changed Then
                Await RecalculateTripPriceAsync(trip, cancellationToken)
            End If
        Next

        If releasedCount > 0 Then
            Await SaveSeatChangesAsync(trips, cancellationToken)
        End If

        Return releasedCount
    End Function

    Private Async Function SaveSeatChangesAsync(trip As Trip, cancellationToken As CancellationToken) As Task
        Try
            Await _dbContext.SaveChangesAsync(cancellationToken)
        Catch ex As DbUpdateConcurrencyException
            Throw New SeatConcurrencyException("El asiento cambió mientras se procesaba la solicitud. Actualiza el mapa e inténtalo nuevamente.", ex)
        End Try
    End Function

    Private Async Function SaveSeatChangesAsync(trips As IEnumerable(Of Trip), cancellationToken As CancellationToken) As Task
        Try
            Await _dbContext.SaveChangesAsync(cancellationToken)
        Catch ex As DbUpdateConcurrencyException
            Throw New SeatConcurrencyException("Uno o más asientos cambiaron durante la limpieza de bloqueos vencidos.", ex)
        End Try
    End Function

    Private Async Function GetTripWithSeatsAsync(tripId As Guid, cancellationToken As CancellationToken) As Task(Of Trip)
        Dim trip = Await _dbContext.Trips.
            Include(Function(candidate) candidate.Seats).
            SingleOrDefaultAsync(Function(candidate) candidate.Id = tripId, cancellationToken)
        If trip Is Nothing Then
            Throw New ResourceNotFoundException($"No existe un viaje con Id {tripId}.")
        End If

        Return trip
    End Function

    Private Shared Function FindSeat(trip As Trip, seatNumber As Integer) As TripSeat
        Dim seat = trip.Seats.SingleOrDefault(Function(candidate) candidate.SeatNumber = seatNumber)
        If seat Is Nothing Then
            Throw New ResourceNotFoundException($"El asiento {seatNumber} no existe en el viaje {trip.Id}.")
        End If

        Return seat
    End Function

    Private Async Function RecalculateTripPriceAsync(trip As Trip, cancellationToken As CancellationToken) As Task(Of FareQuoteDto)
        Dim quote = Await CalculateTripPriceAsync(trip, cancellationToken)
        trip.CurrentPrice = quote.FinalPrice
        Return quote
    End Function

    Private Async Function CalculateTripPriceAsync(trip As Trip, cancellationToken As CancellationToken) As Task(Of FareQuoteDto)
        Dim template = Await _dbContext.ScheduleTemplates.AsNoTracking().
            SingleOrDefaultAsync(Function(candidate) candidate.Id = trip.TemplateId, cancellationToken)
        If template Is Nothing Then
            Throw New ResourceNotFoundException($"No existe la plantilla {trip.TemplateId} del viaje.")
        End If

        Dim rules = Await _dbContext.FareRules.AsNoTracking().ToListAsync(cancellationToken)
        Dim occupiedSeatCount = Enumerable.Count(
            trip.Seats,
            Function(seat) seat.Status = TripSeatStatus.Held OrElse seat.Status = TripSeatStatus.Sold)

        Return _fareCalculator.Calculate(New FareCalculationRequest With {
            .BasePrice = template.BasePrice,
            .Shift = trip.Shift,
            .DepartureDate = trip.DepartureDate,
            .OccupiedSeatCount = occupiedSeatCount,
            .TotalSeatCount = trip.Seats.Count,
            .Rules = rules
        })
    End Function

    Private Async Function EnsureRouteExistsAsync(routeId As Guid, cancellationToken As CancellationToken) As Task
        Dim route = Await _routesClient.GetRouteAsync(routeId, cancellationToken)
        If route Is Nothing Then
            Throw New RequestValidationException($"La ruta {routeId} no existe.")
        End If
    End Function

    Private Shared Sub ValidateSchedule(request As CreateScheduleRequest)
        If request.RouteId = Guid.Empty Then
            Throw New RequestValidationException("RouteId es obligatorio.")
        End If

        If request.BusId = Guid.Empty Then
            Throw New RequestValidationException("BusId es obligatorio.")
        End If

        ScheduleValidation.Validate(request.Shift, request.DepartureTime, request.BasePrice)
    End Sub

    Private Shared Function ToDto(template As ScheduleTemplate) As ScheduleTemplateDto
        Return New ScheduleTemplateDto With {
            .Id = template.Id,
            .RouteId = template.RouteId,
            .BusId = template.BusId,
            .Shift = template.Shift,
            .DepartureTime = template.DepartureTime,
            .BasePrice = template.BasePrice,
            .IsActive = template.IsActive
        }
    End Function
End Class
