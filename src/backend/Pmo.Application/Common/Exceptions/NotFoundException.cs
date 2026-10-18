namespace Pmo.Application.Common.Exceptions;

/// <summary>The resource does not exist or is outside the caller's scope. Maps to 404.</summary>
public class NotFoundException : Exception
{
    public NotFoundException(string resource, object key)
        : base($"{resource} '{key}' was not found.")
    {
    }
}
