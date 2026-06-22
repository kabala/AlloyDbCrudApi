using AlloyDbCrudApi.Domain.Enums;
using AlloyDbCrudApi.Domain.Exceptions;

namespace AlloyDbCrudApi.Domain.Entities;

public class Return
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TransactionId { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public ReturnReason Reason { get; set; } = ReturnReason.Other;
    public ReturnStatus Status { get; set; } = ReturnStatus.Pending;
    public Guid ApprovedByUserId { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ApprovedAt { get; set; }

    public Sale? Sale { get; set; }
    public User? ApprovedBy { get; set; }

    public void Approve(Guid approverUserId, string? notes = null)
    {
        if (Status == ReturnStatus.Approved)
            throw new InvalidReturnException($"Return for '{TransactionId}' is already approved.");
        Status = ReturnStatus.Approved;
        ApprovedByUserId = approverUserId;
        ApprovedAt = DateTime.UtcNow;
        Notes = notes ?? Notes;
    }

    public void Reject(string reason)
    {
        if (Status == ReturnStatus.Approved)
            throw new InvalidReturnException($"Cannot reject an already approved return.");
        Status = ReturnStatus.Rejected;
        Notes = reason;
    }
}
