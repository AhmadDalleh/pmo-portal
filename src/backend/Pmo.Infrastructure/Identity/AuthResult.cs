using Microsoft.AspNetCore.Identity;
using Pmo.Application.Common;
using Pmo.Application.Common.Abstractions;
using Pmo.Application.Common.Exceptions;
using Pmo.Application.Features.Auth;
using Pmo.Domain.Governance;
using Pmo.Infrastructure.Persistence;

namespace Pmo.Infrastructure.Identity;

/// <summary>What the controller needs to answer a sign-in: the JSON body and the cookie to set.</summary>
public record AuthResult(LoginResponse Body, string RefreshCookieValue, DateTimeOffset RefreshExpiresAt);
