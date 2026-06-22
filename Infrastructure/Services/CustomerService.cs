using AlloyDbCrudApi.Application.Abstractions;
using AlloyDbCrudApi.Application.Contracts.Common;
using AlloyDbCrudApi.Application.Contracts.Customers;
using AlloyDbCrudApi.Domain.Entities;
using AlloyDbCrudApi.Domain.Exceptions;
using AlloyDbCrudApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AlloyDbCrudApi.Infrastructure.Services;

public class CustomerService : ICustomerService
{
    private readonly AppDbContext _db;
    private readonly IAuditService _audit;

    public CustomerService(AppDbContext db, IAuditService audit) { _db = db; _audit = audit; }

    public async Task<PagedResult<CustomerDto>> ListAsync(CustomerListQuery q, CancellationToken ct = default)
    {
        var qry = _db.Customers.AsNoTracking();
        if (!q.IncludeInactive) qry = qry.Where(c => c.IsActive);
        if (!string.IsNullOrWhiteSpace(q.City)) qry = qry.Where(c => c.City == q.City);
        if (q.Gender.HasValue) qry = qry.Where(c => c.Gender == q.Gender);

        var total = await qry.CountAsync(ct);
        var items = await qry.OrderBy(c => c.CustomerId)
            .Skip((q.Page - 1) * q.PageSize).Take(q.PageSize)
            .Select(c => new CustomerDto
            {
                CustomerId = c.CustomerId, Age = c.Age, Gender = c.Gender, City = c.City, Email = c.Email, IsActive = c.IsActive,
            })
            .ToListAsync(ct);
        return new PagedResult<CustomerDto> { Items = items, Total = total, Page = q.Page, PageSize = q.PageSize };
    }

    public async Task<CustomerDto?> GetAsync(string customerId, CancellationToken ct = default)
    {
        return await _db.Customers.AsNoTracking()
            .Where(c => c.CustomerId == customerId)
            .Select(c => new CustomerDto
            {
                CustomerId = c.CustomerId, Age = c.Age, Gender = c.Gender, City = c.City, Email = c.Email, IsActive = c.IsActive,
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<CustomerDto> CreateAsync(CreateCustomerRequest req, CancellationToken ct = default)
    {
        if (await _db.Customers.AnyAsync(c => c.CustomerId == req.CustomerId, ct))
            throw new DomainException($"Customer '{req.CustomerId}' already exists.");

        var customer = new Customer
        {
            CustomerId = req.CustomerId,
            Age = req.Age,
            Gender = req.Gender,
            City = req.City,
            Email = string.IsNullOrWhiteSpace(req.Email) ? "unknown@local" : req.Email,
        };
        customer.ValidateProfile();
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("customer.create", "Customer", customer.CustomerId, $"Created {customer.City}", ct);
        return new CustomerDto
        {
            CustomerId = customer.CustomerId, Age = customer.Age, Gender = customer.Gender, City = customer.City, Email = customer.Email, IsActive = customer.IsActive,
        };
    }
}
