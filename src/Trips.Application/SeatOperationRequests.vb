Imports System.ComponentModel.DataAnnotations

Public Class HoldSeatRequest
    <Required>
    Public Property UserId As String

    Public Property Minutes As Integer? = 5
End Class

Public Class SeatUserRequest
    <Required>
    Public Property UserId As String
End Class

Public Class TripSeatDto
    Public Property SeatNumber As Integer
    Public Property Status As Trips.Domain.TripSeatStatus
End Class
