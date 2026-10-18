namespace Pmo.Domain.Common;

/// <summary>
/// A business rule was broken. Code is a stable snake_case id the client can switch on
/// (e.g. "posted_immutable"). Maps to 409 by default, 403 when IsForbidden, 400 when IsValidation.
/// </summary>
public class DomainRuleException : Exception
{
    public string Code { get; }
    public bool IsForbidden { get; }
    public bool IsValidation { get; }

    public DomainRuleException(string code, string message, bool isForbidden = false, bool isValidation = false)
        : base(message)
    {
        if (isForbidden && isValidation)
        {
            throw new ArgumentException("A rule cannot be both forbidden and validation.");
        }

        Code = code;
        IsForbidden = isForbidden;
        IsValidation = isValidation;
    }
}
