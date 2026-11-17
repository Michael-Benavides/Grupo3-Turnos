Imports Trips.Domain

Public Module ScheduleValidation
    Public Sub Validate(shift As Shift, departureTime As TimeSpan, basePrice As Decimal)
        If basePrice <= 0D OrElse basePrice > 99999999.99D Then
            Throw New RequestValidationException("BasePrice debe ser mayor que cero y no superar 99999999.99.")
        End If

        If Not [Enum].IsDefined(GetType(Shift), shift) Then
            Throw New RequestValidationException("El turno especificado no es válido.")
        End If

        Dim isValidTime As Boolean
        Select Case shift
            Case Shift.Morning
                isValidTime = departureTime >= New TimeSpan(5, 0, 0) AndAlso departureTime < New TimeSpan(12, 0, 0)
            Case Shift.Afternoon
                isValidTime = departureTime >= New TimeSpan(12, 0, 0) AndAlso departureTime < New TimeSpan(18, 0, 0)
            Case Shift.Night
                isValidTime = departureTime >= New TimeSpan(18, 0, 0) AndAlso departureTime < TimeSpan.FromDays(1)
        End Select

        If Not isValidTime Then
            Throw New RequestValidationException($"La hora {departureTime:hh\:mm} no corresponde al turno {shift}.")
        End If
    End Sub
End Module
