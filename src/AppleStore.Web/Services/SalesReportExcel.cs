using AppleStore.Infrastructure.Services;
using ClosedXML.Excel;

namespace AppleStore.Web.Services;

// Use case 35: the sales report as a real Excel workbook (ClosedXML, MIT).
// Amounts are numbers with a thousands format, so they can be summed in Excel.
public static class SalesReportExcel
{
    private const string Money = "#,##0";

    public static byte[] Build(SalesReport report)
    {
        using var workbook = new XLWorkbook();

        var summary = workbook.Worksheets.Add("Summary");
        var rows = new (string Label, object Value)[]
        {
            ("From", report.From.ToString("yyyy-MM-dd")),
            ("To", report.To.ToString("yyyy-MM-dd")),
            ("Revenue", report.Revenue),
            ("Sales before discounts", report.Sales),
            ("Discounts", report.Discount),
            ("Paid orders", report.OrdersPaid),
            ("Items sold", report.Items),
            ("Average order", report.AverageOrder),
            ("Orders placed", report.OrdersPlaced),
            ("Waiting for payment", report.OrdersWaiting),
            ("Cancelled", report.OrdersCancelled),
            ("Rule", "Paid orders that were not cancelled; revenue = quantity x unit price - discount; days in Vietnam time"),
        };
        for (var i = 0; i < rows.Length; i++)
        {
            summary.Cell(i + 1, 1).Value = rows[i].Label;
            var cell = summary.Cell(i + 1, 2);
            cell.Value = rows[i].Value switch
            {
                decimal d => d,
                int n => n,
                var other => other.ToString(),
            };
            if (rows[i].Value is decimal)
                cell.Style.NumberFormat.Format = Money;
        }

        var days = workbook.Worksheets.Add("By day");
        Header(days, "Date", "Paid orders", "Items", "Sales", "Discounts", "Revenue");
        var r = 2;
        foreach (var day in report.Days)
        {
            days.Cell(r, 1).Value = day.Date.ToString("yyyy-MM-dd");
            days.Cell(r, 2).Value = day.Orders;
            days.Cell(r, 3).Value = day.Items;
            days.Cell(r, 4).Value = day.Sales;
            days.Cell(r, 5).Value = day.Discount;
            days.Cell(r, 6).Value = day.Revenue;
            days.Range(r, 4, r, 6).Style.NumberFormat.Format = Money;
            r++;
        }

        var products = workbook.Worksheets.Add("By product");
        Header(products, "Product", "Quantity", "Sales before discounts");
        r = 2;
        foreach (var product in report.Products)
        {
            products.Cell(r, 1).Value = product.Name;
            products.Cell(r, 2).Value = product.Quantity;
            products.Cell(r, 3).Value = product.Sales;
            products.Cell(r, 3).Style.NumberFormat.Format = Money;
            r++;
        }

        foreach (var sheet in workbook.Worksheets)
            sheet.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void Header(IXLWorksheet sheet, params string[] names)
    {
        for (var i = 0; i < names.Length; i++)
            sheet.Cell(1, i + 1).Value = names[i];
        sheet.Row(1).Style.Font.Bold = true;
    }
}
