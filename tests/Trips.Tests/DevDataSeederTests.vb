Imports System.Threading
Imports Microsoft.EntityFrameworkCore
Imports Microsoft.Extensions.DependencyInjection
Imports Microsoft.Extensions.Logging
Imports Trips.Application
Imports Trips.Infrastructure
Imports Xunit

Public Class DevDataSeederTests

    Private Shared Function BuildProvider() As ServiceProvider
        Dim services As New ServiceCollection()
        services.AddLogging()
        Dim dbName As String = Guid.NewGuid().ToString()
        services.AddDbContext(Of TripsDbContext)(Sub(o) o.UseInMemoryDatabase(dbName))
        services.AddSingleton(Of IFareCalculator, FareCalculator)()
        services.AddSingleton(Of IRoutesClient, FakeRoutesClient)()
        services.AddScoped(Of ISchedulingService, SchedulingService)()
        Return services.BuildServiceProvider()
    End Function

    <Fact>
    Public Async Function EnsureSeededAsync_Creates15Templates() As Task
        Using provider = BuildProvider()
            Using scope = provider.CreateScope()
                Dim db = scope.ServiceProvider.GetRequiredService(Of TripsDbContext)()
                Dim scheduling = scope.ServiceProvider.GetRequiredService(Of ISchedulingService)()
                Dim routes = scope.ServiceProvider.GetRequiredService(Of IRoutesClient)()

                Await DevDataSeeder.EnsureSeededAsync(db, scheduling, routes, CancellationToken.None)

                Assert.Equal(15, Await db.ScheduleTemplates.CountAsync())
            End Using
        End Using
    End Function

    <Fact>
    Public Async Function EnsureSeededAsync_IsIdempotent() As Task
        Using provider = BuildProvider()
            Using scope = provider.CreateScope()
                Dim db = scope.ServiceProvider.GetRequiredService(Of TripsDbContext)()
                Dim scheduling = scope.ServiceProvider.GetRequiredService(Of ISchedulingService)()
                Dim routes = scope.ServiceProvider.GetRequiredService(Of IRoutesClient)()

                Await DevDataSeeder.EnsureSeededAsync(db, scheduling, routes, CancellationToken.None)
                Dim templates1 = Await db.ScheduleTemplates.CountAsync()
                Dim trips1 = Await db.Trips.CountAsync()

                Await DevDataSeeder.EnsureSeededAsync(db, scheduling, routes, CancellationToken.None)

                Assert.Equal(templates1, Await db.ScheduleTemplates.CountAsync())
                Assert.Equal(trips1, Await db.Trips.CountAsync())
            End Using
        End Using
    End Function

    <Fact>
    Public Async Function EnsureSeededAsync_GeneratesSevenDaysWith40SeatsEach() As Task
        Using provider = BuildProvider()
            Using scope = provider.CreateScope()
                Dim db = scope.ServiceProvider.GetRequiredService(Of TripsDbContext)()
                Dim scheduling = scope.ServiceProvider.GetRequiredService(Of ISchedulingService)()
                Dim routes = scope.ServiceProvider.GetRequiredService(Of IRoutesClient)()

                Await DevDataSeeder.EnsureSeededAsync(db, scheduling, routes, CancellationToken.None)

                Dim tripCount = Await db.Trips.CountAsync()
                Assert.Equal(15 * 7, tripCount)
                Assert.Equal(tripCount * 40, Await db.TripSeats.CountAsync())
            End Using
        End Using
    End Function
End Class
