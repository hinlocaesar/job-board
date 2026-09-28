using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using JobBoard.Api.Common;
using JobBoard.Api.Features.Auth;
using JobBoard.Api.Options;
using JobBoard.Api.Services;

namespace JobBoard.Api.Common;

/// <summary>Shared plumbing for every API controller (validation, identity, client IP).</summary>
[ApiController]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    protected Guid CurrentUserId => User.GetUserId();
    protected string? ClientIp => HttpContext.GetIp();

    /// <summary>Resolves <c>IValidator&lt;T&gt;</c> from DI and throws FluentValidation's <see cref="ValidationException"/> (mapped to 400 by the middleware).</summary>
    protected async Task ValidateAsync<T>(T instance) where T : class
    {
        var validator = HttpContext.RequestServices.GetRequiredService<IValidator<T>>();
        await validator.ValidateAndThrowAsync(instance);
    }
}
