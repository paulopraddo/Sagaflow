using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Boilerplate.API;

/// <summary>
/// Captura qualquer exceção não tratada que escape dos handlers/controllers e devolve uma
/// resposta JSON consistente (RFC 7807), em vez do 500 vazio padrão do Kestrel.
/// </summary>
public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IHostEnvironment environment) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException)
        {
            // Cliente cancelou/fechou a conexão (navegação, timeout) — não é uma falha do servidor.
            return true;
        }

        var (statusCode, title) = MapException(exception);

        logger.Log(
            statusCode >= 500 ? LogLevel.Error : LogLevel.Warning,
            exception,
            "Requisição {Method} {Path} falhou com status {StatusCode}",
            httpContext.Request.Method,
            httpContext.Request.Path,
            statusCode);

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = environment.IsDevelopment()
                ? exception.ToString()
                : "Ocorreu um erro inesperado. Tente novamente mais tarde.",
            Instance = httpContext.Request.Path,
        };
        problem.Extensions["traceId"] = httpContext.TraceIdentifier;

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/problem+json";
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);

        return true;
    }

    private static (int StatusCode, string Title) MapException(Exception exception) => exception switch
    {
        DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "Conflito ao salvar os dados."),
        DbUpdateException => (StatusCodes.Status409Conflict, "Não foi possível salvar os dados."),
        UnauthorizedAccessException => (StatusCodes.Status403Forbidden, "Acesso negado."),
        _ => (StatusCodes.Status500InternalServerError, "Erro interno do servidor."),
    };
}
