namespace AppleStore.Domain.Enums;

// Where a logged price change came from (BM_PRICE_01).
public enum PriceChangeSource
{
    // An admin saved one variant on the product page.
    Edit = 0,
    // A batch change from the prices page.
    Batch = 1,
    // A variant added with a price.
    NewVariant = 2,
}
