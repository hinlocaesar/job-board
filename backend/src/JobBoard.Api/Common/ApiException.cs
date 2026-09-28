using System.Net;

namespace JobBoard.Api.Common;

/// <summary>Application-level error that the exception middleware turns into a RFC 7807 response.</summary>
public sealed class ApiException : Exception
{
    public HttpStatusCode StatusCode { get; }
    public string Code { get; }
    public IDictionary<string, string[]>? Errors { get; }

    public ApiException(HttpStatusCode statusCode, string code, string message, IDictionary<string, string[]>? errors = null)
        : base(message)
    {
        StatusCode = statusCode;
        Code = code;
        Errors = errors;
    }

    public static ApiException NotFound(string what) => new(HttpStatusCode.NotFound, "not_found", $"{what} was not found.");
    public static ApiException Forbidden(string? message = null) => new(HttpStatusCode.Forbidden, "forbidden", message ?? "You do not have permission to perform this action.");
    public static ApiException BadRequest(string code, string message, IDictionary<string, string[]>? errors = null) => new(HttpStatusCode.BadRequest, code, message, errors);
    public static ApiException Unauthorized(string code, string message) => new(HttpStatusCode.Unauthorized, code, message);
    public static ApiException Conflict(string code, string message) => new(HttpStatusCode.Conflict, code, message);
    public static ApiException TooManyRequests(string message) => new((HttpStatusCode)429, "rate_limited", message);
}

public static class Roles
{
    public const string Admin = "admin";
    public const string Employer = "employer";
    public const string Worker = "worker";
    public static readonly string[] All = [Admin, Employer, Worker];
    public static readonly string[] Signup = [Employer, Worker];
}
