Public Class TripSeat
    Public Property Id As Guid = Guid.NewGuid()
    Public Property TripId As Guid
    Public Property SeatNumber As Integer
    Public Property Status As TripSeatStatus = TripSeatStatus.Available
    Public Property HeldBy As String
    Public Property HeldUntil As DateTime?
    Public Property RowVersion As Byte() = Array.Empty(Of Byte)()

    Public Sub Hold(userId As String, minutes As Integer)
        If String.IsNullOrWhiteSpace(userId) Then
            Throw New DomainException("El identificador del usuario es obligatorio.")
        End If

        If minutes <= 0 Then
            Throw New DomainException("La duración del bloqueo debe ser mayor que cero.")
        End If

        If Status = TripSeatStatus.Held AndAlso HeldUntil.HasValue AndAlso HeldUntil.Value <= DateTime.UtcNow Then
            ReleaseHold()
        End If

        If Status <> TripSeatStatus.Available Then
            Throw New DomainException("El asiento no está disponible para bloquear.")
        End If

        Status = TripSeatStatus.Held
        HeldBy = userId
        HeldUntil = DateTime.UtcNow.AddMinutes(minutes)
    End Sub

    Public Sub Confirm(userId As String)
        If Status <> TripSeatStatus.Held OrElse
           Not String.Equals(HeldBy, userId, StringComparison.Ordinal) OrElse
           Not HeldUntil.HasValue OrElse
           HeldUntil.Value <= DateTime.UtcNow Then
            Throw New DomainException("El bloqueo del asiento no existe, expiró o pertenece a otro usuario.")
        End If

        Status = TripSeatStatus.Sold
        ClearHold()
    End Sub

    Public Sub Release(userId As String)
        If Status = TripSeatStatus.Held AndAlso String.Equals(HeldBy, userId, StringComparison.Ordinal) Then
            ReleaseHold()
        End If
    End Sub

    Public Sub ReleaseIfExpired(now As DateTime)
        If Status = TripSeatStatus.Held AndAlso HeldUntil.HasValue AndAlso HeldUntil.Value <= now Then
            ReleaseHold()
        End If
    End Sub

    Private Sub ReleaseHold()
        Status = TripSeatStatus.Available
        ClearHold()
    End Sub

    Private Sub ClearHold()
        HeldBy = Nothing
        HeldUntil = Nothing
    End Sub
End Class
