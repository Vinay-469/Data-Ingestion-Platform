using System.Text.Json;

namespace Backend_Tool;

public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger, IWebHostEnvironment environment)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = 499;
                context.Response.ContentType = "application/json; charset=utf-8";
                await context.Response.WriteAsync(JsonSerializer.Serialize(new { statusCode = 499, message = "The request was cancelled.", traceId = context.TraceIdentifier }, JsonOptions));
            }
            logger.LogInformation("Request was cancelled by the client. TraceId: {TraceId}", context.TraceIdentifier);
        }
        catch (Exception exception)
        {
            await HandleExceptionAsync(context, exception);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        logger.LogError(exception, "Unhandled exception. TraceId: {TraceId}, Path: {Path}", context.TraceIdentifier, context.Request.Path);

        if (context.Response.HasStarted)
        {
            logger.LogWarning("The response had already started; the exception response could not be rewritten. TraceId: {TraceId}", context.TraceIdentifier);
            throw exception;
        }

        var (statusCode, message) = exception switch
        {
            InvalidDataException => (StatusCodes.Status400BadRequest, exception.Message),
            ArgumentException => (StatusCodes.Status400BadRequest, exception.Message),
            KeyNotFoundException => (StatusCodes.Status404NotFound, "The requested resource was not found."),
            UnauthorizedAccessException => (StatusCodes.Status403Forbidden, "You are not authorized to perform this operation."),
            InvalidOperationException => (StatusCodes.Status503ServiceUnavailable, exception.Message),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred while processing the request.")
        };

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";

        var response = new
        {
            statusCode,
            message,
            traceId = context.TraceIdentifier,
            detail = environment.IsDevelopment() ? exception.Message : null
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response, JsonOptions));
    }
}
