using System.Net;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using LibraryManagement.Data.Exceptions;
using LibraryManagement.Models.Responses;

namespace LibraryManagement.API.Middleware;

public sealed class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception ex)
    {
        var (statusCode, message) = ex switch
        {
            ResourceNotFoundException => (HttpStatusCode.NotFound, ex.Message),
            DuplicateResourceException => (HttpStatusCode.Conflict, ex.Message),
            ReferentialIntegrityException => (HttpStatusCode.Conflict, ex.Message),
            BusinessRuleException => (HttpStatusCode.UnprocessableEntity, ex.Message),
            SqlException => (HttpStatusCode.InternalServerError, "A database error occurred while processing the request."),
            _ => (HttpStatusCode.InternalServerError, "An unexpected server error occurred.")
        };

        if ((int)statusCode >= 500)
        {
            _logger.LogError(ex, "Unhandled exception on {Method} {Path} (TraceId: {TraceId})",
                context.Request.Method, context.Request.Path, context.TraceIdentifier);
        }
        else
        {
            _logger.LogWarning("Domain error ({Status}) on {Method} {Path}: {Message}",
                (int)statusCode, context.Request.Method, context.Request.Path, ex.Message);
        }

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var payload = new ApiErrorResponse
        {
            Success = false,
            Message = message,
            TraceId = context.TraceIdentifier
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }));
    }
}
