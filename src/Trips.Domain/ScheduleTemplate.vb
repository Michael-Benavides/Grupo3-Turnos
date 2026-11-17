Public Class ScheduleTemplate
    Public Property Id As Guid = Guid.NewGuid()
    Public Property RouteId As Guid
    Public Property BusId As Guid
    Public Property Shift As Shift
    Public Property DepartureTime As TimeSpan
    Public Property BasePrice As Decimal
    Public Property IsActive As Boolean
End Class
