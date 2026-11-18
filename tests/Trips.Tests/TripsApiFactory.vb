Imports Microsoft.AspNetCore.Mvc.Testing
Imports Microsoft.AspNetCore.Hosting
Imports Microsoft.Extensions.DependencyInjection
Imports Microsoft.Extensions.Hosting
Imports Microsoft.EntityFrameworkCore
Imports System
Imports System.Linq
Imports System.Threading
Imports System.Threading.Tasks
Imports Trips.Api
Imports Trips.Application
Imports Trips.Infrastructure
Imports Trips.Domain

Public Class TripsApiFactory
    Inherits WebApplicationFactory(Of ApiMarker)

    ' Un único nombre de base por fábrica: todos los requests comparten los mismos datos
    Private ReadOnly _dbName As String = "TripsTests-" & Guid.NewGuid().ToString()

    Protected Overrides Sub ConfigureWebHost(builder As IWebHostBuilder)
        builder.UseEnvironment("Testing")
        builder.ConfigureServices(Sub(services)
                                      ' Quitar cualquier registro previo del DbContext
                                      Dim toRemove = services.Where(Function(d) d.ServiceType = GetType(DbContextOptions(Of TripsDbContext)) OrElse d.ServiceType = GetType(TripsDbContext)).ToList()
                                      For Each d In toRemove
                                          services.Remove(d)
                                      Next
                                      Dim name As String = _dbName
                                      services.AddDbContext(Of TripsDbContext)(Sub(options) options.UseInMemoryDatabase(name))

                                      ' Quitar SeatHoldCleaner
                                      Dim cleaners = services.Where(Function(d) d.ServiceType = GetType(IHostedService) AndAlso d.ImplementationType IsNot Nothing AndAlso d.ImplementationType.Name = "SeatHoldCleaner").ToList()
                                      For Each d In cleaners
                                          services.Remove(d)
                                      Next

                                      ' Sembrar al arrancar el host
                                      services.AddSingleton(Of IHostedService, SeedHostedService)()
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

                db.ScheduleTemplates.Add(New ScheduleTemplate With {
                    .Id = Guid.NewGuid(),
                    .RouteId = Guid.NewGuid(),
                    .BusId = Guid.NewGuid(),
                    .Shift = Shift.Morning,
                    .DepartureTime = New TimeSpan(8, 0, 0),
                    .BasePrice = 10D,
                    .IsActive = True
                })
                Await db.SaveChangesAsync(cancellationToken)

                ' Hoy y mañana, así siempre hay un viaje aunque la hora de hoy ya haya pasado
                Dim request = New GenerateTripsRequest With {
                    .FechaDesde = DateOnly.FromDateTime(DateTime.UtcNow.Date),
                    .FechaHasta = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(1))
                }
                Await scheduling.GenerateTripsAsync(request, cancellationToken)
            End Using
        End Function

        Public Function StopAsync(cancellationToken As CancellationToken) As Task Implements IHostedService.StopAsync
            Return Task.CompletedTask
        End Function
    End Class
End Class