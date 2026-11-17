Imports Microsoft.AspNetCore.Http
Imports System.Threading.Tasks
Imports System

Public Class CorrelationIdMiddleware
    Private ReadOnly _next As RequestDelegate
    Private Const HeaderName As String = "X-Correlation-Id"

    Public Sub New(nextDelegate As RequestDelegate)
        _next = nextDelegate
    End Sub

    Public Async Function InvokeAsync(context As HttpContext) As Task
        Dim correlationId As String = context.Request.Headers(HeaderName).FirstOrDefault()
        If String.IsNullOrWhiteSpace(correlationId) Then
            correlationId = Guid.NewGuid().ToString()
        End If
        ' Add to response header
        context.Response.Headers(HeaderName) = correlationId
        ' Enrich Serilog log context
        Serilog.Context.LogContext.PushProperty("CorrelationId", correlationId)
        Await _next(context)
    End Function
End Class
