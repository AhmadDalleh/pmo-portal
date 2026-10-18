namespace Pmo.Application.Common.Exceptions;

/// <summary>The resource is in scope but the caller may not perform this action. Maps to 403.</summary>
public class ForbiddenException : Exception
{
    public ForbiddenException(string message)
        : base(message)
    {
    }
}
