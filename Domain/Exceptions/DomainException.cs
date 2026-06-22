namespace AlloyDbCrudApi.Domain.Exceptions;

public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
    public DomainException(string message, Exception inner) : base(message, inner) { }
}

public class InsufficientStockException : DomainException
{
    public string ProductId { get; }
    public string StoreId { get; }
    public int Requested { get; }
    public int Available { get; }

    public InsufficientStockException(string productId, string storeId, int requested, int available)
        : base($"Insufficient stock for product '{productId}' at store '{storeId}': requested {requested}, available {available}.")
    {
        ProductId = productId;
        StoreId = storeId;
        Requested = requested;
        Available = available;
    }
}

public class InvalidDiscountException : DomainException
{
    public InvalidDiscountException(string message) : base(message) { }
}

public class InvalidProductException : DomainException
{
    public InvalidProductException(string message) : base(message) { }
}

public class InvalidReturnException : DomainException
{
    public InvalidReturnException(string message) : base(message) { }
}
