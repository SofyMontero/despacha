using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Despacha.Api.Shared.Errors;

internal sealed class GlobalExceptionHandler(IProblemDetailsService problemDetails, ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        int status;
        string title;

        if (exception is DespachaException known)
        {
            status = known.StatusCode;
            title = known.Message;
            logger.LogWarning("Solicitud rechazada: {ErrorType}", exception.GetType().Name);
        }
        else
        {
            status = StatusCodes.Status500InternalServerError;
            title = "Ocurrió un error inesperado.";
            logger.LogError(exception, "Error no controlado");
        }

        httpContext.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails { Status = status, Title = title },
        });
    }
}
