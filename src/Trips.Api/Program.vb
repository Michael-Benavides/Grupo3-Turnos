Imports Microsoft.AspNetCore.Builder
Imports Microsoft.AspNetCore.Http
Imports Microsoft.Extensions.Configuration
Imports Microsoft.Extensions.DependencyInjection
Imports Microsoft.EntityFrameworkCore
Imports Microsoft.Extensions.Hosting
Imports Microsoft.Extensions.Diagnostics.HealthChecks
Imports System.Text.Json.Serialization
Imports System.Reflection
Imports Trips.Application
Imports Trips.Infrastructure
Imports FluentValidation
Imports FluentValidation.DependencyInjectionExtensions
Imports Serilog
Imports Serilog.AspNetCore

Module Program
    Sub Main(args As String())
        Dim builder = WebApplication.CreateBuilder(args)

        ' Services
        builder.Services.AddControllers() _
            .AddJsonOptions(Sub(options)
                options.JsonSerializerOptions.Converters.Add(New JsonStringEnumConverter())
            End Sub)

        ' FluentValidation (DI extensions)
        builder.Services.AddValidatorsFromAssembly(GetType(CreateScheduleRequest).Assembly)

        ' CORS
        builder.Services.AddCors(Sub(options)
            options.AddPolicy("Default", Sub(policy)
                Dim allowed = builder.Configuration.GetSection("Cors:AllowedOrigins").Get(Of String())()
                If allowed Is Nothing OrElse allowed.Length = 0 Then
                    allowed = New String() {"http://localhost:5173", "http://localhost:3000"}
                End If
                policy.WithOrigins(allowed).AllowAnyHeader().AllowAnyMethod()
            End Sub)
        End Sub)

        ' Health checks
        builder.Services.AddHealthChecks() _
            .AddDbContextCheck(Of TripsDbContext)("Database", tags:=New String() {"ready"})

        ' Serilog configuration (read from appsettings.json)
        Log.Logger = New LoggerConfiguration() _
            .ReadFrom.Configuration(builder.Configuration) _
            .Enrich.FromLogContext() _
            .Enrich.WithProperty("Application", "Trips.Api") _
            .CreateLogger()

        builder.Host.UseSerilog()

        builder.Services.AddEndpointsApiExplorer()
        builder.Services.AddSwaggerGen()
        builder.Services.AddProblemDetails()
        builder.Services.AddExceptionHandler(Of SchedulingExceptionHandler)()

        Dim configureDatabase As New Action(Of DbContextOptionsBuilder)(Sub(options)
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
            builder.Services.AddHttpClient(Of IRoutesClient, HttpRoutesClient)(Sub(client)
                client.BaseAddress = New Uri(routesBaseUrl)
            End Sub)
        End If

        Dim app = builder.Build()

        ' Middleware pipeline
        app.UseSerilogRequestLogging()
        app.UseCors("Default")
        app.UseMiddleware(Of CorrelationIdMiddleware)()
        app.UseExceptionHandler()
        app.UseSwagger()
        app.UseSwaggerUI()

        ' Health endpoints
        app.MapHealthChecks("/health")
        app.MapHealthChecks("/health/ready", New Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions With {.Predicate = Function(check) check.Tags.Contains("ready")})

        ' API version prefix handled by controllers' [Route] attributes
        app.MapControllers()

        app.Run()
    End Sub
End Module