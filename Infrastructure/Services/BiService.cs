using AlloyDbCrudApi.Application.Abstractions;
using AlloyDbCrudApi.Application.Contracts.Bi;
using AlloyDbCrudApi.Domain.Enums;
using AlloyDbCrudApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace AlloyDbCrudApi.Infrastructure.Services;

public class BiService : IBiService
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    private readonly AppDbContext _db;
    private readonly IMemoryCache _cache;

    public BiService(AppDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public Task<BiDashboardDto> GetDashboardAsync(BiDashboardQuery query, CancellationToken ct = default)
        => GetOrCreateAsync(CacheKey("dashboard", query.FromDate, query.ToDate), () => BuildDashboardAsync(query, ct), ct);

    public Task<IReadOnlyList<BiProductAbcDto>> GetProductAbcAsync(BiProductAbcQuery query, CancellationToken ct = default)
        => GetOrCreateAsync(CacheKey("abc", query.FromDate, query.ToDate, query.Take, query.AbcClass), () => BuildProductAbcAsync(query, ct), ct);

    public Task<IReadOnlyList<BiCustomerRfmDto>> GetCustomerRfmAsync(BiCustomerRfmQuery query, CancellationToken ct = default)
        => GetOrCreateAsync(CacheKey("rfm", query.FromDate, query.ToDate, query.Take), () => BuildCustomerRfmAsync(query, ct), ct);

    public Task<IReadOnlyList<BiBreakdownItemDto>> GetBreakdownAsync(string dimension, BiBreakdownQuery query, CancellationToken ct = default)
        => GetOrCreateAsync(CacheKey("breakdown", dimension, query.FromDate, query.ToDate, query.Take), () => BuildBreakdownAsync(dimension, query, ct), ct);

    private async Task<BiDashboardDto> BuildDashboardAsync(BiDashboardQuery query, CancellationToken ct)
    {
        var rows = await QueryRowsAsync(query.FromDate, query.ToDate, ct);
        var orderedRows = rows.OrderBy(x => x.Date).ToList();
        var dateFrom = orderedRows.FirstOrDefault()?.Date ?? query.FromDate;
        var dateTo = orderedRows.LastOrDefault()?.Date ?? query.ToDate;

        return new BiDashboardDto
        {
            Summary = Summarize(orderedRows, dateFrom, dateTo),
            Yearly = orderedRows
                .GroupBy(x => x.Date.Year)
                .OrderBy(x => x.Key)
                .Select(g => ToPeriodPoint(g, g.Key.ToString()))
                .ToList(),
            Monthly = orderedRows
                .GroupBy(x => new DateOnly(x.Date.Year, x.Date.Month, 1))
                .OrderBy(x => x.Key)
                .Select(g => ToPeriodPoint(g, g.Key.ToString("yyyy-MM")))
                .ToList(),
            CategoryPerformance = orderedRows
                .GroupBy(x => x.Category)
                .OrderByDescending(g => g.Sum(x => x.Revenue))
                .Select(g => ToBreakdown(g, g.Key, g.Key))
                .ToList(),
            StorePerformance = orderedRows
                .GroupBy(x => new { x.StoreId, x.StoreName, x.Region, x.StoreSizeM2 })
                .OrderByDescending(g => g.Sum(x => x.Revenue))
                .Select(g =>
                {
                    var dto = new BiStorePerformanceDto
                    {
                        StoreSizeM2 = g.Key.StoreSizeM2,
                        RevenuePerM2 = g.Key.StoreSizeM2 > 0 ? Math.Round(g.Sum(x => x.Revenue) / g.Key.StoreSizeM2, 2) : null,
                    };
                    CopyBreakdown(ToBreakdown(g, g.Key.StoreId, g.Key.StoreName, g.Key.Region), dto);
                    return dto;
                })
                .ToList(),
            DiscountImpact = orderedRows
                .GroupBy(x => x.Discount)
                .OrderBy(x => x.Key)
                .Select(g => new BiDiscountImpactDto
                {
                    Discount = g.Key,
                    Transactions = g.Select(x => x.TransactionId).Distinct().Count(),
                    UnitsSold = g.Sum(x => x.Quantity),
                    Revenue = Round2(g.Sum(x => x.Revenue)),
                    Margin = Round2(g.Sum(x => x.Margin)),
                    MarginRate = Ratio(g.Sum(x => x.Margin), g.Sum(x => x.Revenue)),
                    ReturnRate = Ratio(
                        g.Where(x => x.IsReturned).Select(x => x.TransactionId).Distinct().Count(),
                        g.Select(x => x.TransactionId).Distinct().Count()),
                })
                .ToList(),
            CityPerformance = orderedRows
                .GroupBy(x => x.City)
                .OrderByDescending(g => g.Sum(x => x.Revenue))
                .Select(g => ToBreakdown(g, g.Key, g.Key))
                .ToList(),
            Recommendations = BuildRecommendations(),
        };
    }

    private async Task<IReadOnlyList<BiProductAbcDto>> BuildProductAbcAsync(BiProductAbcQuery query, CancellationToken ct)
    {
        var rows = await QueryRowsAsync(query.FromDate, query.ToDate, ct);
        var groups = rows
            .GroupBy(x => new { x.ProductId, x.Category, x.Color, x.Size, x.Season, x.Supplier })
            .Select(g => new
            {
                g.Key.ProductId,
                g.Key.Category,
                g.Key.Color,
                g.Key.Size,
                g.Key.Season,
                g.Key.Supplier,
                Transactions = g.Select(x => x.TransactionId).Distinct().Count(),
                UnitsSold = g.Sum(x => x.Quantity),
                Revenue = g.Sum(x => x.Revenue),
                Margin = g.Sum(x => x.Margin),
                ReturnedTransactions = g.Where(x => x.IsReturned).Select(x => x.TransactionId).Distinct().Count(),
            })
            .OrderByDescending(x => x.Revenue)
            .ThenBy(x => x.ProductId)
            .ToList();

        var totalRevenue = groups.Sum(x => x.Revenue);
        decimal cumulativeRevenue = 0m;
        var results = new List<BiProductAbcDto>(groups.Count);
        for (var i = 0; i < groups.Count; i++)
        {
            var item = groups[i];
            cumulativeRevenue += item.Revenue;
            var pct = Ratio(cumulativeRevenue, totalRevenue);
            var abcClass = pct <= 0.80m ? "A" : pct <= 0.95m ? "B" : "C";
            results.Add(new BiProductAbcDto
            {
                Rank = i + 1,
                ProductId = item.ProductId,
                Category = item.Category,
                Color = item.Color,
                Size = item.Size,
                Season = item.Season,
                Supplier = item.Supplier,
                Transactions = item.Transactions,
                UnitsSold = item.UnitsSold,
                Revenue = Round2(item.Revenue),
                Margin = Round2(item.Margin),
                MarginRate = Ratio(item.Margin, item.Revenue),
                ReturnRate = Ratio(item.ReturnedTransactions, item.Transactions),
                CumulativeRevenuePercent = pct,
                AbcClass = abcClass,
            });
        }

        return results
            .Where(x => string.IsNullOrWhiteSpace(query.AbcClass) || x.AbcClass == query.AbcClass)
            .Take(query.Take)
            .ToList();
    }

    private async Task<IReadOnlyList<BiCustomerRfmDto>> BuildCustomerRfmAsync(BiCustomerRfmQuery query, CancellationToken ct)
    {
        var rows = await QueryRowsAsync(query.FromDate, query.ToDate, ct);
        if (rows.Count == 0) return [];

        var referenceDate = rows.Max(x => x.Date);
        var customers = rows
            .GroupBy(x => new { x.CustomerId, x.City, x.Age, x.Gender })
            .Select(g => new CustomerRfmWorkItem
            {
                CustomerId = g.Key.CustomerId,
                City = g.Key.City,
                Age = g.Key.Age,
                Gender = g.Key.Gender,
                LastPurchaseDate = g.Max(x => x.Date),
                Frequency = g.Select(x => x.TransactionId).Distinct().Count(),
                Monetary = g.Sum(x => x.Revenue),
            })
            .ToList();

        foreach (var item in customers)
            item.RecencyDays = referenceDate.DayNumber - item.LastPurchaseDate.DayNumber;

        AssignScore(customers.OrderBy(x => x.RecencyDays).ThenBy(x => x.CustomerId).ToList(), 5, (item, score) => item.RScore = score);
        AssignScore(customers.OrderByDescending(x => x.Frequency).ThenBy(x => x.CustomerId).ToList(), 5, (item, score) => item.FScore = score);
        AssignScore(customers.OrderByDescending(x => x.Monetary).ThenBy(x => x.CustomerId).ToList(), 5, (item, score) => item.MScore = score);

        return customers
            .OrderByDescending(x => x.RScore + x.FScore + x.MScore)
            .ThenByDescending(x => x.Monetary)
            .ThenBy(x => x.CustomerId)
            .Take(query.Take)
            .Select(x => new BiCustomerRfmDto
            {
                CustomerId = x.CustomerId,
                City = x.City,
                Age = x.Age,
                Gender = x.Gender,
                RecencyDays = x.RecencyDays,
                Frequency = x.Frequency,
                Monetary = Round2(x.Monetary),
                LastPurchaseDate = x.LastPurchaseDate,
                RScore = x.RScore,
                FScore = x.FScore,
                MScore = x.MScore,
                Segment = GetSegment(x.RScore, x.FScore, x.MScore),
            })
            .ToList();
    }

    private async Task<IReadOnlyList<BiBreakdownItemDto>> BuildBreakdownAsync(string dimension, BiBreakdownQuery query, CancellationToken ct)
    {
        var rows = await QueryRowsAsync(query.FromDate, query.ToDate, ct);
        IEnumerable<BiBreakdownItemDto> results = dimension switch
        {
            "category" => rows.GroupBy(x => x.Category).Select(g => ToBreakdown(g, g.Key, g.Key)),
            "store" => rows.GroupBy(x => new { x.StoreId, x.StoreName, x.Region }).Select(g => ToBreakdown(g, g.Key.StoreId, g.Key.StoreName, g.Key.Region)),
            "customer-city" => rows.GroupBy(x => x.City).Select(g => ToBreakdown(g, g.Key, g.Key)),
            "supplier" => rows.GroupBy(x => x.Supplier).Select(g => ToBreakdown(g, g.Key, g.Key)),
            "season" => rows.GroupBy(x => x.Season).Select(g => ToBreakdown(g, g.Key, g.Key)),
            "discount" => rows.GroupBy(x => x.Discount).Select(g => ToBreakdown(g, $"{g.Key:0.##}", $"{g.Key:P0}")),
            "return-category" => rows.Where(x => x.IsReturned).GroupBy(x => x.Category).Select(g => ToBreakdown(g, g.Key, g.Key)),
            "return-store" => rows.Where(x => x.IsReturned).GroupBy(x => new { x.StoreId, x.StoreName, x.Region }).Select(g => ToBreakdown(g, g.Key.StoreId, g.Key.StoreName, g.Key.Region)),
            "return-supplier" => rows.Where(x => x.IsReturned).GroupBy(x => x.Supplier).Select(g => ToBreakdown(g, g.Key, g.Key)),
            "return-size" => rows.Where(x => x.IsReturned).GroupBy(x => x.Size).Select(g => ToBreakdown(g, g.Key, g.Key)),
            _ => [],
        };

        return results
            .OrderByDescending(x => x.Revenue)
            .ThenBy(x => x.Label)
            .Take(query.Take)
            .ToList();
    }

    private async Task<List<BiSaleRow>> QueryRowsAsync(DateOnly? fromDate, DateOnly? toDate, CancellationToken ct)
    {
        var query =
            from sale in _db.Sales.AsNoTracking()
            join item in _db.SaleItems.AsNoTracking() on sale.TransactionId equals item.TransactionId
            join product in _db.Products.AsNoTracking() on item.ProductId equals product.ProductId
            join customer in _db.Customers.AsNoTracking() on sale.CustomerId equals customer.CustomerId
            join store in _db.Stores.AsNoTracking() on sale.StoreId equals store.StoreId
            join supplierLeft in _db.Suppliers.AsNoTracking() on product.SupplierId equals supplierLeft.Id into suppliers
            from supplier in suppliers.DefaultIfEmpty()
            join returnLeft in _db.Returns.AsNoTracking() on sale.TransactionId equals returnLeft.TransactionId into returns
            from ret in returns.DefaultIfEmpty()
            select new BiSaleRow
            {
                TransactionId = sale.TransactionId,
                Date = sale.Date,
                StoreId = sale.StoreId,
                StoreName = store.StoreName,
                Region = store.Region,
                StoreSizeM2 = store.StoreSizeM2,
                CustomerId = sale.CustomerId,
                City = customer.City,
                Age = customer.Age,
                Gender = customer.Gender,
                ProductId = product.ProductId,
                Category = product.Category,
                Color = product.Color,
                Size = string.IsNullOrWhiteSpace(product.Size) ? "Unspecified" : product.Size,
                Season = string.IsNullOrWhiteSpace(product.Season) ? "Unspecified" : product.Season,
                Supplier = supplier != null ? supplier.Name : "Unassigned",
                Quantity = item.Quantity,
                Discount = item.Discount,
                Revenue = item.Revenue,
                Margin = item.Margin,
                IsReturned = ret != null,
            };

        if (fromDate.HasValue) query = query.Where(x => x.Date >= fromDate.Value);
        if (toDate.HasValue) query = query.Where(x => x.Date <= toDate.Value);

        return await query.ToListAsync(ct);
    }

    private static BiSummaryDto Summarize(IReadOnlyCollection<BiSaleRow> rows, DateOnly? dateFrom, DateOnly? dateTo)
    {
        var transactions = rows.Select(x => x.TransactionId).Distinct().Count();
        var returnedTransactions = rows.Where(x => x.IsReturned).Select(x => x.TransactionId).Distinct().Count();
        var revenue = rows.Sum(x => x.Revenue);
        var margin = rows.Sum(x => x.Margin);
        var units = rows.Sum(x => x.Quantity);
        return new BiSummaryDto
        {
            Transactions = transactions,
            UnitsSold = units,
            Revenue = Round2(revenue),
            Margin = Round2(margin),
            MarginRate = Ratio(margin, revenue),
            ReturnRate = Ratio(returnedTransactions, transactions),
            AvgDiscount = Ratio(rows.Sum(x => x.Discount * x.Quantity), units),
            AvgTicket = Ratio(revenue, transactions),
            DateFrom = dateFrom,
            DateTo = dateTo,
        };
    }

    private static BiPeriodPointDto ToPeriodPoint<TKey>(IGrouping<TKey, BiSaleRow> group, string period)
    {
        var transactions = group.Select(x => x.TransactionId).Distinct().Count();
        var returnedTransactions = group.Where(x => x.IsReturned).Select(x => x.TransactionId).Distinct().Count();
        var revenue = group.Sum(x => x.Revenue);
        var margin = group.Sum(x => x.Margin);
        var units = group.Sum(x => x.Quantity);
        return new BiPeriodPointDto
        {
            Period = period,
            Transactions = transactions,
            UnitsSold = units,
            Revenue = Round2(revenue),
            Margin = Round2(margin),
            MarginRate = Ratio(margin, revenue),
            ReturnRate = Ratio(returnedTransactions, transactions),
            AvgDiscount = Ratio(group.Sum(x => x.Discount * x.Quantity), units),
        };
    }

    private static BiBreakdownItemDto ToBreakdown(IEnumerable<BiSaleRow> rows, string key, string label, string? secondaryLabel = null)
    {
        var list = rows.ToList();
        var transactions = list.Select(x => x.TransactionId).Distinct().Count();
        var returnedTransactions = list.Where(x => x.IsReturned).Select(x => x.TransactionId).Distinct().Count();
        var revenue = list.Sum(x => x.Revenue);
        var margin = list.Sum(x => x.Margin);
        var units = list.Sum(x => x.Quantity);
        return new BiBreakdownItemDto
        {
            Key = key,
            Label = label,
            SecondaryLabel = secondaryLabel,
            Transactions = transactions,
            UnitsSold = units,
            Revenue = Round2(revenue),
            Margin = Round2(margin),
            MarginRate = Ratio(margin, revenue),
            ReturnRate = Ratio(returnedTransactions, transactions),
            AvgDiscount = Ratio(list.Sum(x => x.Discount * x.Quantity), units),
        };
    }

    private static void CopyBreakdown(BiBreakdownItemDto source, BiStorePerformanceDto target)
    {
        target.Key = source.Key;
        target.Label = source.Label;
        target.SecondaryLabel = source.SecondaryLabel;
        target.Transactions = source.Transactions;
        target.UnitsSold = source.UnitsSold;
        target.Revenue = source.Revenue;
        target.Margin = source.Margin;
        target.MarginRate = source.MarginRate;
        target.ReturnRate = source.ReturnRate;
        target.AvgDiscount = source.AvgDiscount;
    }

    private async Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, CancellationToken ct)
    {
        if (_cache.TryGetValue(key, out T? value) && value is not null)
            return value;

        value = await factory();
        _cache.Set(key, value, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = CacheDuration,
            Size = 1,
        });
        return value;
    }

    private static string CacheKey(params object?[] parts)
        => string.Join("|", parts.Select(x => x?.ToString() ?? "null"));

    private static decimal Round2(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static decimal Ratio(decimal numerator, decimal denominator)
        => denominator == 0m ? 0m : Math.Round(numerator / denominator, 4, MidpointRounding.AwayFromZero);

    private static decimal Ratio(int numerator, int denominator)
        => denominator == 0 ? 0m : Math.Round((decimal)numerator / denominator, 4, MidpointRounding.AwayFromZero);

    private static void AssignScore<T>(IReadOnlyList<T> items, int buckets, Action<T, int> assign)
    {
        if (items.Count == 0) return;
        for (var i = 0; i < items.Count; i++)
        {
            var percentile = (decimal)i / items.Count;
            var score = buckets - (int)Math.Floor(percentile * buckets);
            assign(items[i], Math.Clamp(score, 1, buckets));
        }
    }

    private static string GetSegment(int rScore, int fScore, int mScore)
    {
        var total = rScore + fScore + mScore;
        if (rScore >= 4 && fScore >= 4 && mScore >= 4) return "Champions";
        if (total >= 11) return "Loyal";
        if (total >= 8) return "Potential";
        if (rScore <= 2 && mScore >= 4) return "At Risk";
        return "Need Attention";
    }

    private static List<string> BuildRecommendations() =>
    [
        "Control high discounts with minimum-margin rules because margin falls progressively as discount levels rise.",
        "Prioritize class A products for inventory, assortment, and return monitoring because they concentrate most revenue.",
        "Monitor returns by category, store, supplier, and size to detect operational hotspots before they affect margin.",
        "Use customer city and RFM segments to target retention and reactivation campaigns instead of broad promotions.",
        "Track store productivity with revenue per square meter to distinguish total revenue from space efficiency.",
    ];

    private sealed class BiSaleRow
    {
        public string TransactionId { get; set; } = string.Empty;
        public DateOnly Date { get; set; }
        public string StoreId { get; set; } = string.Empty;
        public string StoreName { get; set; } = string.Empty;
        public string Region { get; set; } = string.Empty;
        public int StoreSizeM2 { get; set; }
        public string CustomerId { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public int Age { get; set; }
        public Gender Gender { get; set; }
        public string ProductId { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public string Size { get; set; } = string.Empty;
        public string Season { get; set; } = string.Empty;
        public string Supplier { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal Discount { get; set; }
        public decimal Revenue { get; set; }
        public decimal Margin { get; set; }
        public bool IsReturned { get; set; }
    }

    private sealed class CustomerRfmWorkItem
    {
        public string CustomerId { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public int Age { get; set; }
        public Gender Gender { get; set; }
        public int RecencyDays { get; set; }
        public int Frequency { get; set; }
        public decimal Monetary { get; set; }
        public DateOnly LastPurchaseDate { get; set; }
        public int RScore { get; set; }
        public int FScore { get; set; }
        public int MScore { get; set; }
    }
}
