using HelloOrder.Core.Enums;

namespace HelloOrder.Core.Entities;

public class Quotation
{
    public Guid Id { get; set; }
    public Guid MerchantId { get; set; }
    public string QuotationNo { get; set; } = null!;
    public Guid? BusinessUserId { get; set; }
    public QuotationStatus Status { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime? ValidUntil { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Merchant Merchant { get; set; } = null!;
    public ICollection<QuotationItem> Items { get; set; } = new List<QuotationItem>();
}
