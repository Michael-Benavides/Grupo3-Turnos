Imports Microsoft.EntityFrameworkCore
Imports System.Threading
Imports System.Threading.Tasks
Imports Trips.Application
Imports Trips.Domain
Imports Trips.Infrastructure
Imports Xunit

Public Class SchedulingServiceTests
    <Fact>
    Public Sub ScheduleValidation_EnforcesShiftBoundaries()
        ScheduleValidation.Validate(Shift.Morning, New TimeSpan(5, 0, 0), 10D)
        ScheduleValidation.Validate(Shift.Morning, New TimeSpan(11, 59, 59), 10D)
        ScheduleValidation.Validate(Shift.Afternoon, New TimeSpan(12, 0, 0), 10D)
        ScheduleValidation.Validate(Shift.Afternoon, New TimeSpan(17, 59, 59), 10D)
        ScheduleValidation.Validate(Shift.Night, New TimeSpan(18, 0, 0), 10D)
        ScheduleValidation.Validate(Shift.Night, New TimeSpan(23, 59, 59), 10D)

        Assert.Throws(Of RequestValidationException)(
            Sub() ScheduleValidation.Validate(Shift.Morning, New TimeSpan(4, 59, 59), 10D))
        Assert.Throws(Of RequestValidationException)(
            Sub() ScheduleValidation.Validate(Shift.Morning, New TimeSpan(12, 0, 0), 10D))
        Assert.Throws(Of RequestValidationException)(
            Sub() ScheduleValidation.Validate(Shift.Afternoon, New TimeSpan(11, 59, 59), 10D))
        Assert.Throws(Of RequestValidationException)(
            Sub() ScheduleValidation.Validate(Shift.Afternoon, New TimeSpan(18, 0, 0), 10D))
        Assert.Throws(Of RequestValidationException)(
            Sub() ScheduleValidation.Validate(Shift.Night, New TimeSpan(17, 59, 59), 10D))
        Assert.Throws(Of RequestValidationException)(
            Sub() ScheduleValidation.Validate(Shift.Night, TimeSpan.FromDays(1), 10D))
        Assert.Throws(Of RequestValidationException)(
            Sub() ScheduleValidation.Validate(Shift.Afternoon, New TimeSpan(13, 0, 0), 0D))
    End Sub

    <Fact>
    Public Async Function GenerateTrips_IsIdempotentForTemplateAndDate() As Task
        Using context = CreateContext()
            AddTemplate(context)
            Dim service = New SchedulingService(context, New FakeRoutesClient(), New FareCalculator())
            Dim request = New GenerateTripsRequest With {
                .FechaDesde = New DateOnly(2026, 11, 15),
                .FechaHasta = New DateOnly(2026, 11, 15)
            }

            Dim firstResult = Await service.GenerateTripsAsync(request, CancellationToken.None)
            Dim template = Await context.ScheduleTemplates.SingleAsync()
            template.DepartureTime = New TimeSpan(9, 0, 0)
            Await context.SaveChangesAsync()
            Dim secondResult = Await service.GenerateTripsAsync(request, CancellationToken.None)

            Assert.Equal(1, firstResult.CreatedCount)
            Assert.Equal(0, firstResult.SkippedCount)
            Assert.Equal(0, secondResult.CreatedCount)
            Assert.Equal(1, secondResult.SkippedCount)
            Assert.Equal(1, Await context.Trips.CountAsync())
        End Using
    End Function

    <Fact>
    Public Async Function GenerateTrips_CreatesFortyAvailableSeatsPerTrip() As Task
        Using context = CreateContext()
            AddTemplate(context)
            Dim service = New SchedulingService(context, New FakeRoutesClient(), New FareCalculator())
            Dim request = New GenerateTripsRequest With {
                .FechaDesde = New DateOnly(2026, 11, 15),
                .FechaHasta = New DateOnly(2026, 11, 15)
            }

            Await service.GenerateTripsAsync(request, CancellationToken.None)

            Dim trip = Await context.Trips.Include(Function(item) item.Seats).SingleAsync()
            Assert.Equal(40, trip.Seats.Count)
            Assert.All(trip.Seats, Sub(seat) Assert.Equal(TripSeatStatus.Available, seat.Status))
        End Using
    End Function

    <Fact>
    Public Async Function SeatChanges_RecalculateCurrentPriceUsingHeldAndSoldOccupancy() As Task
        Using context = CreateContext()
            AddTemplate(context)
            Dim service = New SchedulingService(context, New FakeRoutesClient(), New FareCalculator())
            Dim request = New GenerateTripsRequest With {
                .FechaDesde = New DateOnly(2026, 11, 16),
                .FechaHasta = New DateOnly(2026, 11, 16)
            }

            Await service.GenerateTripsAsync(request, CancellationToken.None)
            Dim trip = Await context.Trips.Include(Function(item) item.Seats).SingleAsync()
            For Each seat In trip.Seats.Take(28)
                seat.Hold("existing-user", 30)
            Next
            Await context.SaveChangesAsync()

            Dim heldQuote = Await service.HoldSeatAsync(trip.Id, 29, "new-user", 30, CancellationToken.None)
            Dim confirmedQuote = Await service.ConfirmSeatAsync(trip.Id, 29, "new-user", CancellationToken.None)
            Dim releasedQuote = Await service.ReleaseSeatAsync(trip.Id, 1, "existing-user", CancellationToken.None)

            Assert.Equal(11.5D, heldQuote.FinalPrice)
            Assert.Equal(11.5D, confirmedQuote.FinalPrice)
            Assert.Equal(10D, releasedQuote.FinalPrice)
            Assert.Equal(10D, trip.CurrentPrice)
        End Using
    End Function

    <Fact>
    Public Async Function ExpiredHoldCleaner_ReleasesSeatAndRecalculatesPrice() As Task
        Using context = CreateContext()
            AddTemplate(context)
            Dim service = New SchedulingService(context, New FakeRoutesClient(), New FareCalculator())
            Await service.GenerateTripsAsync(
                New GenerateTripsRequest With {
                    .FechaDesde = New DateOnly(2026, 11, 16),
                    .FechaHasta = New DateOnly(2026, 11, 16)
                },
                CancellationToken.None)

            Dim trip = Await context.Trips.Include(Function(item) item.Seats).SingleAsync()
            For Each seat In trip.Seats.Take(29)
                seat.Status = TripSeatStatus.Held
                seat.HeldBy = "passenger"
                seat.HeldUntil = New DateTime(2026, 11, 16, 8, 0, 0)
            Next
            Await context.SaveChangesAsync()

            Dim released = Await service.ReleaseExpiredSeatHoldsAsync(
                New DateTime(2026, 11, 16, 8, 0, 1),
                CancellationToken.None)
            Dim refreshedTrip = Await context.Trips.Include(Function(item) item.Seats).SingleAsync()

            Assert.Equal(29, released)
            Assert.All(refreshedTrip.Seats, Sub(seat) Assert.Equal(TripSeatStatus.Available, seat.Status))
            Assert.Equal(10D, refreshedTrip.CurrentPrice)
        End Using
    End Function

    Private Shared Function CreateContext() As TripsDbContext
        Dim options = New DbContextOptionsBuilder(Of TripsDbContext)().
            UseInMemoryDatabase(Guid.NewGuid().ToString()).
            Options
        Dim context = New TripsDbContext(options)
        context.Database.EnsureCreated()
        Return context
    End Function

    Private Shared Sub AddTemplate(context As TripsDbContext)
        context.ScheduleTemplates.Add(New ScheduleTemplate With {
            .RouteId = Guid.NewGuid(),
            .BusId = Guid.NewGuid(),
            .Shift = Shift.Morning,
            .DepartureTime = New TimeSpan(8, 0, 0),
            .BasePrice = 10D,
            .IsActive = True
        })
        context.SaveChanges()
    End Sub
End Class
