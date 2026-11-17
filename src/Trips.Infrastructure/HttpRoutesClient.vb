Imports System.Net.Http.Json
Imports System.Net.Http
Imports System.Threading
Imports System.Threading.Tasks
Imports Trips.Application

Public Class HttpRoutesClient
    Implements IRoutesClient

    Private ReadOnly _httpClient As HttpClient

    Public Sub New(httpClient As HttpClient)
        _httpClient = httpClient
    End Sub

    Public Async Function GetRouteAsync(routeId As Guid, cancellationToken As CancellationToken) As Task(Of RouteInfo) Implements IRoutesClient.GetRouteAsync
        Return Await _httpClient.GetFromJsonAsync(Of RouteInfo)($"/api/routes/{routeId}", cancellationToken)
    End Function
End Class
