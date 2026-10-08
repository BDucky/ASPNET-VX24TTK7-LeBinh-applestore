using AppleStore.Infrastructure.Services;

namespace AppleStore.Web.Models.Orders;

// The one place order changes become sentences, for customers and for staff.
public static class OrderMessages
{
    public const string SaveFailed = "We could not save that change. Please try again.";
    public const string NotTracked = "No order matches that number and phone.";

    public static (string Text, bool Good) ForCustomerCancel(OrderChangeResult result) => result.Outcome switch
    {
        OrderChangeOutcome.Done when result.RefundDue => ("Your order was cancelled. The payment will be refunded.", true),
        OrderChangeOutcome.Done => ("Your order was cancelled.", true),
        _ => ("This order can no longer be cancelled. Contact us if you need help.", false),
    };

    public static (string Text, bool Good) ForStaff(OrderAction action, OrderChangeResult result) => (action, result.Outcome) switch
    {
        (OrderAction.Confirm, OrderChangeOutcome.Done) => ("Order confirmed.", true),
        (OrderAction.Ship, OrderChangeOutcome.Done) => ("Order shipped. The customer was emailed the tracking number.", true),
        (OrderAction.Complete, OrderChangeOutcome.Done) => ("Order completed.", true),
        (OrderAction.Cancel, OrderChangeOutcome.Done) when result.RefundDue => ("Order cancelled. It was paid: refund the customer.", true),
        (OrderAction.Cancel, OrderChangeOutcome.Done) => ("Order cancelled. The stock was put back.", true),
        (_, OrderChangeOutcome.NeedsPayment) => ("This order is not paid yet, so it cannot be confirmed.", false),
        (_, OrderChangeOutcome.MissingTracking) => ("Enter the carrier and the tracking number.", false),
        _ => ("This order changed meanwhile. Check it and try again.", false),
    };

    public static string Shipment(Domain.Enums.ShipmentStatus? status) => status switch
    {
        Domain.Enums.ShipmentStatus.AwaitingPickup => "Waiting for pickup",
        Domain.Enums.ShipmentStatus.InTransit => "In transit",
        Domain.Enums.ShipmentStatus.Delivered => "Delivered",
        Domain.Enums.ShipmentStatus.Cancelled => "Cancelled",
        _ => "",
    };
}
