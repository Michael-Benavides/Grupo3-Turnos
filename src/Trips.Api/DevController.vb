Imports Microsoft.AspNetCore.Mvc
Imports Microsoft.Extensions.DependencyInjection
Imports Microsoft.Extensions.Hosting
Imports Microsoft.Extensions.Logging
Imports Microsoft.Extensions.Options
Imports System.Threading
Imports Microsoft.EntityFrameworkCore
Imports Trips.Application
Imports Trips.Infrastructure

Namespace Trips.Api
    <ApiController>
    <Route("api/v1/dev")>
    Public Class DevController
        Inherits ControllerBase

        Private ReadOnly _logger As ILogger(Of DevController)
        Private ReadOnly _env As IHostEnvironment

        Public Sub New(logger As ILogger(Of DevController), env As IHostEnvironment)
            _logger = logger
            _env = env
        End Sub

        <HttpGet("reset")>
        Public Async Function Reset() As Task(Of IActionResult)
            If Not _env.IsDevelopment Then
                Return NotFound()
            End If

            _logger.LogInformation("Resetting development data.")
            Using scope = HttpContext.RequestServices.CreateScope()
                Dim db = scope.ServiceProvider.GetRequiredService(Of TripsDbContext)()
                Dim scheduling = scope.ServiceProvider.GetRequiredService(Of ISchedulingService)()
                Dim routes = scope.ServiceProvider.GetRequiredService(Of IRoutesClient)()

                Await db.TripSeats.ExecuteDeleteAsync()
                Await db.Trips.ExecuteDeleteAsync()
                Await db.ScheduleTemplates.ExecuteDeleteAsync()
                Await db.SaveChangesAsync()

                Await DevDataSeeder.EnsureSeededAsync(db, scheduling, routes, CancellationToken.None)
            End Using

            Return Ok(New With {.Message = "Development data reset and reseeded."})
        End Function
    End Class
End Namespace
