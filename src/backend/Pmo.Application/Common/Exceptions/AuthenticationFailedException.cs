namespace Pmo.Application.Common.Exceptions;

/// <summary>Sign-in or refresh failed. The message is deliberately generic (no "user exists" hints). Maps to 401.</summary>
public class AuthenticationFailedException : Exception
{
    public AuthenticationFailedException()
        : base("Invalid email or password.")
    {
    }
}
