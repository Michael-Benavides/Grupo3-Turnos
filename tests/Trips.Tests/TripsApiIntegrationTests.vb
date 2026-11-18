Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports System.Net
Imports System.Net.Http
Imports System.Text
Imports System.Text.Json
Imports System.Threading.Tasks
Imports Xunit
Imports Microsoft.AspNetCore.Mvc.Testing

Public Class TripsApiIntegrationTests
    Implements IClassFixture(Of TripsApiFactory)

    Private ReadOnly _factory As TripsApiFactory
    Private ReadOnly _jsonOptions As JsonSerializerOptions = New JsonSerializerOptions With {.PropertyNameCaseInsensitive = True}

    Public Sub New(factory As TripsApiFactory)
        _factory = factory
    End Sub

    Private Async Function PostJsonAsync(url As String, payload As Object) As Task(Of HttpResponseMessage)
        Using client = _factory.CreateClient()
            Dim json = JsonSerializer.Serialize(payload)
            Dim content = New StringContent(json, Encoding.UTF8, "application/json")
            Return Await client.PostAsync(url, content)
        End Using
    End Function

    ' Cualquier 4xx (error del cliente)
    Private Shared Function IsClientError(resp As HttpResponseMessage) As Boolean
        Dim code As Integer = CInt(resp.StatusCode)
        Return code >= 400 AndAlso code < 500
    End Function

    ' Conflicto de concurrencia (409) o regla de dominio violada (422)
    Private Shared Function IsConflictOrRule(resp As HttpResponseMessage) As Boolean
        Return resp.StatusCode = HttpStatusCode.Conflict OrElse resp.StatusCode = HttpStatusCode.UnprocessableEntity
    End Function

    Private Async Function GetFirstTripIdAsync(client As HttpClient) As Task(Of Guid)
        Dim respTrips = Await client.GetAsync("/api/v1/trips")
        Assert.Equal(HttpStatusCode.OK, respTrips.StatusCode)
        Dim trips = JsonSerializer.Deserialize(Of TripDto())(Await respTrips.Content.ReadAsStringAsync(), _jsonOptions)
        Assert.NotEmpty(trips)
        Return trips(0).Id
    End Function

    <Fact>
    Public Async Function FullFlow_WorksAsExpected() As Task
        Using client = _factory.CreateClient()
            ' 1. Lista de viajes
            Dim tripId = Await GetFirstTripIdAsync(client)

            ' 2. Asientos (40 Available)
            Dim respSeats = Await client.GetAsync($"/api/v1/trips/{tripId}/seats")
            Assert.Equal(HttpStatusCode.OK, respSeats.StatusCode)
            Dim seats = JsonSerializer.Deserialize(Of TripSeatDto())(Await respSeats.Content.ReadAsStringAsync(), _jsonOptions)
            Assert.Equal(40, seats.Length)

            ' 3. Hold del asiento 1
            Dim respHold = Await PostJsonAsync($"/api/v1/trips/{tripId}/seats/1/hold", New With {.UserId = "user1", .Minutes = 5})
            Assert.Equal(HttpStatusCode.OK, respHold.StatusCode)

            ' 4. Confirm del asiento 1
            Dim respConfirm = Await PostJsonAsync($"/api/v1/trips/{tripId}/seats/1/confirm", New With {.UserId = "user1"})
            Assert.Equal(HttpStatusCode.OK, respConfirm.StatusCode)

            ' 5. El asiento 1 ahora es Sold
            Dim respSeats2 = Await client.GetAsync($"/api/v1/trips/{tripId}/seats")
            Assert.Equal(HttpStatusCode.OK, respSeats2.StatusCode)
            Dim seats2 = JsonSerializer.Deserialize(Of TripSeatDto())(Await respSeats2.Content.ReadAsStringAsync(), _jsonOptions)
            Dim seat1 = seats2.First(Function(s) s.SeatNumber = 1)
            Assert.Equal("Sold", seat1.Status)

            ' 6. Precio
            Dim respPrice = Await client.GetAsync($"/api/v1/trips/{tripId}/price")
            Assert.Equal(HttpStatusCode.OK, respPrice.StatusCode)
        End Using
    End Function

    <Fact>
    Public Async Function ConcurrentHolds_OnlyOneSucceeds() As Task
        Using client = _factory.CreateClient()
            Dim tripId = Await GetFirstTripIdAsync(client)

            ' Asiento 10: no lo usa ninguna otra prueba
            Dim tasks As New List(Of Task(Of HttpResponseMessage))()
            For i As Integer = 1 To 20
                Dim body = New With {.UserId = $"user{i}", .Minutes = 5}
                tasks.Add(PostJsonAsync($"/api/v1/trips/{tripId}/seats/10/hold", body))
            Next

            Dim results = Await Task.WhenAll(tasks)
            Dim okCount = results.Count(Function(r) r.StatusCode = HttpStatusCode.OK)
            Dim rejectedCount = results.Count(Function(r) IsConflictOrRule(r))
            Dim summary = String.Join(", ", results.Select(Function(r) CInt(r.StatusCode).ToString()))

            Assert.True(okCount = 1, "Ganadores: " & okCount & ". Códigos: " & summary)
            Assert.True(rejectedCount = 19, "Rechazados 409/422: " & rejectedCount & ". Códigos: " & summary)
        End Using
    End Function

    <Fact>
    Public Async Function EdgeCases_ReturnAppropriateStatusCodes() As Task
        Using client = _factory.CreateClient()
            Dim tripId = Await GetFirstTripIdAsync(client)

            ' Asiento 0 (inválido)
            Dim resp0 = Await PostJsonAsync($"/api/v1/trips/{tripId}/seats/0/hold", New With {.UserId = "u0", .Minutes = 5})
            Assert.True(IsClientError(resp0), "Asiento 0 devolvió: " & resp0.StatusCode.ToString())

            ' Asiento 41 (fuera de rango)
            Dim resp41 = Await PostJsonAsync($"/api/v1/trips/{tripId}/seats/41/hold", New With {.UserId = "u41", .Minutes = 5})
            Assert.True(IsClientError(resp41), "Asiento 41 devolvió: " & resp41.StatusCode.ToString())

            ' Hold y confirm del asiento 20 por su dueño
            Dim holdResp = Await PostJsonAsync($"/api/v1/trips/{tripId}/seats/20/hold", New With {.UserId = "owner", .Minutes = 5})
            Assert.Equal(HttpStatusCode.OK, holdResp.StatusCode)
            Dim confirmResp = Await PostJsonAsync($"/api/v1/trips/{tripId}/seats/20/confirm", New With {.UserId = "owner"})
            Assert.Equal(HttpStatusCode.OK, confirmResp.StatusCode)

            ' Hold sobre un asiento ya vendido
            Dim holdSoldResp = Await PostJsonAsync($"/api/v1/trips/{tripId}/seats/20/hold", New With {.UserId = "other", .Minutes = 5})
            Assert.True(IsConflictOrRule(holdSoldResp), "Hold sobre vendido devolvió: " & holdSoldResp.StatusCode.ToString())

            ' Confirm por otro usuario
            Dim confirmOtherResp = Await PostJsonAsync($"/api/v1/trips/{tripId}/seats/20/confirm", New With {.UserId = "other"})
            Assert.True(IsClientError(confirmOtherResp), "Confirm de otro usuario devolvió: " & confirmOtherResp.StatusCode.ToString())
        End Using
    End Function

    ' DTOs simples para deserializar
    Private Class TripDto
        Public Property Id As Guid
    End Class

    Private Class TripSeatDto
        Public Property SeatNumber As Integer
        Public Property Status As String
    End Class
End Class