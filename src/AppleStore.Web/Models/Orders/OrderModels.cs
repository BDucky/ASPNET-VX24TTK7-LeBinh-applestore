using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using AppleStore.Domain.Enums;
using AppleStore.Infrastructure.Services;

namespace AppleStore.Web.Models.Orders;

// An order page: the order, and the buttons OrderTransitions allows the viewer.
public sealed record OrderPage(OrderSummary Order, IReadOnlyList<OrderAction> Actions)
{
    public bool WaitsForPayment => OrderTransitions.NeedsPayment(OrderAction.Confirm, Order.PaymentMethod, Order.PaymentStatus)
        && Order.Status == OrderStatus.Pending;

    public static OrderPage For(OrderSummary order, bool byStaff) =>
        new(order, OrderTransitions.Allowed(order.Status, order.PaymentMethod, order.PaymentStatus, byStaff));
}

public sealed record StaffOrderList(IReadOnlyList<OrderListItem> Orders, OrderStatus? Status);

public sealed class TrackForm
{
    [Required, Range(1, int.MaxValue, ErrorMessage = "Enter the order number."), Display(Name = "Order number")]
    public int? OrderId { get; set; }

    [Required, StringLength(20), Display(Name = "Receiver phone")]
    public string? Phone { get; set; }
}

public sealed record TrackPage(TrackForm Form, TrackingResult? Result, bool Searched);

// The nav link each staff role gets: one table, not role checks in views.
public static class StaffLinks
{
    private static readonly (UserRole Role, string Label, string Href)[] Links =
    [
        (UserRole.Admin, "Admin", "/Admin"),
        (UserRole.Employee, "Staff", "/Admin/Orders"),
    ];

    public static (string Label, string Href)? For(ClaimsPrincipal user) =>
        Links.Where(l => user.IsInRole(l.Role.ToString())).Select(l => ((string, string)?)(l.Label, l.Href)).FirstOrDefault();
}
