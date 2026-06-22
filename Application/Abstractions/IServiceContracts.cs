using AlloyDbCrudApi.Application.Contracts.Auth;
using AlloyDbCrudApi.Application.Contracts.Customers;
using AlloyDbCrudApi.Application.Contracts.Common;
using AlloyDbCrudApi.Application.Contracts.Inventory;
using AlloyDbCrudApi.Application.Contracts.Products;
using AlloyDbCrudApi.Application.Contracts.Returns;
using AlloyDbCrudApi.Application.Contracts.Sales;
using AlloyDbCrudApi.Application.Contracts.Users;
using AlloyDbCrudApi.Domain.Enums;

namespace AlloyDbCrudApi.Application.Abstractions;

public interface IAuthService
{
    Task<TokenResponse> LoginAsync(LoginRequest req, CancellationToken ct = default);
    Task<TokenResponse> RefreshAsync(RefreshRequest req, CancellationToken ct = default);
}

public interface IUserService
{
    Task<PagedResult<UserDto>> ListAsync(int page, int pageSize, CancellationToken ct = default);
    Task<UserDto> CreateAsync(CreateUserRequest req, CancellationToken ct = default);
    Task<UserDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
}

public interface IProductService
{
    Task<PagedResult<ProductDto>> ListAsync(ProductListQuery q, CancellationToken ct = default);
    Task<ProductDto?> GetAsync(string productId, CancellationToken ct = default);
    Task<ProductDto> CreateAsync(CreateProductRequest req, CancellationToken ct = default);
}

public interface ICustomerService
{
    Task<PagedResult<CustomerDto>> ListAsync(CustomerListQuery q, CancellationToken ct = default);
    Task<CustomerDto?> GetAsync(string customerId, CancellationToken ct = default);
    Task<CustomerDto> CreateAsync(CreateCustomerRequest req, CancellationToken ct = default);
}

public interface ISaleService
{
    Task<PagedResult<SaleDto>> ListAsync(SaleListQuery q, CancellationToken ct = default);
    Task<SaleDto?> GetAsync(string transactionId, CancellationToken ct = default);
    Task<SaleDto> CreateAsync(CreateSaleRequest req, CancellationToken ct = default);
}

public interface IReturnService
{
    Task<ReturnDto> CreateAsync(CreateReturnRequest req, CancellationToken ct = default);
    Task<ReturnDto?> GetAsync(Guid returnId, CancellationToken ct = default);
}

public interface IInventoryService
{
    Task<PagedResult<InventoryDto>> ListAsync(InventoryQuery q, CancellationToken ct = default);
    Task<InventoryDto?> GetAsync(string storeId, string productId, CancellationToken ct = default);
}

public interface IStoreService
{
    Task<List<StoreDto>> ListAsync(CancellationToken ct = default);
}

public class StoreDto
{
    public string StoreId { get; set; } = string.Empty;
    public string StoreName { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public int StoreSizeM2 { get; set; }
    public StoreChannel Channel { get; set; }
    public bool IsActive { get; set; }
}
