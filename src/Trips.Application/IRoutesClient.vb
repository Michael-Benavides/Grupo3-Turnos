Imports System.Threading
Imports System.Threading.Tasks

Public Interface IRoutesClient
    Function GetRouteAsync(routeId As Guid, cancellationToken As CancellationToken) As Task(Of RouteInfo)
End Interface
