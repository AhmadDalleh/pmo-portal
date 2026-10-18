namespace Pmo.Application.Features.Auth;

/// <summary>What the Angular app needs after sign-in. The refresh token is NOT here: it travels in an HttpOnly cookie.</summary>
public record LoginResponse(string AccessToken, int ExpiresInSeconds, MeDto User);
