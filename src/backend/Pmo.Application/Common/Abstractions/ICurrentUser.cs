namespace Pmo.Application.Common.Abstractions;

/// <summary>The signed-in caller, read from the validated JWT. Never from the request body.</summary>
public interface ICurrentUser
{
    int Id { get; }
    string Role { get; }
    bool IsAdmin { get; }
    bool IsInRole(string role);
}
