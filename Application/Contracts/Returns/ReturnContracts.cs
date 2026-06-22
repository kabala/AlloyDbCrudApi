using AlloyDbCrudApi.Domain.Enums;

namespace AlloyDbCrudApi.Application.Contracts.Returns;

public class CreateReturnRequest
{
    public string TransactionId { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public ReturnReason Reason { get; set; } = ReturnReason.CustomerChange;
    public string? Notes { get; set; }
}

public class ReturnDto
{
    public Guid Id { get; set; }
    public string TransactionId { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public ReturnReason Reason { get; set; }
    public ReturnStatus Status { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
}
