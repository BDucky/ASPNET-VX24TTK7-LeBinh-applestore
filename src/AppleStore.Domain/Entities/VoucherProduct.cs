namespace AppleStore.Domain.Entities;

// A voucher with any rows here applies only to those products; with none,
// to every product.
public class VoucherProduct
{
    public int VoucherId { get; set; }
    public int ProductId { get; set; }

    public Voucher Voucher { get; set; } = null!;
    public Product Product { get; set; } = null!;
}
