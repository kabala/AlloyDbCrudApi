using AlloyDbCrudApi.Domain.Enums;

namespace AlloyDbCrudApi.Application.Contracts.Bi;

public class BiDashboardQuery
{
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
}

public class BiProductAbcQuery : BiDashboardQuery
{
    public int Take { get; set; } = 100;
    public string? AbcClass { get; set; }
}

public class BiCustomerRfmQuery : BiDashboardQuery
{
    public int Take { get; set; } = 100;
}

public class BiBreakdownQuery : BiDashboardQuery
{
    public int Take { get; set; } = 25;
}

public class BiDashboardDto
{
    public BiSummaryDto Summary { get; set; } = new();
    public List<BiPeriodPointDto> Yearly { get; set; } = new();
    public List<BiPeriodPointDto> Monthly { get; set; } = new();
    public List<BiBreakdownItemDto> CategoryPerformance { get; set; } = new();
    public List<BiStorePerformanceDto> StorePerformance { get; set; } = new();
    public List<BiDiscountImpactDto> DiscountImpact { get; set; } = new();
    public List<BiBreakdownItemDto> CityPerformance { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
}

public class BiSummaryDto
{
    public int Transactions { get; set; }
    public int UnitsSold { get; set; }
    public decimal Revenue { get; set; }
    public decimal Margin { get; set; }
    public decimal MarginRate { get; set; }
    public decimal ReturnRate { get; set; }
    public decimal AvgDiscount { get; set; }
    public decimal AvgTicket { get; set; }
    public DateOnly? DateFrom { get; set; }
    public DateOnly? DateTo { get; set; }
}

public class BiPeriodPointDto
{
    public string Period { get; set; } = string.Empty;
    public int Transactions { get; set; }
    public int UnitsSold { get; set; }
    public decimal Revenue { get; set; }
    public decimal Margin { get; set; }
    public decimal MarginRate { get; set; }
    public decimal ReturnRate { get; set; }
    public decimal AvgDiscount { get; set; }
}

public class BiBreakdownItemDto
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? SecondaryLabel { get; set; }
    public int Transactions { get; set; }
    public int UnitsSold { get; set; }
    public decimal Revenue { get; set; }
    public decimal Margin { get; set; }
    public decimal MarginRate { get; set; }
    public decimal ReturnRate { get; set; }
    public decimal AvgDiscount { get; set; }
}

public class BiStorePerformanceDto : BiBreakdownItemDto
{
    public int StoreSizeM2 { get; set; }
    public decimal? RevenuePerM2 { get; set; }
}

public class BiDiscountImpactDto
{
    public decimal Discount { get; set; }
    public int Transactions { get; set; }
    public int UnitsSold { get; set; }
    public decimal Revenue { get; set; }
    public decimal Margin { get; set; }
    public decimal MarginRate { get; set; }
    public decimal ReturnRate { get; set; }
}

public class BiProductAbcDto
{
    public int Rank { get; set; }
    public string ProductId { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public string Season { get; set; } = string.Empty;
    public string Supplier { get; set; } = string.Empty;
    public int Transactions { get; set; }
    public int UnitsSold { get; set; }
    public decimal Revenue { get; set; }
    public decimal Margin { get; set; }
    public decimal MarginRate { get; set; }
    public decimal ReturnRate { get; set; }
    public decimal CumulativeRevenuePercent { get; set; }
    public string AbcClass { get; set; } = string.Empty;
}

public class BiCustomerRfmDto
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
    public string Segment { get; set; } = string.Empty;
}
