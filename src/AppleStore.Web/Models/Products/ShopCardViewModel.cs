namespace AppleStore.Web.Models.Products;

// One card shape for both grids (search results and a model's variants),
// laid out like rauvang.com's cards: photo, a badge under it, a centred
// name, and a red "From" price.
public record ShopCardViewModel(string Href, string? ImageUrl, string Title, string? Badge, decimal Price);
