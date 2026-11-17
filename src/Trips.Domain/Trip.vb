Public Class Trip
    Public Property Id As Guid = Guid.NewGuid()
    Public Property TemplateId As Guid
    Public Property RouteId As Guid
    Public Property BusId As Guid
    Public Property DepartureAt As DateTime
    Public Property DepartureDate As DateOnly
    Public Property Shift As Shift
    Public Property Status As TripStatus
    Public Property CurrentPrice As Decimal
    Public Property Seats As ICollection(Of TripSeat) = New List(Of TripSeat)()
End Class
