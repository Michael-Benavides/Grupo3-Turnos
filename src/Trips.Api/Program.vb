Imports Microsoft.AspNetCore.Builder
Imports Microsoft.AspNetCore.Http
Imports Microsoft.Extensions.Configuration
Imports Microsoft.Extensions.DependencyInjection
Imports Microsoft.EntityFrameworkCore
Imports Microsoft.Extensions.Hosting
Imports System.Text.Json.Serialization
Imports Trips.Application
Imports Trips.Infrastructure

Module Program
    Sub Main(args As String())
        Dim builder = WebApplication.CreateBuilder(args)
        builder.Services.AddControllers().AddJsonOptions(
            Sub(options)
                options.JsonSerializerOptions.Converters.Add(New JsonStringEnumConverter())
            End Sub)
        builder.Services.AddEndpointsApiExplorer()
        builder.Services.AddSwaggerGen()
        builder.Services.AddProblemDetails()
        builder.Services.AddExceptionHandler(Of SchedulingExceptionHandler)()
        Dim configureDatabase As New Action(Of DbContextOptionsBuilder)(
            Sub(options)
                options.UseSqlServer(builder.Configuration.GetConnectionString("TripsDb"))
            End Sub)
        builder.Services.AddDbContext(Of TripsDbContext)(configureDatabase)
        builder.Services.AddScoped(Of ISchedulingService, SchedulingService)()
        builder.Services.AddSingleton(Of IFareCalculator, FareCalculator)()
        builder.Services.AddHostedService(Of SeatHoldCleaner)()

        If builder.Configuration.GetValue(Of Boolean)("Routes:UseFakeClient", True) Then
            builder.Services.AddSingleton(Of IRoutesClient, FakeRoutesClient)()
        Else
            Dim routesBaseUrl = builder.Configuration("Routes:BaseUrl")
            If String.IsNullOrWhiteSpace(routesBaseUrl) Then
                Throw New InvalidOperationException("Routes:BaseUrl es obligatorio al desactivar Routes:UseFakeClient.")
            End If

            builder.Services.AddHttpClient(Of IRoutesClient, HttpRoutesClient)(
                Sub(client)
                    client.BaseAddress = New Uri(routesBaseUrl)
                End Sub)
        End If

        Dim app = builder.Build()
        If app.Environment.IsDevelopment() Then
            Using scope = app.Services.CreateScope()
                Dim dbContext = scope.ServiceProvider.GetRequiredService(Of TripsDbContext)()
                dbContext.Database.MigrateAsync().GetAwaiter().GetResult()
            End Using
        End If

        app.UseExceptionHandler()
        app.UseSwagger()
        app.UseSwaggerUI()

        app.MapGet("/health", Function() Results.Ok(New With {
            .status = "ok",
            .service = "trips"
        }))
        app.MapControllers()

        app.Run()
    End Sub
End Module
