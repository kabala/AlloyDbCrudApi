using AlloyDbCrudApi.Domain.Enums;

namespace AlloyDbCrudApi.Application.Contracts.Customers;

public class CustomerDto
{
    public string CustomerId { get; set; } = string.Empty;
    public int Age { get; set; }
    public Gender Gender { get; set; }
    public string City { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public class CreateCustomerRequest
{
    public string CustomerId { get; set; } = string.Empty;
    public int Age { get; set; }
    public Gender Gender { get; set; } = Gender.Unspecified;
    public string City { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public class CustomerListQuery
{
    public string? City { get; set; }
    public Gender? Gender { get; set; }
    public bool IncludeInactive { get; set; } = false;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}
