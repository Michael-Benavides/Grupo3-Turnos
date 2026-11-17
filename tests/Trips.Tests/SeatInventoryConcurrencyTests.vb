Imports Microsoft.EntityFrameworkCore
Imports Microsoft.EntityFrameworkCore.Diagnostics
Imports System.Linq
Imports System.Threading
Imports System.Threading.Tasks
Imports Trips.Application
Imports Trips.Domain
Imports Trips.Infrastructure
Imports Xunit

Public Class SeatInventoryConcurrencyTests
    <Fact>
    Public Async Function SimultaneousHoldsOnSameSeat_OnlyOneUserWins() As Task
        Dim databaseName = Guid.NewGuid().ToString()
        Dim tripId = Guid.NewGuid()
        Dim templateId = Guid.NewGuid()
        Dim routeId = Guid.NewGuid()
        Dim busId = Guid.NewGuid()

        Using setupContext = CreateContext(databaseName)
            setupContext.Database.EnsureCreated()
            setupContext.ScheduleTemplates.Add(New ScheduleTemplate With {
                .Id = templateId,
                .RouteId = routeId,
                .BusId = busId,
                .Shift = Shift.Morning,
                .DepartureTime = New TimeSpan(8, 0, 0),
                .BasePrice = 10D,
                .IsActive = True
            })

            Dim trip = New Trip With {
                .Id = tripId,
                .TemplateId = templateId,
                .RouteId = routeId,
                .BusId = busId,
                .DepartureAt = New DateTime(2026, 11, 16, 8, 0, 0),
                .DepartureDate = New DateOnly(2026, 11, 16),
                .Shift = Shift.Morning,
                .Status = TripStatus.Scheduled,
                .CurrentPrice = 10D
            }
            For seatNumber = 1 To 40
                trip.Seats.Add(New TripSeat With {
                    .TripId = tripId,
                    .SeatNumber = seatNumber,
                    .Status = TripSeatStatus.Available,
                    .RowVersion = New Byte() {1}
                })
            Next

            setupContext.Trips.Add(trip)
            Await setupContext.SaveChangesAsync()
        End Using

        Dim barrier = New TwoPartySaveBarrier()
        Dim firstTask = HoldWithSeparateContextAsync(databaseName, barrier, tripId, "passenger-1")
        Dim secondTask = HoldWithSeparateContextAsync(databaseName, barrier, tripId, "passenger-2")
        Dim results = Await Task.WhenAll(firstTask, secondTask)

        Assert.Equal(1, results.Count(Function(result) result))
        Assert.Equal(1, results.Count(Function(result) Not result))

        Using verifyContext = CreateContext(databaseName)
            Dim heldSeat = Await verifyContext.TripSeats.SingleAsync(
                Function(seat) seat.TripId = tripId AndAlso seat.SeatNumber = 1)
            Assert.Equal(TripSeatStatus.Held, heldSeat.Status)
            Assert.Contains(heldSeat.HeldBy, New String() {"passenger-1", "passenger-2"})
        End Using
    End Function

    <Fact>
    Public Async Function SeatMap_ReturnsFortySeats() As Task
        Dim databaseName = Guid.NewGuid().ToString()
        Dim tripId = Guid.NewGuid()
        Dim templateId = Guid.NewGuid()

        Using context = CreateContext(databaseName)
            context.Database.EnsureCreated()
            context.ScheduleTemplates.Add(New ScheduleTemplate With {
                .Id = templateId,
                .RouteId = Guid.NewGuid(),
                .BusId = Guid.NewGuid(),
                .Shift = Shift.Morning,
                .DepartureTime = New TimeSpan(8, 0, 0),
                .BasePrice = 10D,
                .IsActive = True
            })

            Dim trip = New Trip With {
                .Id = tripId,
                .TemplateId = templateId,
                .DepartureAt = New DateTime(2026, 11, 16, 8, 0, 0),
                .DepartureDate = New DateOnly(2026, 11, 16),
                .Shift = Shift.Morning,
                .Status = TripStatus.Scheduled,
                .CurrentPrice = 10D
            }
            For seatNumber = 1 To 40
                trip.Seats.Add(New TripSeat With {.TripId = tripId, .SeatNumber = seatNumber})
            Next
            context.Trips.Add(trip)
            Await context.SaveChangesAsync()

            Dim service = New SchedulingService(context, New FakeRoutesClient(), New FareCalculator())
            Dim seats = Await service.GetTripSeatsAsync(tripId, CancellationToken.None)

            Assert.Equal(40, seats.Count)
            Assert.All(seats, Sub(seat) Assert.Equal(TripSeatStatus.Available, seat.Status))
        End Using
    End Function

    Private Shared Async Function HoldWithSeparateContextAsync(
        databaseName As String,
        barrier As TwoPartySaveBarrier,
        tripId As Guid,
        userId As String) As Task(Of Boolean)

        Using context = CreateContext(databaseName, barrier)
            Dim service = New SchedulingService(context, New FakeRoutesClient(), New FareCalculator())
            Try
                Await service.HoldSeatAsync(tripId, 1, userId, 5, CancellationToken.None)
                Return True
            Catch ex As SeatConcurrencyException
                Return False
            End Try
        End Using
    End Function

    Private Shared Function CreateContext(databaseName As String, Optional barrier As TwoPartySaveBarrier = Nothing) As TripsDbContext
        Dim builder = New DbContextOptionsBuilder(Of TripsDbContext)().UseInMemoryDatabase(databaseName)
        If barrier IsNot Nothing Then
            builder.AddInterceptors(barrier)
        End If
        Return New TripsDbContext(builder.Options)
    End Function

    Private Class TwoPartySaveBarrier
        Inherits SaveChangesInterceptor

        Private ReadOnly _ready As New TaskCompletionSource(Of Boolean)(TaskCreationOptions.RunContinuationsAsynchronously)
        Private _arrivals As Integer
        Private _nextToken As Integer = 1

        Public Overrides Function SavingChangesAsync(
            eventData As DbContextEventData,
            result As InterceptionResult(Of Integer),
            Optional cancellationToken As CancellationToken = Nothing) As ValueTask(Of InterceptionResult(Of Integer))

            Return New ValueTask(Of InterceptionResult(Of Integer))(
                WaitForBothSavesAsync(eventData, result, cancellationToken))
        End Function

        Private Async Function WaitForBothSavesAsync(
            eventData As DbContextEventData,
            result As InterceptionResult(Of Integer),
            cancellationToken As CancellationToken) As Task(Of InterceptionResult(Of Integer))
            Dim context = eventData.Context
            If context IsNot Nothing Then
                For Each entry In context.ChangeTracker.Entries(Of TripSeat)().Where(Function(candidate) candidate.State = EntityState.Modified)
                    Dim token = Interlocked.Increment(_nextToken)
                    entry.Property(Function(seat) seat.RowVersion).CurrentValue = New Byte() {CByte(token)}
                    entry.Property(Function(seat) seat.RowVersion).IsModified = True
                Next
            End If

            If Interlocked.Increment(_arrivals) = 2 Then
                _ready.TrySetResult(True)
            End If

            Await _ready.Task.WaitAsync(cancellationToken)
            Return result
        End Function
    End Class
End Class
