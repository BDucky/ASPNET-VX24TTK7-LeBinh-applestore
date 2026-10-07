namespace AppleStore.Web.Areas.Admin.Models;

// ProductsOnSale counts products whose Status is on, the same rule the
// storefront uses to show a product.
public sealed record DashboardViewModel(int Products, int ProductsOnSale, int Customers, int Orders);
