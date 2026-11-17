Imports Trips.Domain
Imports Xunit

Public Class TripSeatTests
    <Fact>
    Public Sub Hold_OnlyReservesAnAvailableSeat()
        Dim seat = New TripSeat()

        seat.Hold("passenger-1", 5)

        Assert.Equal(TripSeatStatus.Held, seat.Status)
        Assert.Equal("passenger-1", seat.HeldBy)
        Assert.True(seat.HeldUntil > DateTime.UtcNow)
        Assert.Throws(Of DomainException)(Sub() seat.Hold("passenger-2", 5))
    End Sub

    <Fact>
    Public Sub Hold_RejectsInvalidUserAndDuration()
        Dim seat = New TripSeat()

        Assert.Throws(Of DomainException)(Sub() seat.Hold("", 5))
        Assert.Throws(Of DomainException)(Sub() seat.Hold("passenger-1", 0))
    End Sub

    <Fact>
    Public Sub Hold_ReplacesAnExpiredReservation()
        Dim seat = New TripSeat With {
            .Status = TripSeatStatus.Held,
            .HeldBy = "previous-user",
            .HeldUntil = DateTime.UtcNow.AddMinutes(-1)
        }

        seat.Hold("next-user", 5)

        Assert.Equal(TripSeatStatus.Held, seat.Status)
        Assert.Equal("next-user", seat.HeldBy)
    End Sub

    <Fact>
    Public Sub Confirm_RequiresTheSameUserAndAnUnexpiredHold()
        Dim seat = New TripSeat()
        seat.Hold("passenger-1", 5)

        Assert.Throws(Of DomainException)(Sub() seat.Confirm("passenger-2"))
        seat.Confirm("passenger-1")

        Assert.Equal(TripSeatStatus.Sold, seat.Status)
        Assert.Null(seat.HeldBy)
        Assert.Null(seat.HeldUntil)
    End Sub

    <Fact>
    Public Sub Release_OnlyFreesTheOwningUsersHold()
        Dim seat = New TripSeat()
        seat.Hold("passenger-1", 5)

        seat.Release("passenger-2")
        Assert.Equal(TripSeatStatus.Held, seat.Status)

        seat.Release("passenger-1")
        Assert.Equal(TripSeatStatus.Available, seat.Status)
        Assert.Null(seat.HeldBy)
        Assert.Null(seat.HeldUntil)
    End Sub

    <Fact>
    Public Sub ReleaseIfExpired_OnlyFreesExpiredHolds()
        Dim seat = New TripSeat()
        seat.Hold("passenger-1", 5)
        Dim expiry = seat.HeldUntil.Value

        seat.ReleaseIfExpired(expiry.AddTicks(-1))
        Assert.Equal(TripSeatStatus.Held, seat.Status)

        seat.ReleaseIfExpired(expiry)
        Assert.Equal(TripSeatStatus.Available, seat.Status)
    End Sub
End Class
