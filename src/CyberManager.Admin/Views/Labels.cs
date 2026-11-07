using CyberManager.Shared;

namespace CyberManager.Admin.Views;

/// <summary>Textos y colores de los valores del dominio en el idioma actual.</summary>
public static class Labels
{
    public static string StatusName(StationStatus status) => L.T(status switch
    {
        StationStatus.Offline => "Sin conexión",
        StationStatus.Available => "Disponible",
        StationStatus.InUse => "En uso",
        StationStatus.Paused => "En pausa",
        StationStatus.Maintenance => "Mantenimiento",
        StationStatus.Reserved => "Reservado",
        _ => status.ToString()
    });

    public static string StatusColor(StationStatus status) => status switch
    {
        StationStatus.Offline => "#8FA4C4",
        StationStatus.Available => "#22C55E",
        StationStatus.InUse => "#22D3EE",
        StationStatus.Paused => "#F59E0B",
        StationStatus.Maintenance => "#8B5CF6",
        StationStatus.Reserved => "#3B82F6",
        _ => "#8FA4C4"
    };

    public static string StationKindName(StationKind kind) => L.T(kind switch
    {
        StationKind.Pc => "PC",
        StationKind.Console => "Consola",
        StationKind.Other => "Otro",
        _ => kind.ToString()
    });

    public static string SessionTypeName(SessionType type) => L.T(type switch
    {
        SessionType.Prepaid => "Prepago",
        SessionType.Postpaid => "Postpago",
        SessionType.Free => "Cortesía",
        _ => type.ToString()
    });

    public static string PaymentName(PaymentMethod method) => L.T(method switch
    {
        PaymentMethod.Cash => "Efectivo Bs",
        PaymentMethod.Card => "Tarjeta",
        PaymentMethod.Transfer => "Transferencia",
        PaymentMethod.CustomerBalance => "Saldo del cliente",
        PaymentMethod.Mixed => "Mixto",
        PaymentMethod.CashUsd => "Efectivo USD",
        PaymentMethod.PagoMovil => "Pago Móvil",
        PaymentMethod.Pending => "Pendiente",
        _ => method.ToString()
    });

    public static string InvoiceName(InvoiceType type) => L.T(type switch
    {
        InvoiceType.Ticket => "Ticket",
        InvoiceType.Factura => "Factura",
        InvoiceType.NotaEntrega => "Nota de entrega",
        _ => type.ToString()
    });

    public static string StockName(StockMovementType type) => L.T(type switch
    {
        StockMovementType.Initial => "Inicial",
        StockMovementType.Purchase => "Compra",
        StockMovementType.Sale => "Venta",
        StockMovementType.Adjustment => "Ajuste",
        StockMovementType.Waste => "Merma",
        StockMovementType.Return => "Devolución",
        _ => type.ToString()
    });

    public static string CashName(CashMovementType type) => L.T(type switch
    {
        CashMovementType.Opening => "Apertura",
        CashMovementType.Sale => "Venta",
        CashMovementType.Deposit => "Depósito",
        CashMovementType.Withdrawal => "Retiro",
        CashMovementType.Expense => "Gasto",
        CashMovementType.Refund => "Devolución",
        CashMovementType.TimeSale => "Venta de tiempo",
        _ => type.ToString()
    });

    public static string ReservationName(ReservationStatus status) => L.T(status switch
    {
        ReservationStatus.Pending => "Pendiente",
        ReservationStatus.Confirmed => "Confirmada",
        ReservationStatus.InProgress => "En curso",
        ReservationStatus.Completed => "Completada",
        ReservationStatus.Cancelled => "Cancelada",
        ReservationStatus.NoShow => "No asistió",
        _ => status.ToString()
    });

    public static string ReservationColor(ReservationStatus status) => status switch
    {
        ReservationStatus.Pending => "#F59E0B",
        ReservationStatus.Confirmed => "#3B82F6",
        ReservationStatus.InProgress => "#22D3EE",
        ReservationStatus.Completed => "#22C55E",
        ReservationStatus.Cancelled => "#EF4444",
        ReservationStatus.NoShow => "#8FA4C4",
        _ => "#8FA4C4"
    };

    public static string RoleName(UserRole role) => L.T(role switch
    {
        UserRole.Admin => "Administrador",
        UserRole.Cashier => "Cajero",
        UserRole.Technician => "Técnico",
        _ => role.ToString()
    });

    public static string CustomerTxName(CustomerTransactionType type) => L.T(type switch
    {
        CustomerTransactionType.Recharge => "Recarga",
        CustomerTransactionType.Consumption => "Consumo",
        CustomerTransactionType.Refund => "Devolución",
        CustomerTransactionType.Adjustment => "Ajuste",
        CustomerTransactionType.Credit => "Crédito",
        _ => type.ToString()
    });

    public static string RateSourceName(RateSource source) => L.T(source switch
    {
        RateSource.Manual => "Manual",
        RateSource.Bcv => "BCV",
        RateSource.Paralelo => "Paralelo",
        _ => source.ToString()
    });
}
