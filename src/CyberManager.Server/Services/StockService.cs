using CyberManager.Shared;
using CyberManager.Shared.Domain;
using Microsoft.EntityFrameworkCore;

namespace CyberManager.Server.Services;

public class StockService
{
    private readonly Data.AppDbContext _db;
    public StockService(Data.AppDbContext db) => _db = db;

    /// <summary>Aplica un movimiento de inventario. La cantidad es positiva para entradas y negativa para salidas.</summary>
    public async Task<Product> ApplyAsync(int productId, StockMovementType type, decimal signedQuantity,
        decimal unitCost, string? reference, int? userId, string? notes, bool allowNegative = false)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == productId)
                      ?? throw AppException.NotFound("Producto");

        if (product.IsService && type is StockMovementType.Sale or StockMovementType.Purchase)
            return product;

        var newStock = product.Stock + signedQuantity;
        if (newStock < 0 && !allowNegative)
            throw new AppException($"Stock insuficiente de '{product.Name}'. Disponible: {product.Stock}.");

        product.Stock = newStock;
        if (type == StockMovementType.Purchase && unitCost > 0)
            product.Cost = unitCost;

        _db.StockMovements.Add(new StockMovement
        {
            ProductId = productId,
            Type = type,
            Quantity = signedQuantity,
            UnitCost = unitCost,
            BalanceAfter = newStock,
            Reference = reference,
            UserId = userId,
            Notes = notes,
            CreatedUtc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        return product;
    }
}
