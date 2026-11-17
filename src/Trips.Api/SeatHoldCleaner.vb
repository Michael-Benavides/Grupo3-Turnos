Imports Microsoft.Extensions.DependencyInjection
Imports Microsoft.Extensions.Hosting
Imports Microsoft.Extensions.Logging
Imports Microsoft.EntityFrameworkCore
Imports Microsoft.Data.SqlClient
Imports System.Threading
Imports System.Threading.Tasks
Imports Trips.Application

Public Class SeatHoldCleaner
    Inherits BackgroundService

    Private ReadOnly _scopeFactory As IServiceScopeFactory
    Private ReadOnly _logger As ILogger(Of SeatHoldCleaner)

    Public Sub New(scopeFactory As IServiceScopeFactory, logger As ILogger(Of SeatHoldCleaner))
        _scopeFactory = scopeFactory
        _logger = logger
    End Sub

    Protected Overrides Async Function ExecuteAsync(stoppingToken As CancellationToken) As Task
        While Not stoppingToken.IsCancellationRequested
            Await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken)

            Using scope = _scopeFactory.CreateScope()
                Dim schedulingService = scope.ServiceProvider.GetRequiredService(Of ISchedulingService)()
                Try
                    Dim releasedCount = Await schedulingService.ReleaseExpiredSeatHoldsAsync(DateTime.UtcNow, stoppingToken)
                    If releasedCount > 0 Then
                        _logger.LogInformation("Se liberaron {ReleasedCount} bloqueos de asientos vencidos.", releasedCount)
                    End If
                Catch ex As SeatConcurrencyException
                    _logger.LogWarning(ex, "La limpieza encontró cambios concurrentes; se reintentará en el siguiente ciclo.")
                Catch ex As DbUpdateException
                    _logger.LogError(ex, "No se pudieron guardar los cambios de la limpieza de bloqueos.")
                Catch ex As SqlException
                    _logger.LogError(ex, "No se pudo acceder a SQL Server durante la limpieza de bloqueos.")
                End Try
            End Using
        End While
    End Function
End Class
