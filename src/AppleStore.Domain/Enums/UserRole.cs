namespace AppleStore.Domain.Enums;

// Ordinal values are an implementation choice; the report's UserRole column has
// no explicit enumerated domain. Guest is unauthenticated and never a User row,
// so it has no member here.
public enum UserRole
{
    Customer = 0,
    Employee = 1,
    Admin = 2,
}
