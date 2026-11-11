namespace SareeStore.Application.Common.Exceptions;

public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message)
    {
    }

    public NotFoundException(string entityName, object key)
        : base($"Entity '{entityName}' with identifier '{key}' was not found.")
    {
    }
}
