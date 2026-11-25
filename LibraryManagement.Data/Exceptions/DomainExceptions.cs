namespace LibraryManagement.Data.Exceptions;

public class ResourceNotFoundException : Exception
{
    public ResourceNotFoundException(string message) : base(message) { }
}

public class DuplicateResourceException : Exception
{
    public DuplicateResourceException(string message) : base(message) { }
}

public class ReferentialIntegrityException : Exception
{
    public ReferentialIntegrityException(string message) : base(message) { }
}

public class BusinessRuleException : Exception
{
    public BusinessRuleException(string message) : base(message) { }
}
