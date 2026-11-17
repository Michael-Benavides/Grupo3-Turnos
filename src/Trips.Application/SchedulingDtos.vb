Imports System.ComponentModel.DataAnnotations
Imports Trips.Domain

Public Class CreateScheduleRequest
    <Required>
    Public Property RouteId As Guid

    <Required>
    Public Property BusId As Guid

    <Required>
    Public Property Shift As Shift

    <Required>
    Public Property DepartureTime As TimeSpan

    Public Property BasePrice As Decimal
End Class

Public Class UpdateScheduleRequest
    Inherits CreateScheduleRequest
End Class

Public Class ScheduleTemplateDto
    Public Property Id As Guid
    Public Property RouteId As Guid
    Public Property BusId As Guid
    Public Property Shift As Shift
    Public Property DepartureTime As TimeSpan
    Public Property BasePrice As Decimal
    Public Property IsActive As Boolean
End Class

Public Class GenerateTripsRequest
    <Required>
    Public Property FechaDesde As DateOnly

    <Required>
    Public Property FechaHasta As DateOnly

    Public Property RouteId As Guid?
End Class

Public Class GenerateTripsResultDto
    Public Property CreatedCount As Integer
    Public Property SkippedCount As Integer
End Class

Public Class TripSearchDto
    Public Property Id As Guid
    Public Property TemplateId As Guid
    Public Property RouteId As Guid
    Public Property BusId As Guid
    Public Property DepartureAt As DateTime
    Public Property Shift As Shift
    Public Property Status As TripStatus
    Public Property CurrentPrice As Decimal
    Public Property AvailableSeats As Integer
End Class

Public Class RouteInfo
    Public Property Id As Guid
    Public Property Origin As String
    Public Property Destination As String
End Class
