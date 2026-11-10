using CyberManager.Shared;

namespace CyberManager.Shared.Domain;

public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#64748B";
    public int SortOrder { get; set; }
    public bool Active { get; set; } = true;
    public List<Product> Products { get; set; } = new();
}

public class Product
{
    public int Id { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int? CategoryId { get; set; }
    public Category? Category { get; set; }
    public decimal Price { get; set; }
    public decimal Cost { get; set; }
    public decimal Stock { get; set; }
    public decimal MinStock { get; set; }
    public string Unit { get; set; } = "pza";
    public bool IsService { get; set; }
    public decimal TaxRate { get; set; }
    public bool Active { get; set; } = true;
    public string? ImagePath { get; set; }
    /// <summary>Icono corto (emoji o texto) que se muestra en la caja y la pantalla del cliente.</summary>
    public string? Icon { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}

public class StockMovement
{
    public long Id { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public StockMovementType Type { get; set; }
    /// <summary>Positivo entra, negativo sale.</summary>
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal BalanceAfter { get; set; }
    public string? Reference { get; set; }
    public int? UserId { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}

public class Supplier
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? Notes { get; set; }
    public bool Active { get; set; } = true;
    public List<SupplierPayment> Payments { get; set; } = new();
}

/// <summary>Pago o abono realizado a un proveedor.</summary>
public class SupplierPayment
{
    public long Id { get; set; }
    public int SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public DateTime DateUtc { get; set; } = DateTime.UtcNow;
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; } = PaymentMethod.Cash;
    public string? Reference { get; set; }
    public string? Notes { get; set; }
    public int? UserId { get; set; }
}

public class Purchase
{
    public long Id { get; set; }
    public int? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public string? InvoiceNumber { get; set; }
    public DateTime DateUtc { get; set; } = DateTime.UtcNow;
    public decimal Total { get; set; }
    public int? UserId { get; set; }
    public string? Notes { get; set; }
    public List<PurchaseItem> Items { get; set; } = new();
}

public class PurchaseItem
{
    public long Id { get; set; }
    public long PurchaseId { get; set; }
    public Purchase? Purchase { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
}
