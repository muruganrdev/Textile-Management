namespace SareeStore.Application.Common.Exceptions;

/// <summary>
/// Exception thrown when an authenticated user attempts an operation they are not authorized to perform.
/// Maps to HTTP 403 Forbidden.
/// </summary>
public class ForbiddenException : Exception
{
    public ForbiddenException(string message) : base(message)
    {
    }
}
