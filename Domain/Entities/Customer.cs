using AlloyDbCrudApi.Domain.Enums;
using AlloyDbCrudApi.Domain.Exceptions;

namespace AlloyDbCrudApi.Domain.Entities;

public class Customer
{
    public string CustomerId { get; set; } = string.Empty;
    public int Age { get; set; }
    public Gender Gender { get; set; } = Gender.Unspecified;
    public string City { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }

    public List<Sale> Sales { get; set; } = new();

    public void ValidateProfile()
    {
        if (Age < 0 || Age > 130)
            throw new DomainException($"Customer '{CustomerId}' has invalid age {Age}.");
        if (string.IsNullOrWhiteSpace(City))
            throw new DomainException($"Customer '{CustomerId}' has empty city.");
        if (string.IsNullOrWhiteSpace(Email))
            Email = "unknown@local";
    }

    public void SoftDelete()
    {
        IsActive = false;
        DeletedAt = DateTime.UtcNow;
    }
}
