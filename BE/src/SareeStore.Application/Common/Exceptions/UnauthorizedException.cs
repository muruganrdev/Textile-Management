namespace SareeStore.Application.Common.Exceptions;

/// <summary>
/// Exception thrown when authentication or credential verification fails.
/// </summary>
public class UnauthorizedException : Exception
{
    public UnauthorizedException(string message = "Invalid email or password.")
        : base(message)
    {
    }
}
