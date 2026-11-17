Imports System.Threading
Imports Microsoft.AspNetCore.Builder
Imports Microsoft.AspNetCore.Cors.Infrastructure
Imports Microsoft.AspNetCore.Http
Imports Microsoft.Extensions.Configuration
Imports Microsoft.Extensions.DependencyInjection
Imports Microsoft.EntityFrameworkCore
Imports Microsoft.Extensions.Hosting
Imports Microsoft.Extensions.Diagnostics.HealthChecks
Imports System.Text.Json.Serialization
Imports Trips.Application
Imports Trips.Infrastructure
Imports FluentValidation
Imports FluentValidation.DependencyInjectionExtensions
Imports Serilog
Imports Serilog.AspNetCore

Module Program

    ' CORS: abierto en Development, restringido por configuración en otros entornos
    Private Sub ConfigureCorsPolicy(policy As CorsPolicyBuilder, isDev As Boolean, configuration As IConfiguration)
        If isDev Then
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()
            Return
        End If

        Dim allowed = configuration.GetSection("Cors:AllowedOrigins").Get(Of String())()
        If allowed Is Nothing OrElse allowed.Length = 0 Then
            allowed = New String() {"http://localhost:5173", "http://localhost:3000"}
        End If
        policy.WithOrigins(allowed).AllowAnyHeader().AllowAnyMethod()
    End Sub

    ' Datos de demostración (solo Development)
    Private Sub SeedDevelopmentData(app As WebApplication)
        Using scope = app.Services.CreateScope()
            Dim db = scope.ServiceProvider.GetRequiredService(Of TripsDbContext)()
            db.Database.Migrate()
            Dim scheduling = scope.ServiceProvider.GetRequiredService(Of ISchedulingService)()
            Dim routes = scope.ServiceProvider.GetRequiredService(Of IRoutesClient)()
            DevDataSeeder.EnsureSeededAsync(db, scheduling, routes, CancellationToken.None).GetAwaiter().GetResult()
        End Using
    End Sub

    Sub Main(args As String())
        Dim builder = WebApplication.CreateBuilder(args)
        Dim isDev As Boolean = builder.Environment.IsDevelopment()

        ' Serilog (configuración desde appsettings.json)
        Log.Logger = New LoggerConfiguration() _
            .ReadFrom.Configuration(builder.Configuration) _
            .Enrich.FromLogContext() _
            .Enrich.WithProperty("Application", "Trips.Api") _
            .CreateLogger()
        builder.Host.UseSerilog()

        ' Controladores y JSON
        builder.Services.AddControllers() _
            .AddJsonOptions(Sub(options) options.JsonSerializerOptions.Converters.Add(New JsonStringEnumConverter()))

        ' Validaciones
        builder.Services.AddValidatorsFromAssembly(GetType(CreateScheduleRequest).Assembly)

        ' CORS
        builder.Services.AddCors(Sub(options) options.AddPolicy("Default", Sub(policy) ConfigureCorsPolicy(policy, isDev, builder.Configuration)))

        ' Health checks
        builder.Services.AddHealthChecks() _
            .AddDbContextCheck(Of TripsDbContext)("Database", tags:=New String() {"ready"})

        builder.Services.AddEndpointsApiExplorer()
        builder.Services.AddSwaggerGen()
        builder.Services.AddProblemDetails()
        builder.Services.AddExceptionHandler(Of SchedulingExceptionHandler)()

        ' Base de datos y servicios
        Dim configureDatabase As New Action(Of DbContextOptionsBuilder)(Sub(options) options.UseSqlServer(builder.Configuration.GetConnectionString("TripsDb")))
        If Not builder.Environment.IsEnvironment("Testing") Then
    builder.Services.AddDbContext(Of TripsDbContext)(configureDatabase)
End If
        builder.Services.AddScoped(Of ISchedulingService, SchedulingService)()
        builder.Services.AddSingleton(Of IFareCalculator, FareCalculator)()
        builder.Services.AddHostedService(Of SeatHoldCleaner)()

        ' Cliente de Routes (falso o real)
        If builder.Configuration.GetValue(Of Boolean)("Routes:UseFakeClient", True) Then
            builder.Services.AddSingleton(Of IRoutesClient, FakeRoutesClient)()
        Else
            Dim routesBaseUrl = builder.Configuration("Routes:BaseUrl")
            If String.IsNullOrWhiteSpace(routesBaseUrl) Then
                Throw New InvalidOperationException("Routes:BaseUrl es obligatorio al desactivar Routes:UseFakeClient.")
            End If
            builder.Services.AddHttpClient(Of IRoutesClient, HttpRoutesClient)(Sub(client) client.BaseAddress = New Uri(routesBaseUrl))
        End If

        Dim app = builder.Build()

        ' Pipeline
        app.UseSerilogRequestLogging()
        app.UseCors("Default")
        app.UseMiddleware(Of CorrelationIdMiddleware)()
        app.UseExceptionHandler()
        app.UseSwagger()
        app.UseSwaggerUI()

        app.MapHealthChecks("/health")
        app.MapHealthChecks("/health/ready", New Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions With {.Predicate = Function(check) check.Tags.Contains("ready")})
        app.MapControllers()

        If app.Environment.IsDevelopment() Then
            SeedDevelopmentData(app)
        End If

        app.Run()
    End Sub

End Module