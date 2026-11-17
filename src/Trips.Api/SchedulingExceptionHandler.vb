Imports Microsoft.AspNetCore.Diagnostics
Imports Microsoft.AspNetCore.Http
Imports Microsoft.AspNetCore.Mvc
Imports Microsoft.EntityFrameworkCore
Imports System.Text.Json
Imports System.Threading
Imports System.Threading.Tasks
Imports Trips.Application
Imports Trips.Domain

Public Class SchedulingExceptionHandler
    Implements IExceptionHandler

    Public Function TryHandleAsync(
        httpContext As HttpContext,
        exception As Exception,
        cancellationToken As CancellationToken) As ValueTask(Of Boolean) Implements IExceptionHandler.TryHandleAsync

        Dim statusCode As Integer
        If TypeOf exception Is SeatConcurrencyException OrElse TypeOf exception Is DbUpdateConcurrencyException Then
            statusCode = StatusCodes.Status409Conflict
        ElseIf TypeOf exception Is DomainException Then
            statusCode = StatusCodes.Status422UnprocessableEntity
        ElseIf TypeOf exception Is RequestValidationException Then
            statusCode = StatusCodes.Status400BadRequest
        ElseIf TypeOf exception Is ResourceNotFoundException Then
            statusCode = StatusCodes.Status404NotFound
        Else
            Return New ValueTask(Of Boolean)(False)
        End If

        Dim problem = New ProblemDetails With {
            .Status = statusCode,
            .Title = GetTitle(statusCode),
            .Detail = exception.Message,
            .Instance = httpContext.Request.Path
        }
        Return New ValueTask(Of Boolean)(WriteProblemAsync(httpContext, problem, statusCode, cancellationToken))
    End Function

    Private Shared Async Function WriteProblemAsync(
        httpContext As HttpContext,
        problem As ProblemDetails,
        statusCode As Integer,
        cancellationToken As CancellationToken) As Task(Of Boolean)
        httpContext.Response.StatusCode = statusCode
        httpContext.Response.ContentType = "application/problem+json"
        Await httpContext.Response.WriteAsync(JsonSerializer.Serialize(problem), cancellationToken)
        Return True
    End Function

    Private Shared Function GetTitle(statusCode As Integer) As String
        Select Case statusCode
            Case StatusCodes.Status404NotFound
                Return "Recurso no encontrado"
            Case StatusCodes.Status409Conflict
                Return "Conflicto de concurrencia"
            Case StatusCodes.Status422UnprocessableEntity
                Return "Regla de negocio violada"
            Case Else
                Return "Solicitud inválida"
        End Select
    End Function
End Class
