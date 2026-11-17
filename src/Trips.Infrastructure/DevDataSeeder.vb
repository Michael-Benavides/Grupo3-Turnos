Imports System
Imports System.Collections.Generic
Imports System.Threading
Imports System.Threading.Tasks
Imports Microsoft.EntityFrameworkCore
Imports Trips.Application
Imports Trips.Domain

''' <summary>
''' Datos de demostración solo para Development.
''' Los GUIDs son fijos para que el frontend y los otros servicios puedan referenciarlos.
''' </summary>
Public Class DevDataSeeder

    Public Shared ReadOnly DemoRouteIds As Guid() = {
        Guid.Parse("10000000-0000-0000-0000-000000000001"),
        Guid.Parse("10000000-0000-0000-0000-000000000002"),
        Guid.Parse("10000000-0000-0000-0000-000000000003"),
        Guid.Parse("10000000-0000-0000-0000-000000000004"),
        Guid.Parse("10000000-0000-0000-0000-000000000005")
    }

    Public Shared ReadOnly DemoBusIds As Guid() = {
        Guid.Parse("20000000-0000-0000-0000-000000000001"),
        Guid.Parse("20000000-0000-0000-0000-000000000002"),
        Guid.Parse("20000000-0000-0000-0000-000000000003")
    }

    Public Shared Async Function EnsureSeededAsync(db As TripsDbContext,
                                                   scheduling As ISchedulingService,
                                                   routes As IRoutesClient,
                                                   ct As CancellationToken) As Task

        Dim shiftTimes As New List(Of KeyValuePair(Of Shift, TimeSpan)) From {
            New KeyValuePair(Of Shift, TimeSpan)(Shift.Morning, New TimeSpan(6, 0, 0)),
            New KeyValuePair(Of Shift, TimeSpan)(Shift.Afternoon, New TimeSpan(14, 0, 0)),
            New KeyValuePair(Of Shift, TimeSpan)(Shift.Night, New TimeSpan(20, 0, 0))
        }

        For i As Integer = 0 To DemoRouteIds.Length - 1
            Dim routeId = DemoRouteIds(i)
            Dim busId = DemoBusIds(i Mod DemoBusIds.Length)

            For Each item In shiftTimes
                Dim shiftValue = item.Key
                Dim exists = Await db.ScheduleTemplates.AnyAsync(
                    Function(t) t.RouteId = routeId AndAlso t.Shift = shiftValue, ct)

                If Not exists Then
                    db.ScheduleTemplates.Add(New ScheduleTemplate With {
                        .Id = Guid.NewGuid(),
                        .RouteId = routeId,
                        .BusId = busId,
                        .Shift = shiftValue,
                        .DepartureTime = item.Value,
                        .BasePrice = 10D,
                        .IsActive = True
                    })
                End If
            Next
        Next
        Await db.SaveChangesAsync(ct)

        Dim request = New GenerateTripsRequest With {
            .FechaDesde = DateOnly.FromDateTime(DateTime.UtcNow.Date),
            .FechaHasta = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(6))
        }
        Await scheduling.GenerateTripsAsync(request, ct)
    End Function
End Class