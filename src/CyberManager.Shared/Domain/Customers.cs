namespace CyberManager.Shared.Domain;

public class Customer
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? DocumentId { get; set; }
    public string? Address { get; set; }
    public string? FiscalData { get; set; }
    public decimal Balance { get; set; }
    public decimal CreditLimit { get; set; }
    public bool IsMember { get; set; }
    public bool Active { get; set; } = true;
    public string? Notes { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public List<CustomerTransaction> Transactions { get; set; } = new();
}

public class CustomerTransaction
{
    public long Id { get; set; }
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public CustomerTransactionType Type { get; set; }
    public decimal Amount { get; set; }
    public decimal BalanceAfter { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }
    public string? Description { get; set; }
    public int? UserId { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
