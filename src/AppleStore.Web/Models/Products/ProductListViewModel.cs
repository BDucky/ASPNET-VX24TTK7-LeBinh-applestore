using AppleStore.Infrastructure.Services;

namespace AppleStore.Web.Models.Products;

public record ProductListViewModel(IReadOnlyList<ProductSummary> Products, string? SelectedCategory, string? Query, ProductSort Sort = ProductSort.Featured);
