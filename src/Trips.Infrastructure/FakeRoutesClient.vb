Imports Trips.Application
Imports System.Threading
Imports System.Threading.Tasks

Public Class FakeRoutesClient
    Implements IRoutesClient

    Public Function GetRouteAsync(routeId As Guid, cancellationToken As CancellationToken) As Task(Of RouteInfo) Implements IRoutesClient.GetRouteAsync
        cancellationToken.ThrowIfCancellationRequested()

        Return Task.FromResult(New RouteInfo With {
            .Id = routeId,
            .Origin = "Tulcán",
            .Destination = "Ibarra"
        })
    End Function
End Class
