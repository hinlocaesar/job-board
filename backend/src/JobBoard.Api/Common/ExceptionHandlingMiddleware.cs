using JobBoard.Api.Common;
using Microsoft.AspNetCore.Mvc;

namespace JobBoard.Api.Common;

/// <summary>Maps exceptions (ApiException / ValidationException / anything else) to problem-details JSON.</summary>
public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger, IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (FluentValidation.ValidationException validation)
        {
            var errors = validation.Errors
                .GroupBy(e => string.IsNullOrEmpty(e.PropertyName) ? "request" : e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            await WriteAsync(context, StatusCodes.Status400BadRequest, "validation_failed", "One or more fields are invalid.", errors);
        }
        catch (ApiException api)
        {
            await WriteAsync(context, (int)api.StatusCode, api.Code, api.Message, api.Errors);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);
            var detail = _environment.IsDevelopment() ? exception.Message : "An unexpected error occurred.";
            await WriteAsync(context, StatusCodes.Status500InternalServerError, "server_error", detail);
        }
    }

    private static async Task WriteAsync(HttpContext context, int status, string code, string message, IDictionary<string, string[]>? errors = null)
    {
        if (context.Response.HasStarted)
            return;

        context.Response.StatusCode = status;
        // WriteAsJsonAsync overwrites Response.ContentType unless the media type is
        // passed explicitly — keep RFC 9457 problem+json intact.
        await context.Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Status = status,
                Title = message,
                Detail = message,
                Extensions =
                {
                    ["code"] = code,
                    ["errors"] = errors,
                },
            },
            typeof(ProblemDetails),
            options: null,
            contentType: "application/problem+json");
    }
}
