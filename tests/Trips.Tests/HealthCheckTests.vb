Imports System
Imports System.Net.Http
Imports System.Threading.Tasks
Imports Microsoft.AspNetCore.Mvc.Testing
Imports Xunit

Public Class HealthCheckTests
    Private ReadOnly _factory As New TripsApiFactory()

    <Fact>
    Public Async Function HealthEndpoint_Returns200() As Task
        Using client = _factory.CreateClient()
            Dim response = Await client.GetAsync("/health")
            Assert.Equal(200, CInt(response.StatusCode))
        End Using
    End Function
End Class
