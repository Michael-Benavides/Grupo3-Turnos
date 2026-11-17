Imports Microsoft.AspNetCore.Mvc.Testing
Imports Microsoft.AspNetCore.Hosting
Imports Microsoft.Extensions.DependencyInjection
Imports Microsoft.Extensions.Hosting
Imports Microsoft.EntityFrameworkCore
Imports System
Imports System.Linq
Imports System.Threading
Imports Trips.Api
Imports Trips.Application
Imports Trips.Infrastructure
Imports Trips.Domain

Public Class TripsApiFactory
    Inherits WebApplicationFactory(Of ApiMarker)

    Protected Overrides Sub ConfigureWebHost(builder As IWebHostBuilder)
        builder.UseEnvironment("Testing")
        builder.ConfigureServices(Sub(services)
            '--- Replace DbContext with InMemory (unique name) ---
            Dim dbDescriptor = services.FirstOrDefault(Function(d) d.ServiceType = GetType(TripsDbContext))
            If dbDescriptor IsNot Nothing Then services.Remove(dbDescriptor)
            services.AddDbContext(Of TripsDbContext)(Function(options) options.UseInMemoryDatabase(Guid.NewGuid().ToString()))

            '--- Remove SeatHoldCleaner hosted service ---
            Dim shd = services.FirstOrDefault(Function(d) d.ServiceType = GetType(IHostedService) AndAlso d.ImplementationType?.Name = "SeatHoldCleaner")
            If shd IsNot Nothing Then services.Remove(shd)

            '--- Add seeder hosted service that runs once on start ---
            services.AddSingleton(Of IHostedService)(Function(sp) New SeedHostedService(sp))
        End Sub)
    End Sub

    Private Class SeedHostedService
        Implements IHostedService

        Private ReadOnly _provider As IServiceProvider

        Public Sub New(provider As IServiceProvider)
            _provider = provider
        End Sub

        Public Async Function StartAsync(cancellationToken As CancellationToken) As Task Implements IHostedService.StartAsync
            Using scope = _provider.CreateScope()
                Dim db = scope.ServiceProvider.GetRequiredService(Of TripsDbContext)()
                Dim scheduling = scope.ServiceProvider.GetRequiredService(Of ISchedulingService)()
                Dim routes = scope.ServiceProvider.GetRequiredService(Of IRoutesClient)()

                '--- Create a single schedule template ---
                Dim routeId = Guid.NewGuid()
                Dim busId = Guid.NewGuid()
                db.ScheduleTemplates.Add(New ScheduleTemplate With {
                    .Id = Guid.NewGuid(),
                    .RouteId = routeId,
                    .BusId = busId,
                    .Shift = Shift.Morning,
                    .DepartureTime = New TimeSpan(8, 0, 0),
                    .BasePrice = 10D,
                    .IsActive = True
                })
                Await db.SaveChangesAsync()

                '--- Generate trips for today ---
                Dim request = New GenerateTripsRequest With {
                    .FechaDesde = DateOnly.FromDateTime(DateTime.UtcNow.Date),
                    .FechaHasta = DateOnly.FromDateTime(DateTime.UtcNow.Date)
                }
                Await scheduling.GenerateTripsAsync(request, CancellationToken.None)
            End Using
            ' No explicit return needed for async Task
        End Function

        Public Function StopAsync(cancellationToken As CancellationToken) As Task Implements IHostedService.StopAsync
            Return Task.CompletedTask
        End Function
    End Class
End Class
