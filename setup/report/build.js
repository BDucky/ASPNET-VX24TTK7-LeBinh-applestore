// Builds the course report (task 11) as a .docx with docx-js.
// Usage: node build.js <repo root>
const fs = require("fs");
const path = require("path");
const {
  Document, Packer, Paragraph, TextRun, HeadingLevel, AlignmentType, Table, TableRow, TableCell,
  WidthType, ImageRun, TableOfContents, StyleLevel, Footer, PageNumber, NumberFormat, SectionType,
  PageBreak, LevelFormat, BorderStyle, ShadingType, VerticalAlign,
} = require("docx");

const ROOT = process.argv[2];
const IMG = path.join(ROOT, "thesis", "doc", "hinh");
const OUT = path.join(ROOT, "thesis", "doc", "BaoCao_ChuyenDeASPNET_LeBinh_470124170_VX24TTK7.docx");

const FONT = "Times New Roman";
const SIZE = 26; // 13 pt
const TEXT_WIDTH = 9071; // A4 11906 - left 1701 - right 1134

// ---------- small builders ----------

// "**bold**" inside a string becomes a bold run.
function runs(text, extra = {}) {
  return text.split(/(\*\*[^*]+\*\*)/).filter(Boolean).map(part =>
    part.startsWith("**") ? new TextRun({ text: part.slice(2, -2), bold: true, ...extra }) : new TextRun({ text: part, ...extra }));
}

const p = (text, opts = {}) => new Paragraph({
  children: runs(text),
  alignment: opts.align ?? AlignmentType.JUSTIFIED,
  indent: opts.noIndent ? undefined : { firstLine: 567 },
  spacing: { after: 120 },
  ...opts.para,
});

const bullets = items => items.map(t => new Paragraph({ children: runs(t), numbering: { reference: "dot", level: 0 }, alignment: AlignmentType.JUSTIFIED, spacing: { after: 60 } }));
const steps = (items, ref) => items.map(t => new Paragraph({ children: runs(t), numbering: { reference: ref, level: 0 }, alignment: AlignmentType.JUSTIFIED, spacing: { after: 60 } }));
let stepLists = 0;
const numbered = items => steps(items, `steps${++stepLists}`);

const h1 = text => new Paragraph({ heading: HeadingLevel.HEADING_1, children: [new TextRun(text)], pageBreakBefore: true });
const h2 = text => new Paragraph({ heading: HeadingLevel.HEADING_2, children: [new TextRun(text)] });
const h3 = text => new Paragraph({ heading: HeadingLevel.HEADING_3, children: [new TextRun(text)] });

const border = { style: BorderStyle.SINGLE, size: 4, color: "808080" };
const borders = { top: border, bottom: border, left: border, right: border };

function cell(text, width, header = false) {
  return new TableCell({
    borders,
    width: { size: width, type: WidthType.DXA },
    shading: header ? { fill: "D9E2F3", type: ShadingType.CLEAR, color: "auto" } : undefined,
    margins: { top: 60, bottom: 60, left: 100, right: 100 },
    verticalAlign: VerticalAlign.CENTER,
    children: String(text).split("\n").map(line => new Paragraph({ children: runs(line, { size: 24, bold: header || undefined }), spacing: { after: 0, line: 276 } })),
  });
}

let tableNo = {};
// A table with a numbered caption above it ("Bảng 3.1: ...").
function table(chapter, caption, header, rows, weights) {
  tableNo[chapter] = (tableNo[chapter] ?? 0) + 1;
  const total = weights.reduce((a, b) => a + b, 0);
  const widths = weights.map(w => Math.floor(TEXT_WIDTH * w / total));
  widths[widths.length - 1] += TEXT_WIDTH - widths.reduce((a, b) => a + b, 0);
  return [
    new Paragraph({ style: "BangCaption", children: [new TextRun(`Bảng ${chapter}.${tableNo[chapter]}: ${caption}`)] }),
    new Table({
      width: { size: TEXT_WIDTH, type: WidthType.DXA },
      columnWidths: widths,
      rows: [
        new TableRow({ tableHeader: true, children: header.map((h, i) => cell(h, widths[i], true)) }),
        ...rows.map(r => new TableRow({ children: r.map((c, i) => cell(c, widths[i])) })),
      ],
    }),
    new Paragraph({ children: [], spacing: { after: 120 } }),
  ];
}

let figNo = {};
let figCount = 0;
// A screenshot scaled to the text width (and at most ~19 cm tall), with a numbered caption below.
function figure(chapter, file, caption, maxWidthCm = 16) {
  figNo[chapter] = (figNo[chapter] ?? 0) + 1;
  figCount++;
  const data = fs.readFileSync(path.join(IMG, file));
  const w = data.readUInt32BE(16), h = data.readUInt32BE(20);
  const pxPerCm = 37.8;
  let width = maxWidthCm * pxPerCm;
  let height = width * h / w;
  const maxH = 15.5 * pxPerCm; // about 60% of the A4 text height
  if (height > maxH) { height = maxH; width = height * w / h; }
  return [
    new Paragraph({ alignment: AlignmentType.CENTER, keepNext: true, spacing: { before: 120, after: 60 },
      children: [new ImageRun({ type: "png", data, transformation: { width: Math.round(width), height: Math.round(height) },
        altText: { title: caption, description: caption, name: file } })] }),
    new Paragraph({ style: "HinhCaption", children: [new TextRun(`Hình ${chapter}.${figNo[chapter]}: ${caption}`)] }),
  ];
}

const pageBreak = () => new Paragraph({ children: [new PageBreak()] });
const blank = (n = 1) => Array.from({ length: n }, () => new Paragraph({ children: [] }));
const centered = (text, opts = {}) => new Paragraph({ alignment: AlignmentType.CENTER, spacing: { after: opts.after ?? 0 },
  children: [new TextRun({ text, bold: opts.bold, size: opts.size, italics: opts.italics, allCaps: opts.caps })] });

// ---------- cover ----------

const cover = [
  centered("TRƯỜNG ĐẠI HỌC TRÀ VINH", { bold: true, size: 28 }),
  centered("TRƯỜNG KỸ THUẬT VÀ CÔNG NGHỆ", { bold: true, size: 28 }),
  centered("KHOA CÔNG NGHỆ THÔNG TIN", { bold: true, size: 28, after: 200 }),
  centered("--------------------", { after: 600 }),
  ...blank(3),
  centered("ĐỒ ÁN MÔN HỌC: CHUYÊN ĐỀ ASP.NET", { bold: true, size: 32, after: 120 }),
  centered("HỌC KỲ 7, NĂM HỌC 2026", { bold: true, size: 28, after: 600 }),
  centered("XÂY DỰNG WEBSITE BÁN SẢN PHẨM", { bold: true, size: 36, after: 60 }),
  centered("CÔNG NGHỆ APPLE (APPLE STORE)", { bold: true, size: 36, after: 600 }),
  ...blank(4),
  new Paragraph({ indent: { left: 4536 }, children: [new TextRun({ text: "Giảng viên hướng dẫn:", bold: true })] }),
  new Paragraph({ indent: { left: 4536 }, spacing: { after: 240 }, children: [new TextRun("TS. Đoàn Phước Miền")] }),
  new Paragraph({ indent: { left: 4536 }, children: [new TextRun({ text: "Sinh viên thực hiện:", bold: true })] }),
  new Paragraph({ indent: { left: 4536 }, children: [new TextRun("Họ tên: Lê Bình")] }),
  new Paragraph({ indent: { left: 4536 }, children: [new TextRun("MSSV: 470124170")] }),
  new Paragraph({ indent: { left: 4536 }, children: [new TextRun("Lớp: VX24TTK7")] }),
  ...blank(5),
  centered("Trà Vinh, tháng 10 năm 2026", { italics: true }),
];

// ---------- front matter ----------

const unnumberedTitle = text => new Paragraph({ style: "FrontTitle", children: [new TextRun(text)] });

const front = [
  unnumberedTitle("LỜI CẢM ƠN"),
  p("Em xin gửi lời cảm ơn chân thành đến thầy TS. Đoàn Phước Miền, giảng viên hướng dẫn môn Chuyên đề ASP.NET, đã tận tình hướng dẫn, góp ý và định hướng cho em trong suốt quá trình thực hiện đồ án này. Những kiến thức về ASP.NET Core mà thầy truyền đạt là nền tảng để em xây dựng được website Apple Store."),
  p("Em cũng xin cảm ơn Ban Giám hiệu, Trường Kỹ thuật và Công nghệ, Khoa Công nghệ Thông tin cùng các thầy cô đã tạo điều kiện học tập và nghiên cứu để em hoàn thành đồ án."),
  p("Em xin cảm ơn gia đình và bạn bè đã luôn động viên, hỗ trợ em trong thời gian học tập và thực hiện đề tài."),
  p("Do thời gian và kinh nghiệm còn hạn chế, đồ án không tránh khỏi những thiếu sót. Em rất mong nhận được ý kiến đóng góp của thầy cô để đề tài được hoàn thiện hơn."),
  p("Em xin chân thành cảm ơn!"),
  ...blank(1),
  new Paragraph({ indent: { left: 5103 }, alignment: AlignmentType.CENTER, children: [new TextRun({ text: "Sinh viên thực hiện", italics: true })] }),
  ...blank(2),
  new Paragraph({ indent: { left: 5103 }, alignment: AlignmentType.CENTER, children: [new TextRun({ text: "Lê Bình", bold: true })] }),
  pageBreak(),
  unnumberedTitle("MỤC LỤC"),
  new TableOfContents("Mục lục", { hyperlink: true, headingStyleRange: "1-3" }),
  pageBreak(),
  unnumberedTitle("DANH MỤC HÌNH"),
  new TableOfContents("Danh mục hình", { hyperlink: true, stylesWithLevels: [new StyleLevel("HinhCaption", 1)] }),
  pageBreak(),
  unnumberedTitle("DANH MỤC BẢNG"),
  new TableOfContents("Danh mục bảng", { hyperlink: true, stylesWithLevels: [new StyleLevel("BangCaption", 1)] }),
  pageBreak(),
  unnumberedTitle("DANH MỤC TỪ VIẾT TẮT"),
  ...(() => {
    const rows = [
      ["MVC", "Model, View, Controller: mô hình kiến trúc của ASP.NET Core MVC"],
      ["EF Core", "Entity Framework Core: thư viện ánh xạ đối tượng và cơ sở dữ liệu (ORM)"],
      ["ORM", "Object Relational Mapping: ánh xạ đối tượng sang bảng dữ liệu"],
      ["CSDL", "Cơ sở dữ liệu"],
      ["OTP", "One-Time Password: mã xác thực dùng một lần"],
      ["COD", "Cash On Delivery: thanh toán khi nhận hàng"],
      ["CRUD", "Create, Read, Update, Delete: thêm, xem, sửa, xóa"],
      ["DI", "Dependency Injection: tiêm phụ thuộc"],
      ["CSRF", "Cross-Site Request Forgery: giả mạo yêu cầu từ trang khác"],
      ["HMAC", "Hash-based Message Authentication Code: mã xác thực thông điệp dùng hàm băm"],
      ["SKU", "Stock Keeping Unit: mã quản lý từng biến thể hàng hóa"],
      ["TDD", "Test-Driven Development: phát triển hướng kiểm thử, viết test trước"],
      ["PR", "Pull Request: yêu cầu gộp mã trên GitHub"],
      ["UTC", "Coordinated Universal Time: giờ quốc tế"],
      ["VAT", "Value Added Tax: thuế giá trị gia tăng"],
      ["VNĐ", "Việt Nam đồng"],
    ];
    tableNo.front = 0;
    return [new Table({
      width: { size: TEXT_WIDTH, type: WidthType.DXA }, columnWidths: [1800, TEXT_WIDTH - 1800],
      rows: [new TableRow({ children: [cell("Từ viết tắt", 1800, true), cell("Ý nghĩa", TEXT_WIDTH - 1800, true)] }),
        ...rows.map(r => new TableRow({ children: [cell(r[0], 1800), cell(r[1], TEXT_WIDTH - 1800)] }))],
    })];
  })(),
];

// ---------- chapter 1 ----------

const ch1 = [
  h1("CHƯƠNG 1: MỞ ĐẦU"),
  h2("1.1. Lý do chọn đề tài"),
  p("Các sản phẩm công nghệ của Apple như iPhone, iPad, Mac, Apple Watch và AirPods có nhiều phiên bản khác nhau về dung lượng, màu sắc và thị trường phát hành. Người mua thường phải so sánh giữa nhiều cửa hàng và sàn thương mại điện tử, trong khi thông tin về sự khác nhau giữa các biến thể (màu, dung lượng, khu vực) thường không rõ ràng."),
  p("Đặc tả yêu cầu mà đề tài dựa vào (đồ án môn Công nghệ phần mềm \"Xây dựng website Apple Store\", Khoa Công nghệ Thông tin, Trường Đại học Trà Vinh, 2025) nêu ra bốn vấn đề của cách bán hàng hiện tại:"),
  ...bullets([
    "Thông tin sản phẩm phân tán trên nhiều kênh, khó phân biệt các biến thể.",
    "Quản lý giỏ hàng và đơn hàng bất tiện, hay sai sót khi đổi số lượng hoặc khi thanh toán khi nhận hàng.",
    "Quản lý tồn kho và báo cáo còn làm thủ công bằng bảng tính nên dễ nhầm lẫn.",
    "Tích hợp thanh toán trực tuyến chưa ổn định: một lần cổng thanh toán VNPay hoặc MoMo gọi lại thất bại có thể làm mất đơn hàng.",
  ]),
  p("Từ đặc tả đó, đề tài xây dựng lại hệ thống trên nền tảng ASP.NET Core MVC, là nội dung chính của môn Chuyên đề ASP.NET. Đề tài vừa giải quyết bài toán bán hàng thực tế, vừa là dịp để vận dụng các cơ chế có sẵn của ASP.NET Core như MVC, Razor, Entity Framework Core, Identity và Areas vào một ứng dụng hoàn chỉnh."),
  h2("1.2. Mục tiêu của đề tài"),
  h3("1.2.1. Mục tiêu chung"),
  p("Xây dựng một website bán sản phẩm công nghệ Apple chạy được thật, cho phép khách hàng tìm, so sánh, đặt mua và thanh toán sản phẩm, đồng thời cho phép nhân viên và quản trị viên quản lý đơn hàng, sản phẩm, giá, khuyến mãi và báo cáo doanh thu."),
  h3("1.2.2. Mục tiêu cụ thể"),
  ...bullets([
    "Hiện thực các trường hợp sử dụng (use case) trong đặc tả: tài khoản và OTP, danh mục sản phẩm, giỏ hàng, đặt hàng, voucher, thanh toán, xử lý đơn hàng, nhập kho, bán hàng tại quầy, quản trị sản phẩm, khuyến mãi, đánh giá và báo cáo.",
    "Sử dụng đúng các cơ chế có sẵn của ASP.NET Core: mô hình MVC với Razor view, EF Core Code First và migration, ASP.NET Core Identity cho đăng nhập và phân quyền, Area cho trang quản trị, model validation và chống giả mạo yêu cầu (anti-forgery).",
    "Bảo đảm dữ liệu đúng khi có nhiều người thao tác cùng lúc: không bán quá số lượng tồn kho, không dùng voucher quá số lượt, không ghi đè thay đổi của người khác.",
    "Kiểm thử kỹ: viết test tự động trước khi viết mã (TDD) và kiểm tra lại toàn bộ trên trình duyệt thật bằng Playwright.",
  ]),
  h2("1.3. Đối tượng và phạm vi nghiên cứu"),
  h3("1.3.1. Đối tượng nghiên cứu"),
  ...bullets([
    "Nền tảng ASP.NET Core MVC (.NET 10) và Entity Framework Core.",
    "Nghiệp vụ bán lẻ sản phẩm công nghệ trực tuyến: danh mục, biến thể, giỏ hàng, đơn hàng, thanh toán, voucher, khuyến mãi, đánh giá, báo cáo.",
    "Bốn nhóm người dùng: khách vãng lai, khách hàng, nhân viên và quản trị viên.",
  ]),
  h3("1.3.2. Phạm vi nghiên cứu"),
  ...bullets([
    "Website gồm phần cửa hàng cho khách và phần quản trị (khu vực /Admin) cho nhân viên và quản trị viên.",
    "Đặc tả có 37 use case và đồ án hoàn thành đủ 37, kể cả hai use case của nhân viên cửa hàng là nhập hàng vào kho (use case 20) và bán hàng tại quầy (use case 21).",
    "Thanh toán VNPay và MoMo được giả lập ngay trong ứng dụng vì không có tài khoản sandbox; chữ ký số của cổng giả lập làm theo cách VNPay ký (HMAC-SHA512).",
    "Cơ sở dữ liệu khi phát triển là SQLite, nhưng các kiểu dữ liệu được khai báo theo đúng thiết kế cho SQL Server trong đặc tả.",
  ]),
  h2("1.4. Phương pháp thực hiện"),
  p("Đồ án được làm theo từng tính năng nhỏ, mỗi tính năng một nhánh Git và một pull request vào nhánh dev. Với mỗi hành vi có quyết định nghiệp vụ, test được viết trước và chạy để thấy thất bại (RED), sau đó mới viết mã để test qua (GREEN). Sau mỗi tính năng, ứng dụng được chạy thật và kiểm tra trên trình duyệt bằng các kịch bản Playwright, ở cả kích thước màn hình máy tính và điện thoại. Mỗi pull request còn được rà soát lại theo cách của một lập trình viên có kinh nghiệm, tìm các tình huống chạy song song có thể làm sai dữ liệu."),
  h2("1.5. Bố cục báo cáo"),
  ...bullets([
    "Chương 1: Mở đầu, nêu lý do, mục tiêu, phạm vi và phương pháp.",
    "Chương 2: Cơ sở lý thuyết, trình bày các công nghệ ASP.NET Core được dùng và cách chúng xuất hiện trong mã nguồn của đồ án.",
    "Chương 3: Phân tích và thiết kế hệ thống: tác nhân, use case, cơ sở dữ liệu, kiến trúc và các luồng nghiệp vụ chính.",
    "Chương 4: Kết quả thực nghiệm: môi trường, giao diện, kiểm thử và đánh giá.",
    "Chương 5: Kết luận và hướng phát triển.",
  ]),
];

// ---------- chapter 2 ----------

const ch2 = [
  h1("CHƯƠNG 2: CƠ SỞ LÝ THUYẾT"),
  h2("2.1. Tổng quan về website thương mại điện tử bán sản phẩm công nghệ"),
  p("Một website thương mại điện tử bán sản phẩm công nghệ cần giải quyết các việc chính: trình bày danh mục và biến thể sản phẩm, cho khách chọn và đặt mua, nhận thanh toán, theo dõi đơn hàng và cho cửa hàng quản lý hàng hóa, giá, khuyến mãi và doanh thu. Với sản phẩm Apple, một mẫu máy (ví dụ iPhone 18 Pro Max) có nhiều cấu hình dung lượng, mỗi cấu hình lại có nhiều màu và nhiều khu vực phát hành với giá khác nhau. Vì vậy dữ liệu phải tách rõ sản phẩm, cấu hình và từng biến thể có mã SKU, giá và tồn kho riêng."),
  h2("2.2. Nền tảng .NET và ASP.NET Core MVC"),
  h3("2.2.1. Giới thiệu .NET và ASP.NET Core"),
  p(".NET là nền tảng phát triển mã nguồn mở của Microsoft, chạy được trên Windows, macOS và Linux. Đồ án dùng .NET 10, phiên bản hỗ trợ dài hạn (LTS). ASP.NET Core là bộ thư viện web của .NET, dùng để xây dựng website và API. Toàn bộ đồ án được phát triển và chạy trên macOS, điều mà ASP.NET Core hỗ trợ đầy đủ."),
  h3("2.2.2. Mô hình kiến trúc MVC"),
  p("MVC chia ứng dụng thành ba phần. **Model** là dữ liệu và quy tắc nghiệp vụ; **View** là phần giao diện, viết bằng Razor; **Controller** nhận yêu cầu từ trình duyệt, gọi phần nghiệp vụ rồi chọn view để trả về. Trong đồ án, ví dụ ProductsController nhận đường dẫn /Products/{slug}, gọi ProductCatalogService để lấy sản phẩm, rồi trả về view Products/Details.cshtml. Nhờ tách như vậy, quy tắc nghiệp vụ nằm trong các lớp dịch vụ và được kiểm thử riêng, còn controller chỉ điều phối."),
  p("Đồ án dùng attribute routing (khai báo đường dẫn ngay trên controller, ví dụ [Route(\"Products\")]) để có địa chỉ gọn như /Products/iphone-17 thay vì /Products/Details?slug=iphone-17."),
  h3("2.2.3. Razor, Tag Helper, Partial View và View Component"),
  p("Razor là cú pháp trộn HTML với C# trong file .cshtml. Tag Helper là các thuộc tính đặc biệt trong thẻ HTML, ví dụ asp-controller, asp-action để sinh đường dẫn đúng, hay asp-for để gắn ô nhập với thuộc tính của model. Partial view là đoạn giao diện dùng lại ở nhiều trang, ví dụ _ShopCard.cshtml (thẻ sản phẩm) hay _OrderSummary.cshtml (tóm tắt đơn hàng dùng chung cho khách và nhân viên). View Component là một khối giao diện có logic riêng: CartCountViewComponent hiển thị số món trong giỏ trên thanh điều hướng, ProductReviewsViewComponent hiển thị khối đánh giá trên trang sản phẩm."),
  h3("2.2.4. Areas"),
  p("Area là cách ASP.NET Core chia một ứng dụng lớn thành các khu vực có controller và view riêng. Toàn bộ trang quản trị nằm trong Areas/Admin với địa chỉ bắt đầu bằng /Admin. Mỗi controller trong khu vực này được bảo vệ bằng thuộc tính [Authorize(Roles = ...)], ví dụ trang sản phẩm chỉ cho Admin, còn trang đơn hàng, khuyến mãi, giá, kho và bán hàng tại quầy cho cả Admin và Employee."),
  h3("2.2.5. Dependency Injection và Options pattern"),
  p("ASP.NET Core có sẵn cơ chế Dependency Injection (DI): các dịch vụ được đăng ký một lần trong Program.cs, ví dụ builder.Services.AddScoped<ICartService, CartService>(), và được tự động truyền vào constructor của controller khi cần. Nhờ DI, khi kiểm thử có thể thay một dịch vụ bằng phiên bản giả (ví dụ dịch vụ gửi email giả) mà không sửa mã."),
  p("Options pattern dùng để đọc cấu hình thành một lớp C# có kiểm tra hợp lệ. Đồ án dùng SmtpOptions cho thông tin gửi email và PaymentOptions cho cổng thanh toán, kèm ValidateOnStart để ứng dụng từ chối khởi động nếu cấu hình sai, thay vì lỗi khi người dùng đang thao tác. Mật khẩu email được lưu bằng user-secrets trên máy, không nằm trong mã nguồn."),
  h3("2.2.6. Model binding và model validation"),
  p("Model binding tự động chuyển dữ liệu từ form hoặc đường dẫn thành tham số và đối tượng C#. Model validation kiểm tra dữ liệu bằng các thuộc tính như [Required], [StringLength] và kết quả nằm trong ModelState. Đồ án dựa vào ModelState để phát hiện số không đọc được, ví dụ khi nhân viên gõ giá \"24.990.000\" có dấu chấm, trang sẽ báo lỗi thay vì lưu giá rỗng. Riêng các ô số tiền ở trang Giá và trang nhập kho, giá trị \"1.000\" vẫn đọc được thành số 1 (có ba chữ số thập phân) nên được kiểm tra thêm: số tiền phải là số nguyên, không có dấu phân cách."),
  h3("2.2.7. Chống giả mạo yêu cầu (anti-forgery)"),
  p("Mọi form gửi bằng phương thức POST đều kèm một mã chống giả mạo (anti-forgery token), và action nhận form được đánh dấu [ValidateAntiForgeryToken]. Một trang web lạ không thể lấy được mã này nên không thể lừa trình duyệt của người dùng gửi yêu cầu thay họ (tấn công CSRF). Đồ án có một test riêng (FormTokenTests) đi qua tất cả các trang và kiểm tra từng form POST đều có mã này."),
  h2("2.3. Entity Framework Core"),
  h3("2.3.1. ORM và cách tiếp cận Code First"),
  p("Entity Framework Core (EF Core) là thư viện ORM của .NET: các bảng được biểu diễn bằng lớp C# (entity), các truy vấn viết bằng LINQ và EF Core tự sinh câu SQL. Đồ án dùng cách Code First: các lớp entity trong dự án AppleStore.Domain là gốc, cơ sở dữ liệu được tạo ra từ chúng. AppDbContext là lớp đại diện cho phiên làm việc với cơ sở dữ liệu."),
  h3("2.3.2. Cấu hình bằng IEntityTypeConfiguration"),
  p("Mỗi bảng có một lớp cấu hình riêng trong Data/Configurations, khai báo khóa chính, khóa ngoại, độ dài chuỗi (HasMaxLength), độ chính xác của tiền (HasPrecision(12, 2) tương ứng decimal(12,2)) và các chỉ mục duy nhất. Nhờ vậy cơ sở dữ liệu giữ đúng hình dạng thiết kế cho SQL Server dù khi phát triển dùng SQLite."),
  h3("2.3.3. Migration"),
  p("Migration là các bước thay đổi cấu trúc cơ sở dữ liệu được sinh ra từ mã, lưu trong thư mục Migrations và áp dụng bằng lệnh dotnet ef database update. Đồ án có 23 migration, từ lược đồ ban đầu đến các thay đổi về sau như thêm bảng voucher, cột trả lời đánh giá, loại khuyến mãi, bảng lịch sử đổi giá, bảng phiếu nhập kho và các cột cho đơn bán tại quầy. Dữ liệu mẫu (danh mục, sản phẩm, biến thể, ảnh, voucher mẫu) cũng được đưa vào qua migration nên mọi máy đều có cùng dữ liệu."),
  h3("2.3.4. Giao dịch và cập nhật có điều kiện"),
  p("Khi một thao tác cần thay đổi nhiều bảng cùng lúc (ví dụ đặt hàng: trừ tồn kho, tăng lượt dùng voucher, xóa giỏ hàng, tạo đơn), đồ án dùng giao dịch (transaction) của EF Core để tất cả cùng thành công hoặc cùng bị hủy. Để chống hai yêu cầu chạy song song ghi đè nhau, đồ án dùng cập nhật có điều kiện (compare-and-swap) bằng ExecuteUpdateAsync: chỉ trừ tồn kho khi số tồn vẫn đủ, chỉ chuyển trạng thái đơn khi đơn vẫn ở trạng thái đã thấy, chỉ lưu sửa đổi của quản trị viên khi phiên bản dữ liệu chưa đổi."),
  h2("2.4. ASP.NET Core Identity"),
  p("ASP.NET Core Identity là hệ thống tài khoản có sẵn: băm mật khẩu, đăng nhập bằng cookie, khóa tài khoản khi nhập sai nhiều lần, vai trò và quyền. Đồ án dùng UserManager và SignInManager của Identity nhưng giữ nguyên bảng Users theo thiết kế của đặc tả, bằng một UserStore tự viết (Infrastructure/Identity/UserStore.cs) để Identity đọc và ghi vào bảng này. Bốn cột phục vụ Identity được thêm bằng migration: NormalizedEmail, SecurityStamp, AccessFailedCount và LockoutEnd."),
  ...bullets([
    "Mật khẩu tối thiểu 8 ký tự; nhập sai 5 lần thì tài khoản bị khóa 5 phút (giá trị mặc định của Identity).",
    "Vai trò Customer, Employee, Admin được đưa vào cookie đăng nhập và kiểm tra bằng [Authorize(Roles = ...)].",
    "Security stamp được kiểm tra ở mọi yêu cầu, nên khi quản trị viên đổi vai trò hoặc người dùng đổi mật khẩu, các phiên đăng nhập khác hết hiệu lực ngay.",
    "Tài khoản quản trị đầu tiên được tạo khi ứng dụng khởi động (AdminSeeder) từ cấu hình trong user-secrets, qua UserManager nên cũng phải theo quy tắc mật khẩu.",
  ]),
  h2("2.5. Hệ quản trị cơ sở dữ liệu"),
  p("Đặc tả thiết kế cơ sở dữ liệu cho SQL Server. Máy phát triển là macOS, không có SQL Server chạy trực tiếp, nên đồ án dùng SQLite khi phát triển: không cần máy chủ, không cần tài khoản và được EF Core hỗ trợ đầy đủ. Các kiểu dữ liệu theo thiết kế SQL Server (nvarchar, decimal(12,2), datetime2) vẫn được khai báo trong cấu hình EF Core, nên khi chuyển sang SQL Server chỉ cần đổi nhà cung cấp và chuỗi kết nối (hướng dẫn trong docs/architecture.md)."),
  p("Một điểm cần chú ý: SQLite lưu số thập phân dưới dạng chuỗi. Các test của đồ án dùng giá trị như 6.490.000 so với mốc 10.000.000 để chứng minh EF Core vẫn so sánh theo giá trị số chứ không theo thứ tự chuỗi."),
  h2("2.6. Các thư viện và công nghệ hỗ trợ"),
  ...table("2", "Thư viện và công nghệ sử dụng", ["Thành phần", "Vai trò trong đồ án"], [
    ["Bootstrap, jQuery", "Bố cục và thành phần giao diện cơ bản đi kèm mẫu dự án ASP.NET Core"],
    ["CSS và JavaScript riêng (effects.css, effects.js)", "Giao diện tối, hiệu ứng hiện dần khi cuộn, chọn màu và khu vực trên trang sản phẩm"],
    ["GSAP, Lenis, three.js", "Hiệu ứng chuyển động và cảnh 3D ở phần đầu trang chủ"],
    ["MailKit 4.18.1", "Gửi email mã OTP và email đơn hàng qua Gmail SMTP"],
    ["ClosedXML 0.105.1", "Xuất báo cáo doanh thu ra file Excel (.xlsx) thật"],
    ["HMAC-SHA512", "Ký và kiểm tra chữ ký của cổng thanh toán giả lập, theo cách VNPay ký"],
    ["xUnit 2.9.3", "Viết và chạy test tự động"],
    ["Microsoft.AspNetCore.Mvc.Testing 10.0.12", "Chạy cả ứng dụng web trong bộ nhớ để test (WebApplicationFactory)"],
    ["Playwright (playwright-cli)", "Điều khiển trình duyệt Chromium thật để kiểm tra giao diện và luồng nghiệp vụ"],
  ], [3, 5]),
  h2("2.7. Kiểm thử phần mềm"),
  h3("2.7.1. Kiểm thử tự động với xUnit"),
  p("Test dịch vụ chạy trên cơ sở dữ liệu SQLite trong bộ nhớ với lược đồ thật, nên các ràng buộc như khóa ngoại và chỉ mục duy nhất được kiểm tra thật. Test giao diện dùng WebApplicationFactory để chạy toàn bộ ứng dụng (định tuyến, Identity, cookie, anti-forgery) trong bộ nhớ và gửi yêu cầu HTTP như một trình duyệt. Thời gian được thay bằng đồng hồ cố định (FixedTime dựa trên TimeProvider) để test về hạn voucher, khuyến mãi hay mã OTP không phụ thuộc giờ chạy."),
  p("Để kiểm tra tình huống hai yêu cầu chạy cùng lúc, đồ án dùng một interceptor của EF Core (SqlRace) chèn câu SQL của yêu cầu thứ hai vào đúng thời điểm trước khi yêu cầu thứ nhất ghi, rồi kiểm tra dữ liệu cuối cùng vẫn đúng."),
  h3("2.7.2. Phát triển hướng kiểm thử (TDD) và kiểm thử đột biến"),
  p("Với mỗi hành vi, test được viết trước và commit riêng ở trạng thái thất bại (RED), sau đó mới commit phần mã làm test qua (GREEN). Sau khi xong, các dòng mã quan trọng được cố ý làm sai (kiểm thử đột biến) để chắc chắn có test phát hiện; nếu không có test nào thất bại thì phải viết thêm test chặt hơn."),
  h3("2.7.3. Kiểm thử trên trình duyệt thật với Playwright"),
  p("Mỗi tính năng có một kịch bản trong thư mục setup/verify-*/. Kịch bản tạo một cơ sở dữ liệu mới bằng chính các migration, khởi động ứng dụng thật, điều khiển trình duyệt Chromium làm như người dùng (đăng ký, nhận OTP, mua hàng, quản trị...), đo giao diện ở chiều rộng 1440 điểm ảnh và 390 điểm ảnh (điện thoại), so sánh dữ liệu với cơ sở dữ liệu và in kết quả đạt hoặc không đạt cho từng mục."),
  h2("2.8. Công cụ phát triển và quản lý mã nguồn"),
  ...bullets([
    "dotnet CLI để tạo, build, chạy và test dự án; dotnet-ef (cài như công cụ cục bộ) để tạo và áp dụng migration; dotnet format để kiểm tra định dạng mã.",
    "Git và GitHub: hai nhánh lâu dài main (phát hành) và dev (tích hợp), mỗi tính năng một nhánh feat/, fix/ hoặc docs/ và gộp vào dev qua pull request.",
    "Kho mã nguồn: BDucky/ASPNET-VX24TTK7-LeBinh-applestore, tổ chức theo cấu trúc thư mục môn học yêu cầu (setup/, scr/, progress-report/, thesis/).",
  ]),
];

// ---------- chapter 3 ----------

const useCases = [
  ["1", "Đăng ký tài khoản (OTP)", "Khách", "Đã làm"],
  ["2", "Gửi OTP qua email", "Khách", "Đã làm"],
  ["3", "Đăng nhập", "Khách", "Đã làm"],
  ["4", "Quên mật khẩu (OTP)", "Khách", "Đã làm"],
  ["5", "Đổi mật khẩu", "Khách hàng", "Đã làm"],
  ["6", "Cập nhật thông tin cá nhân", "Khách hàng", "Đã làm"],
  ["7", "Tìm kiếm sản phẩm", "Khách", "Đã làm"],
  ["8", "Lọc sản phẩm", "Khách", "Đã làm"],
  ["9", "Xem chi tiết sản phẩm", "Khách", "Đã làm"],
  ["10", "So sánh sản phẩm", "Khách", "Đã làm"],
  ["11", "Thêm sản phẩm vào giỏ", "Khách hàng", "Đã làm"],
  ["12", "Xóa sản phẩm khỏi giỏ", "Khách hàng", "Đã làm"],
  ["13", "Cập nhật số lượng trong giỏ", "Khách hàng", "Đã làm"],
  ["14", "Xem giỏ hàng", "Khách hàng", "Đã làm"],
  ["15", "Đặt hàng", "Khách hàng", "Đã làm"],
  ["16", "Áp dụng voucher", "Khách hàng", "Đã làm"],
  ["17", "Thanh toán (COD, VNPay, MoMo)", "Khách hàng", "Đã làm (VNPay, MoMo giả lập)"],
  ["18", "Tra cứu đơn hàng", "Khách hàng", "Đã làm"],
  ["19", "Theo dõi vận chuyển", "Khách hàng", "Đã làm"],
  ["20", "Nhập hàng vào kho", "Nhân viên, quản trị viên", "Đã làm"],
  ["21", "Bán hàng tại quầy", "Nhân viên, quản trị viên", "Đã làm"],
  ["22", "Xác nhận đơn hàng", "Nhân viên", "Đã làm"],
  ["23", "Gán mã vận đơn", "Nhân viên", "Đã làm"],
  ["24", "Cập nhật trạng thái đơn", "Nhân viên", "Đã làm"],
  ["25", "Thêm sản phẩm mới", "Quản trị viên", "Đã làm"],
  ["26", "Sửa sản phẩm", "Quản trị viên", "Đã làm"],
  ["27", "Xóa sản phẩm (ngừng bán)", "Quản trị viên", "Đã làm"],
  ["28", "Tạo chương trình khuyến mãi", "Quản trị viên, nhân viên", "Đã làm"],
  ["29", "Thêm voucher", "Quản trị viên", "Đã làm"],
  ["30", "Sửa voucher", "Quản trị viên", "Đã làm"],
  ["31", "Xóa voucher", "Quản trị viên", "Đã làm"],
  ["32", "Gửi đánh giá sản phẩm", "Khách hàng", "Đã làm"],
  ["33", "Báo cáo tình hình kinh doanh", "Nhân viên, quản trị viên", "Đã làm"],
  ["34", "Báo cáo doanh thu", "Nhân viên, quản trị viên", "Đã làm"],
  ["35", "Xuất báo cáo (Excel, PDF)", "Nhân viên, quản trị viên", "Đã làm"],
  ["36", "In hóa đơn", "Khách hàng, nhân viên", "Đã làm (có in nhiều hóa đơn cùng lúc)"],
  ["37", "Gửi email xác nhận đơn", "Hệ thống", "Đã làm"],
];

const ch3 = [
  h1("CHƯƠNG 3: PHÂN TÍCH VÀ THIẾT KẾ HỆ THỐNG"),
  h2("3.1. Phân tích yêu cầu hệ thống"),
  h3("3.1.1. Tác nhân"),
  ...table("3", "Các tác nhân của hệ thống", ["Tác nhân", "Trách nhiệm chính"], [
    ["Khách (Guest)", "Đăng ký bằng OTP, đăng nhập, quên mật khẩu, tìm, lọc, xem và so sánh sản phẩm"],
    ["Khách hàng (Customer)", "Mọi việc của khách, cộng thêm: cập nhật hồ sơ, quản lý địa chỉ giao hàng, giỏ hàng, đặt hàng, thanh toán COD hoặc trực tuyến, theo dõi đơn, nhận email, đánh giá sản phẩm đã mua"],
    ["Nhân viên (Employee)", "Kiểm tra tồn kho, nhập hàng và lập phiếu nhập, bán hàng tại quầy, xử lý đơn hàng (xác nhận, giao vận chuyển, cập nhật trạng thái), cập nhật giá và chạy khuyến mãi, trả lời và ẩn đánh giá, xem báo cáo doanh thu, in hóa đơn"],
    ["Quản trị viên (Admin)", "Quản lý sản phẩm, biến thể, ảnh, voucher, khuyến mãi, tài khoản và vai trò, xem báo cáo tổng hợp"],
  ], [2, 6]),
  h3("3.1.2. Yêu cầu chức năng"),
  p("Đặc tả nêu các quy tắc nghiệp vụ theo từng biểu mẫu (BM). Bảng 3.2 tóm tắt các quy tắc chính và cách đồ án hiện thực."),
  ...table("3", "Quy tắc nghiệp vụ chính và cách hiện thực", ["Chức năng", "Quy tắc", "Hiện thực trong đồ án"], [
    ["Tìm kiếm và lọc", "Theo tên, danh mục, giá; sắp xếp theo giá hoặc mới nhất", "Tìm theo tên, lọc theo danh mục và 4 khoảng giá, sắp theo giá hoặc mới nhất"],
    ["Giỏ hàng và đặt hàng (BM_CART_CHECKOUT_01)", "Tổng = tổng (số lượng x giá) - giảm giá + phí vận chuyển", "Tính trong CheckoutService, đặt hàng trong một giao dịch; phí vận chuyển miễn phí"],
    ["Voucher (BM_VOUCHER_01)", "Giảm theo % hoặc số tiền; cho mọi sản phẩm hoặc một số sản phẩm; đơn tối thiểu; giới hạn lượt và thời gian", "Bảng Vouchers và VoucherProducts; kiểm tra đủ các điều kiện khi tính tiền và khi đặt"],
    ["Quản lý đơn (BM_ORDER_HISTORY_01)", "Khách xem lịch sử đơn; nhân viên cập nhật trạng thái", "Trang Đơn của tôi; trang quản lý đơn theo một bảng quy tắc chuyển trạng thái"],
    ["Giá và khuyến mãi (BM_PRICE_01)", "Cập nhật hàng loạt, lưu lịch sử đổi giá, giá mới tự áp dụng", "Trang Giá đổi hàng loạt theo % hoặc số tiền; bảng PriceChanges; khuyến mãi tự bật tắt theo thời gian"],
    ["Báo cáo doanh thu (BM_REPORT_REVENUE_01)", "Theo ngày, có sản phẩm bán chạy", "Doanh thu theo ngày và theo sản phẩm; chỉ tính đơn đã thanh toán và không bị hủy; xuất Excel và PDF"],
    ["Nhập kho (BM_STOCK_01)", "Hàng nhập từ nhà cung cấp; kiểm tra trùng SKU; tồn cuối = tồn đầu + nhập", "Phiếu nhập StockReceipts, mỗi dòng ghi tồn trước và sau; một biến thể xuất hiện hai lần trong phiếu thì bị từ chối"],
    ["Bán hàng, hóa đơn (BM_INVOICE_01)", "Tổng = tổng (số lượng x đơn giá) - giảm giá + phí vận chuyển + thuế; in hóa đơn hàng loạt", "Đơn bán tại quầy là một đơn hàng loại Tại quầy; giá đã gồm VAT nên không có dòng thuế, không có phí vận chuyển; in nhiều hóa đơn cùng lúc từ danh sách đơn"],
    ["Tra cứu đơn (BM_ORDER_TRACK_01)", "Theo mã đơn và số điện thoại; trả về trạng thái và mã vận đơn; báo qua email", "Trang Tra cứu đơn; email khi đặt hàng và khi giao cho đơn vị vận chuyển"],
  ], [2.2, 3, 3.6]),
  h3("3.1.3. Yêu cầu phi chức năng"),
  ...table("3", "Yêu cầu phi chức năng", ["Nhóm", "Yêu cầu trong đặc tả", "Cách đáp ứng"], [
    ["Bảo mật", "HTTPS; phiên đăng nhập an toàn; giới hạn số lần thử OTP và đăng nhập", "Chuyển hướng HTTPS và HSTS; cookie của Identity (thay cho JWT vì đây là ứng dụng MVC dựng trang ở máy chủ); khóa tài khoản sau 5 lần sai; mã OTP đăng ký hủy sau 5 lần sai, mã đặt lại mật khẩu sai 5 lần thì khóa tài khoản; anti-forgery cho mọi form"],
    ["Dễ sử dụng", "Tìm kiếm, lọc và đặt hàng dễ dàng", "Giao diện theo trang tham khảo rauvang.com; mọi lỗi có thông báo rõ và đường quay lại; dùng được trên điện thoại"],
    ["Mở rộng", "Thêm cổng thanh toán hoặc tính năng mới không phải làm lại", "Cổng thanh toán qua giao diện IPaymentGateway; dịch vụ nghiệp vụ tách khỏi controller"],
    ["Trao đổi dữ liệu", "Xuất Excel, PDF", "Báo cáo xuất Excel bằng ClosedXML; báo cáo và hóa đơn in hoặc lưu PDF từ trình duyệt"],
    ["Bảo trì", "Ghi log rõ ràng", "ILogger ở mọi chỗ xử lý lỗi cơ sở dữ liệu và email; tài liệu trong thư mục docs/"],
  ], [1.5, 3, 4.2]),
  h2("3.2. Đặc tả các trường hợp sử dụng"),
  h3("3.2.1. Danh sách use case và trạng thái"),
  p("Đặc tả có 37 use case. Bảng 3.4 liệt kê toàn bộ cùng tình trạng hiện thực: cả 37 use case đều đã làm."),
  ...table("3", "Danh sách 37 use case", ["STT", "Use case", "Tác nhân", "Trạng thái"], useCases, [0.6, 3.6, 2.4, 2.2]),
  h3("3.2.2. Đặc tả use case Đăng ký tài khoản (use case 1, 2)"),
  ...numbered([
    "Khách mở trang Đăng ký, nhập email, họ tên, số điện thoại, mật khẩu và nhập lại mật khẩu.",
    "Hệ thống kiểm tra dữ liệu hợp lệ, email và số điện thoại chưa được dùng, mật khẩu đủ 8 ký tự.",
    "Hệ thống tạo mã OTP 6 chữ số có hạn 5 phút, giữ thông tin đăng ký tạm trong bộ nhớ đệm và gửi mã qua email. Tài khoản chưa được tạo ở bước này.",
    "Khách nhập mã OTP. Nếu đúng, hệ thống tạo tài khoản qua UserManager của Identity và đăng nhập luôn.",
    "Nếu nhập sai 5 lần, lần đăng ký bị hủy và khách được mời đăng ký lại.",
  ]),
  h3("3.2.3. Đặc tả use case Đặt hàng và áp dụng voucher (use case 15, 16)"),
  ...numbered([
    "Khách hàng đã đăng nhập mở trang Thanh toán từ giỏ hàng; địa chỉ mặc định được điền sẵn.",
    "Khách nhập mã voucher và bấm Áp dụng; hệ thống kiểm tra voucher còn hoạt động, còn trong thời gian, còn lượt, đủ giá trị đơn tối thiểu và áp cho đúng sản phẩm, rồi tính lại tổng.",
    "Khách chọn phương thức thanh toán (COD, VNPay, MoMo) và bấm Đặt hàng.",
    "Trong một giao dịch, hệ thống tính lại giá, so với tổng khách đã thấy, trừ tồn kho có điều kiện, tăng lượt dùng voucher có điều kiện, xóa các dòng giỏ đã đặt và tạo đơn.",
    "Nếu tổng tiền đã thay đổi (ví dụ khuyến mãi vừa bắt đầu hoặc kết thúc) hoặc hết hàng, giao dịch bị hủy và khách được báo để xem lại; bấm đặt hai lần chỉ tạo một đơn.",
  ]),
  h3("3.2.4. Đặc tả use case Thanh toán trực tuyến (use case 17)"),
  ...numbered([
    "Khách chọn VNPay hoặc MoMo; hệ thống tạo một lần thanh toán và chuyển khách sang trang cổng thanh toán (giả lập).",
    "Cổng trả kết quả về kèm chữ ký HMAC-SHA512; hệ thống kiểm tra chữ ký và số tiền.",
    "Chỉ khi chữ ký đúng, số tiền khớp và lần thanh toán còn đang chờ thì đơn mới được đánh dấu đã thanh toán; kết quả gửi lại lần hai không được tính hai lần.",
    "Nếu thanh toán thất bại hoặc bị hủy, trang đơn hàng hiện nút Thanh toán lại; đơn đã hủy thì không thể thanh toán.",
  ]),
  h3("3.2.5. Đặc tả use case Xử lý đơn hàng (use case 22, 23, 24)"),
  ...table("3", "Quy tắc chuyển trạng thái đơn hàng (OrderTransitions)", ["Hành động", "Nhân viên làm được khi đơn ở trạng thái", "Khách hàng làm được khi", "Trạng thái mới"], [
    ["Xác nhận", "Chờ xác nhận (đơn trực tuyến phải đã thanh toán)", "Không", "Đã xác nhận"],
    ["Giao vận chuyển (kèm đơn vị và mã vận đơn)", "Đã xác nhận", "Không", "Đang giao"],
    ["Hoàn tất", "Đang giao (đơn COD được ghi nhận đã thanh toán)", "Không", "Hoàn tất"],
    ["Hủy", "Chờ xác nhận hoặc Đã xác nhận", "Chờ xác nhận", "Đã hủy (trả lại tồn kho và lượt voucher)"],
  ], [2, 3, 2, 2]),
  p("Hai nhân viên bấm cùng một nút, hoặc khách hủy đúng lúc nhân viên xác nhận, chỉ có một thay đổi được áp dụng vì mỗi lần chuyển trạng thái là một cập nhật có điều kiện trên trạng thái cũ. Đơn bán tại quầy được tạo ở trạng thái Hoàn tất nên không ai hủy hay thanh toán lại được."),
  h3("3.2.6. Đặc tả use case Nhập hàng vào kho (use case 20)"),
  ...numbered([
    "Nhân viên mở trang Nhập hàng; hệ thống chuyển sang địa chỉ có mã form riêng (/Admin/Stock/New?key=...).",
    "Nhân viên nhập tên nhà cung cấp, chọn từng sản phẩm, số lượng và giá nhập; nút Thêm dòng thêm dòng mới ngay trên trang.",
    "Hệ thống kiểm tra: có nhà cung cấp, có ít nhất một dòng, số lượng từ 1 trở lên, giá nhập từ 0 tới giá lớn nhất được lưu, không có biến thể nào xuất hiện hai lần (kiểm tra trùng SKU của BM_STOCK_01).",
    "Trong một giao dịch, mỗi dòng cộng số lượng vào tồn kho ngay trong cơ sở dữ liệu (StockQty + n) rồi đọc lại tồn sau; tồn trước được tính bằng tồn sau trừ số lượng, nên một đơn bán chen vào đúng lúc đó vẫn được giữ và công thức tồn cuối = tồn đầu + nhập luôn đúng.",
    "Phiếu nhập được lưu kèm người lập và thời điểm; nhân viên được chuyển tới trang phiếu, có thể in phiếu.",
    "Bấm lưu lần hai, bấm Quay lại của trình duyệt rồi lưu, hay mở trùng tab đều không nhập hàng hai lần, vì mã form là duy nhất và nằm trong địa chỉ trang.",
  ]),
  h3("3.2.7. Đặc tả use case Bán hàng tại quầy (use case 21)"),
  ...numbered([
    "Nhân viên mở trang Bán hàng tại quầy (cũng có mã form trong địa chỉ), chọn sản phẩm và số lượng, có thể nhập mã voucher và thông tin khách.",
    "Nhân viên bấm Tính tiền: hệ thống tính giá đang bán (có khuyến mãi), áp voucher theo đúng quy tắc của bán trực tuyến và báo các vấn đề như hết hàng hoặc voucher không dùng được.",
    "Nút Hoàn tất bán chỉ hiện khi bảng tính tiền không có vấn đề nào. Nhân viên chọn tiền mặt hoặc chuyển khoản rồi bấm Hoàn tất bán.",
    "Hệ thống chỉ bán đúng những dòng đã tính tiền và đúng tổng đã hiển thị; nếu giá, khuyến mãi hay các dòng đã đổi thì yêu cầu tính lại.",
    "Trong một giao dịch, hệ thống trừ tồn kho có điều kiện, tăng lượt dùng voucher có điều kiện, tạo một đơn hàng loại Tại quầy ở trạng thái Hoàn tất, Đã thanh toán, kèm lần thanh toán và người bán.",
    "Khách lẻ không cần tài khoản; nếu nhập email của một tài khoản có sẵn, đơn được gắn vào tài khoản đó và hiện trong Đơn của tôi. Giá đã gồm VAT nên hóa đơn ghi \"Giá đã bao gồm VAT\" và không có dòng thuế.",
  ]),
  h2("3.3. Thiết kế cơ sở dữ liệu"),
  h3("3.3.1. Tổng quan"),
  p("Lược đồ gồm 24 bảng theo thiết kế của đặc tả, tên cột giữ nguyên, cộng thêm năm bảng do nghiệp vụ cần: Vouchers, VoucherProducts, PriceChanges, StockReceipts và StockReceiptLines. Các nhóm bảng chính:"),
  ...bullets([
    "Tài khoản: Users, UserTokens, Addresses.",
    "Danh mục và sản phẩm: Categories, Products, ProductVariants, OptionTypes, OptionValues, VariantOptions, ProductImages, Attributes, CategoryAttributes, ProductAttributeValues.",
    "Mua hàng: Carts, CartItems, Orders, OrderItems, Payments, Shipments, Vouchers, VoucherProducts.",
    "Tương tác: Reviews, ReviewMedia, Favorites, CompareLists, CompareItems.",
    "Giá và kho: PriceChanges, StockReceipts, StockReceiptLines.",
  ]),
  h3("3.3.2. Các bảng chính"),
  ...table("3", "Bảng Products và ProductVariants", ["Bảng", "Các cột chính", "Ghi chú"], [
    ["Products", "Id, CategoryId, Name, Slug, Description, BasePrice, Status, SortOrder, CreatedAt, UpdatedAt", "Một mẫu máy; Status = đang bán; ngừng bán thay cho xóa để đơn cũ còn nguyên"],
    ["ProductVariants", "Id, ProductId, SKU, Price, StockQty, Status, CreatedAt, UpdatedAt", "Một biến thể (cấu hình, màu, khu vực); Price rỗng nghĩa là Liên hệ; UpdatedAt dùng làm phiên bản để chống ghi đè"],
    ["VariantOptions, OptionTypes, OptionValues", "VariantId, OptionTypeId, OptionValueId", "Gắn cấu hình (config), màu (color), khu vực (region) cho từng biến thể"],
  ], [2, 4, 3]),
  ...table("3", "Bảng đơn hàng và thanh toán", ["Bảng", "Các cột chính", "Ghi chú"], [
    ["Orders", "Id, UserId, Channel, SoldByUserId, FormKey, OrderStatus, PaymentStatus, Subtotal, DiscountAmount, ShippingFee, TotalAmount, VoucherCode, thông tin người nhận, Note, CreatedAt, UpdatedAt", "Trạng thái: Chờ xác nhận, Đã xác nhận, Đang giao, Hoàn tất, Đã hủy. Channel: Trực tuyến hoặc Tại quầy"],
    ["OrderItems", "Id, OrderId, ProductId, VariantId, Price, Quantity", "Price là giá thực tính lúc đặt (đã trừ khuyến mãi)"],
    ["Payments", "Id, OrderId, Method, Status, TxnId, PaidAmount, PaidAt, ProviderRaw, CreatedAt", "Mỗi lần thanh toán một dòng; giữ dữ liệu gốc cổng trả về. Method: COD, VNPay, MoMo, tiền mặt, chuyển khoản"],
    ["Shipments", "Id, OrderId, Carrier, TrackingNo, Status, Fee", "Đơn vị vận chuyển và mã vận đơn"],
  ], [2, 4, 3]),
  ...table("3", "Các bảng thêm so với đặc tả", ["Bảng hoặc cột", "Nội dung", "Lý do"], [
    ["Vouchers, VoucherProducts", "Mã, loại giảm (% hoặc số tiền), mức giảm, đơn tối thiểu, thời gian, giới hạn lượt, số lượt đã dùng, sản phẩm áp dụng", "Đặc tả chỉ lưu mã voucher dạng chữ trong Orders nhưng quy tắc BM_VOUCHER_01 cần đủ các thông tin này"],
    ["Vouchers.Kind, Vouchers.Name", "Kind = Code (voucher nhập mã) hoặc Automatic (khuyến mãi tự áp dụng); Name là tên chương trình", "Khuyến mãi dùng chung bảng với voucher để không phải chép lại trang quản lý"],
    ["PriceChanges", "Biến thể, giá cũ, giá mới, nguồn thay đổi, người đổi, thời điểm", "BM_PRICE_01 yêu cầu lưu lịch sử đổi giá"],
    ["StockReceipts, StockReceiptLines", "Phiếu: nhà cung cấp, ghi chú, mã form (duy nhất), người lập, thời điểm. Dòng: biến thể, số lượng, giá nhập, tồn trước, tồn sau", "Use case 20 và BM_STOCK_01 cần phiếu nhập; đặc tả chưa có bảng nào để lưu"],
    ["Orders: UserId cho phép rỗng; Channel, SoldByUserId, FormKey", "Khách lẻ không có tài khoản; kênh bán; nhân viên bán; mã form chống bán trùng", "Use case 21: lần bán tại quầy được lưu như một đơn hàng để dùng chung hóa đơn, báo cáo doanh thu và quy tắc trừ kho"],
    ["PaymentMethod: Cash, BankTransfer", "Tiền mặt, chuyển khoản tại quầy", "Hai cách trả tiền khi mua tại cửa hàng"],
    ["Users: NormalizedEmail, SecurityStamp, AccessFailedCount, LockoutEnd", "Các cột Identity cần", "Dùng ASP.NET Core Identity trên bảng Users của đặc tả"],
    ["Reviews: Reply, RepliedAt, UpdatedAt", "Trả lời của cửa hàng", "Đặc tả cho nhân viên trả lời đánh giá nhưng chưa có cột lưu"],
  ], [2.4, 3.6, 3]),
  h3("3.3.3. Mối quan hệ giữa các bảng"),
  ...bullets([
    "Một danh mục có nhiều sản phẩm; một sản phẩm có nhiều biến thể và nhiều ảnh.",
    "Một người dùng có một giỏ hàng (chỉ mục duy nhất trên UserId), nhiều địa chỉ, nhiều đơn hàng và nhiều đánh giá.",
    "Một giỏ hàng có nhiều dòng, mỗi biến thể chỉ một dòng (chỉ mục duy nhất trên CartId và VariantId).",
    "Một đơn hàng có nhiều dòng hàng, nhiều lần thanh toán và một hoặc nhiều lần vận chuyển. Đơn bán tại quầy có thể không gắn với tài khoản nào (khách lẻ) và gắn với nhân viên bán.",
    "Một phiếu nhập có nhiều dòng, mỗi biến thể chỉ một dòng (chỉ mục duy nhất trên ReceiptId và VariantId); biến thể đã có trong phiếu nhập thì không xóa được.",
    "Mỗi khách hàng chỉ có một đánh giá cho một sản phẩm (chỉ mục duy nhất trên ProductId và UserId).",
    "Một voucher hoặc khuyến mãi có thể áp cho một số sản phẩm qua VoucherProducts; không có dòng nào nghĩa là áp cho mọi sản phẩm.",
  ]),
  h3("3.3.4. Một số điều chỉnh so với thiết kế gốc"),
  ...bullets([
    "Giá sản phẩm và biến thể cho phép rỗng, nghĩa là \"Liên hệ\", thay vì lưu số 0 có thể bị cộng nhầm vào tổng đơn.",
    "Trạng thái đơn hàng có 5 giá trị (thêm Đã xác nhận) vì đặc tả nêu miền giá trị từ 0 đến 4 nhưng chỉ đặt tên 4 giá trị.",
    "Mã OTP đăng ký giữ trong bộ nhớ đệm chứ không lưu vào UserTokens, vì tài khoản chỉ được tạo sau khi OTP đúng. Mã đặt lại mật khẩu được lưu trong UserTokens dưới dạng đã băm.",
    "Danh sách so sánh lưu trong cookie để khách chưa đăng nhập cũng dùng được, nên hai bảng CompareLists và CompareItems được giữ nguyên nhưng chưa dùng.",
    "Năm sản phẩm chưa có ảnh được phép sử dụng của đúng mẫu máy hiển thị một hình vẽ theo loại sản phẩm (đồng hồ, máy tính bảng, pin sạc...) thay cho ảnh của đời máy trước.",
  ]),
  h2("3.4. Thiết kế kiến trúc"),
  ...table("3", "Các dự án trong giải pháp", ["Dự án", "Nội dung", "Phụ thuộc"], [
    ["AppleStore.Domain", "Các lớp entity và enum, không phụ thuộc thư viện nào", "Không"],
    ["AppleStore.Infrastructure", "AppDbContext, cấu hình EF Core, migration, Identity (UserStore), các dịch vụ nghiệp vụ, thanh toán, email", "Domain"],
    ["AppleStore.Web", "Controller, view Razor, Area Admin, view component, wwwroot, Program.cs", "Infrastructure, Domain"],
    ["AppleStore.Tests", "Test xUnit cho dịch vụ và cho toàn ứng dụng web", "Web, Infrastructure, Domain"],
  ], [2.4, 4.6, 2]),
  p("Một yêu cầu đi qua các bước: trình duyệt gửi yêu cầu, ASP.NET Core định tuyến tới controller, model binding tạo đối tượng từ form, controller gọi dịch vụ nghiệp vụ trong Infrastructure, dịch vụ đọc ghi qua AppDbContext, controller trả về view Razor hoặc chuyển hướng kèm thông báo."),
  p("Một số quy tắc được gom về một nơi duy nhất để mọi trang không thể hiểu khác nhau: OrderTransitions cho chuyển trạng thái đơn, DiscountMath cho công thức giảm giá, SalePrices cho giá đang bán, SaleRules cho kiểm tra voucher, trừ kho và tăng lượt voucher (dùng chung cho đặt hàng trực tuyến và bán tại quầy), PriceLimits cho giá lớn nhất được lưu, FormKeys cho mã form chống lưu trùng (dùng chung cho phiếu nhập và bán tại quầy), VariantText cho tên biến thể trên các trang nhân viên, NoPhoto cho hình thay thế khi chưa có ảnh."),
  h2("3.5. Thiết kế các quy tắc tính tiền"),
  ...bullets([
    "Giá đang bán của một biến thể = giá gốc trừ mức giảm lớn nhất trong các khuyến mãi đang chạy áp cho sản phẩm đó; một mức giảm làm giá về 0 thì không được áp dụng.",
    "Tạm tính = tổng (số lượng x giá đang bán) của các dòng mua được.",
    "Giảm giá voucher tính trên tạm tính đã trừ khuyến mãi; giảm theo % được làm tròn tới đồng, không vượt quá phần được áp dụng.",
    "Tổng tiền = tạm tính - giảm giá voucher + phí vận chuyển (hiện miễn phí). Giá bán đã bao gồm VAT nên không cộng thêm thuế; đơn bán tại quầy không có phí vận chuyển.",
    "Doanh thu trong báo cáo chỉ tính các đơn đã thanh toán và không bị hủy, gom theo ngày giờ Việt Nam (UTC+7).",
    "Đổi giá hàng loạt theo %: giá mới được làm tròn tới 1.000 đồng; nếu có biến thể nào về 0 hoặc vượt giá lớn nhất thì không thay đổi gì.",
  ]),
];

// ---------- chapter 4 ----------

const ch4 = [
  h1("CHƯƠNG 4: KẾT QUẢ THỰC NGHIỆM"),
  h2("4.1. Môi trường phát triển"),
  ...table("4", "Phần mềm và công cụ", ["Thành phần", "Phiên bản hoặc ghi chú"], [
    ["Hệ điều hành", "macOS"],
    ["Nền tảng", ".NET 10 (SDK 10.0.400)"],
    ["Web framework", "ASP.NET Core MVC, Razor"],
    ["ORM", "Entity Framework Core 10.0.12, Code First"],
    ["Cơ sở dữ liệu", "SQLite (khi phát triển); lược đồ theo kiểu SQL Server"],
    ["Kiểm thử", "xUnit 2.9.3, Microsoft.AspNetCore.Mvc.Testing 10.0.12, playwright-cli với Chromium"],
    ["Quản lý mã nguồn", "Git, GitHub (pull request vào nhánh dev)"],
  ], [3, 5]),
  h2("4.2. Cấu trúc mã nguồn"),
  ...table("4", "Các thành phần chính trong mã nguồn", ["Thư mục", "Nội dung"], [
    ["src/AppleStore.Web/Controllers", "Account, Addresses, Products, Cart, Checkout, Payments, PaymentSimulator, Orders, Track, Compare, Reviews, Home"],
    ["src/AppleStore.Web/Areas/Admin/Controllers", "Dashboard, Orders, Products, Vouchers, Promotions (chung lớp nền DiscountAdminController), Prices, Stock, Sales, Reports, Reviews, Users"],
    ["src/AppleStore.Infrastructure/Services", "CartService, CheckoutService, OrderManagementService, ProductCatalogService, CompareService, ReviewService, ReportService, PriceService, SalePrices, SaleRules, StockService, InStoreSaleService, AdminCatalogService, AdminVoucherService, AdminUserService, RegistrationService, PasswordResetService"],
    ["src/AppleStore.Infrastructure/Payments", "PaymentService, SimulatedPaymentGateway, PaymentSignature (HMAC-SHA512)"],
    ["src/AppleStore.Infrastructure/Identity", "UserStore, AdminSeeder, AppUserClaimsPrincipalFactory"],
    ["tests/AppleStore.Tests", "636 test xUnit"],
    ["setup/verify-*", "15 kịch bản kiểm tra trên trình duyệt; setup/screenshots chụp các hình trong chương này; setup/report dựng file báo cáo này"],
  ], [3.2, 5]),
  p("Cách chạy ứng dụng: dotnet tool restore, dotnet ef database update (tạo cơ sở dữ liệu và dữ liệu mẫu), rồi dotnet run --project src/AppleStore.Web. Hướng dẫn đầy đủ có trong README.md."),
  h2("4.3. Giao diện phía khách hàng"),
  p("Các hình dưới đây được chụp từ ứng dụng thật, chạy trên một cơ sở dữ liệu mới tạo bằng migration, bằng script setup/screenshots/capture.py. Mỗi hình được cắt đúng khối cần xem và chụp ở độ phân giải gấp đôi để chữ đọc rõ khi in."),
  h3("4.3.1. Trang chủ và danh mục sản phẩm"),
  p("Trang chủ có băng chuyển động giới thiệu sản phẩm nổi bật và các banner sản phẩm. Trang danh mục hiển thị dải mẫu máy của danh mục, giống cách trình bày của trang tham khảo rauvang.com."),
  ...figure("4", "01-trang-chu.png", "Trang chủ"),
  ...figure("4", "02-danh-muc.png", "Trang danh mục iPhone"),
  p("Năm sản phẩm chưa có ảnh được phép sử dụng của đúng mẫu máy hiển thị hình vẽ theo loại sản phẩm, ví dụ hình đồng hồ cho Apple Watch, thay vì ảnh của đời máy trước."),
  ...figure("4", "31-anh-cho.png", "Dải mẫu Apple Watch: hai mẫu chưa có ảnh hiện hình vẽ đồng hồ"),
  h3("4.3.2. Tìm kiếm, lọc theo giá và sắp xếp"),
  p("Khách tìm theo tên, lọc theo bốn khoảng giá (dưới 10 triệu, 10 đến 20 triệu, 20 đến 40 triệu, từ 40 triệu) và sắp theo giá hoặc mới nhất. Khoảng giá đang chọn được giữ lại khi đổi cách sắp xếp hoặc tìm kiếm."),
  ...figure("4", "03-tim-kiem-loc-gia.png", "Tìm kiếm kết hợp lọc theo khoảng giá và sắp xếp"),
  h3("4.3.3. Trang cấu hình sản phẩm"),
  p("Mỗi cấu hình có trang riêng với các nút chọn màu và khu vực; giá, mã SKU và tồn kho đổi ngay khi chọn. Khi có khuyến mãi, giá cũ được gạch ngang và tên chương trình hiện bên cạnh."),
  ...figure("4", "04-cau-hinh-san-pham.png", "Trang cấu hình sản phẩm có khuyến mãi"),
  h3("4.3.4. Đăng ký, xác thực OTP và đăng nhập"),
  ...figure("4", "05-dang-ky.png", "Trang đăng ký tài khoản"),
  ...figure("4", "06-nhap-otp.png", "Trang nhập mã OTP gửi qua email"),
  ...figure("4", "07-dang-nhap.png", "Trang đăng nhập"),
  h3("4.3.5. So sánh sản phẩm"),
  p("Khách (kể cả chưa đăng nhập) chọn tối đa 3 sản phẩm thuộc bất kỳ danh mục nào để so sánh giá, tình trạng còn hàng, cấu hình, màu, khu vực và điểm đánh giá."),
  ...figure("4", "08-so-sanh.png", "Trang so sánh sản phẩm", 13),
  h3("4.3.6. Giỏ hàng, thanh toán và đơn hàng"),
  ...figure("4", "09-gio-hang.png", "Giỏ hàng"),
  ...figure("4", "10-thanh-toan.png", "Trang thanh toán đã áp dụng voucher", 12),
  ...figure("4", "11-don-hang.png", "Trang đơn hàng sau khi đặt", 13),
  ...figure("4", "12-don-cua-toi.png", "Danh sách đơn hàng của khách"),
  ...figure("4", "13-tra-cuu-don.png", "Tra cứu đơn hàng bằng mã đơn và số điện thoại"),
  h3("4.3.7. Đánh giá sản phẩm"),
  p("Chỉ khách đã nhận một đơn có sản phẩm đó mới được đánh giá; mỗi khách một đánh giá cho một sản phẩm và có thể sửa lại."),
  ...figure("4", "24-danh-gia.png", "Khối đánh giá trên trang sản phẩm", 12),
  h2("4.4. Giao diện trang quản trị"),
  p("Trang quản trị nằm trong Area Admin. Quản trị viên thấy đủ các mục; nhân viên chỉ thấy Đơn hàng, Báo cáo, Đánh giá, Khuyến mãi, Giá, Kho và Bán hàng."),
  h3("4.4.1. Tổng quan và xử lý đơn hàng"),
  ...figure("4", "14-admin-tong-quan.png", "Trang tổng quan của quản trị viên"),
  ...figure("4", "15-admin-don-hang.png", "Danh sách đơn hàng theo trạng thái"),
  ...figure("4", "23-admin-chi-tiet-don.png", "Chi tiết đơn hàng và các nút xử lý theo trạng thái", 12),
  h3("4.4.2. Nhập kho và tồn kho"),
  p("Nhân viên lập phiếu nhập với nhà cung cấp, sản phẩm, số lượng và giá nhập. Trang phiếu cho thấy tồn trước và tồn sau của từng dòng, đúng công thức tồn cuối = tồn đầu + nhập. Trang tồn kho liệt kê mọi biến thể, tồn thấp nhất lên đầu, tìm được theo SKU hoặc tên."),
  ...figure("4", "25-nhap-kho.png", "Lập phiếu nhập kho", 13),
  ...figure("4", "26-phieu-nhap.png", "Phiếu nhập đã lưu: tồn trước và tồn sau của từng dòng"),
  ...figure("4", "27-ton-kho.png", "Danh sách tồn kho"),
  h3("4.4.3. Bán hàng tại quầy và in hóa đơn hàng loạt"),
  p("Nhân viên chọn sản phẩm, bấm Tính tiền để xem giá đang bán (có khuyến mãi và voucher), rồi bấm Hoàn tất bán. Lần bán trở thành một đơn hàng loại Tại quầy, đã thanh toán và hoàn tất. Từ danh sách đơn, nhân viên đánh dấu nhiều đơn và in tất cả hóa đơn cùng lúc."),
  ...figure("4", "28-ban-tai-quay.png", "Bán hàng tại quầy: bảng tính tiền trước khi hoàn tất", 12),
  ...figure("4", "29-don-tai-quay.png", "Đơn bán tại quầy: kênh Tại quầy và nhân viên bán", 13),
  ...figure("4", "30-in-hoa-don-loat.png", "In nhiều hóa đơn cùng lúc"),
  h3("4.4.4. Sản phẩm, voucher, khuyến mãi và giá"),
  ...figure("4", "16-admin-san-pham.png", "Quản lý sản phẩm"),
  ...figure("4", "17-admin-voucher.png", "Quản lý voucher"),
  ...figure("4", "18-admin-khuyen-mai.png", "Quản lý khuyến mãi"),
  ...figure("4", "19-admin-gia.png", "Đổi giá hàng loạt và lịch sử đổi giá"),
  h3("4.4.5. Báo cáo, đánh giá và tài khoản"),
  ...figure("4", "20-admin-bao-cao.png", "Báo cáo doanh thu, xuất Excel và PDF"),
  ...figure("4", "21-admin-danh-gia.png", "Quản lý đánh giá: trả lời và ẩn"),
  ...figure("4", "22-admin-tai-khoan.png", "Quản lý tài khoản và vai trò"),
  h2("4.5. Kiểm thử và đánh giá"),
  h3("4.5.1. Kiểm thử tự động"),
  p("Bộ test hiện có 636 test xUnit, tất cả đều đạt. Trước mỗi lần gộp mã, cổng kiểm tra gồm: dotnet build --no-incremental (0 cảnh báo, 0 lỗi), dotnet test, dotnet format --verify-no-changes và kịch bản kiểm tra trên trình duyệt của tính năng đó."),
  h3("4.5.2. Kiểm thử trên trình duyệt"),
  ...table("4", "Các kịch bản kiểm tra trên trình duyệt (chạy lại ngày 10/10/2026)", ["Kịch bản", "Nội dung", "Số mục kiểm tra"], [
    ["verify-account", "Đăng ký, OTP, đăng nhập, khóa tài khoản, quên và đổi mật khẩu, hồ sơ, địa chỉ", "69"],
    ["verify-admin", "Khu vực quản trị, phân quyền, tài khoản quản trị đầu tiên", "31"],
    ["verify-cart", "Giỏ hàng", "23"],
    ["verify-checkout", "Đặt hàng và voucher", "25"],
    ["verify-payment", "Thanh toán VNPay, MoMo giả lập", "16"],
    ["verify-orders", "Đơn hàng, tra cứu, xử lý đơn, email", "19"],
    ["verify-admin-catalog", "Quản trị sản phẩm, voucher, vai trò", "15"],
    ["verify-reports", "Báo cáo, file Excel, PDF, hóa đơn", "16"],
    ["verify-reviews", "Đánh giá và trả lời", "9"],
    ["verify-compare", "So sánh sản phẩm", "12"],
    ["verify-catalog-filter", "Lọc theo giá, sắp xếp mới nhất", "16"],
    ["verify-promotions", "Khuyến mãi", "15"],
    ["verify-prices", "Đổi giá hàng loạt và lịch sử giá", "11"],
    ["verify-stock", "Nhập kho, phiếu nhập, tồn kho", "13"],
    ["verify-sales", "Bán hàng tại quầy, in hóa đơn hàng loạt", "11"],
    ["Tổng", "15 kịch bản", "301"],
  ], [2.4, 5, 1.6]),
  h3("4.5.3. Kiểm thử đột biến và rà soát mã"),
  p("Sau mỗi tính năng, các dòng mã quyết định được cố ý làm sai để chắc có test phát hiện, ví dụ bỏ điều kiện tồn kho, bỏ kiểm tra chữ ký thanh toán, cho phép giá về 0. Một số lần có đột biến không bị phát hiện; khi đó hoặc viết thêm test chặt hơn, hoặc nhận ra dòng mã đó là thừa và bỏ đi. Mỗi pull request còn được rà soát bởi một lượt review độc lập, tìm các tình huống chạy song song, ghi dữ liệu chưa kiểm soát và phân quyền."),
  h3("4.5.4. Một số lỗi phát hiện và đã khắc phục"),
  ...table("4", "Một số lỗi phát hiện trong quá trình kiểm thử", ["Lỗi", "Phát hiện bởi", "Cách khắc phục"], [
    ["Các form quản trị viết đường dẫn tay không có mã chống giả mạo (trình duyệt nhận lỗi 400)", "Kiểm tra trên trình duyệt", "Thêm mã; thêm test đi qua mọi trang kiểm tra mọi form"],
    ["Giá nhập dạng \"24.990.000\" bị lưu thành rỗng", "Rà soát toàn bộ ứng dụng", "Kiểm tra ModelState và báo lỗi trên form"],
    ["Mã OTP đăng ký đoán được không giới hạn", "Rà soát toàn bộ ứng dụng", "Hủy lần đăng ký sau 5 lần sai"],
    ["Đơn đã hủy vẫn thanh toán trực tuyến được", "Test thanh toán lại", "Từ chối thanh toán; tiền đến muộn được ghi nhận, đơn vẫn hủy"],
    ["Giỏ hàng in ra dòng mã Razor bên cạnh giá", "Kiểm tra trên trình duyệt", "Sửa cách viết; thêm test không cho mã lọt ra trang"],
    ["\"1.000\" nhập ở ô đổi giá bị hiểu là 1 đồng", "Test giao diện", "Số tiền phải là số nguyên không dấu phân cách"],
    ["Số tiền trên thẻ báo cáo bị tràn khỏi khung", "Ảnh chụp cho báo cáo này", "Sửa CSS; thêm phép đo vào kịch bản kiểm tra"],
    ["Lưu phiếu nhập xong, bấm Quay lại của trình duyệt rồi lưu lần nữa thì hàng được nhập hai lần (tồn 60 thành 70)", "Kịch bản kiểm tra nhập kho", "Đưa mã form vào địa chỉ trang; mã đã dùng thì chuyển tới phiếu đã lưu"],
    ["Trang chi tiết và hóa đơn của đơn khách lẻ (không có tài khoản) bị lỗi", "Test giao diện bán tại quầy", "Đọc email khách theo cách cho phép không có tài khoản"],
    ["Tên biến thể bị lặp (\"iPhone 18 Pro Max iPhone 18 Pro Max 256GB\") trên trang kho và bán hàng", "Ảnh chụp cho báo cáo này", "Một hàm duy nhất VariantText đặt tên biến thể, có test"],
  ], [3.4, 2, 3.4]),
  h3("4.5.5. Quy trình quản lý mã nguồn"),
  p("Mỗi tính năng là một nhánh riêng (ví dụ feat/cart, feat/promotions, feat/price-history) và được gộp vào nhánh dev qua pull request sau khi qua đủ cổng kiểm tra. Từ PR #12 đến PR #35, mỗi pull request ghi rõ quyết định nào của chủ dự án, quyết định nào của người làm, và kết quả kiểm tra. Tiến độ được ghi hằng tuần trong thư mục progress-report/ và nhật ký kiểm tra trong docs/verification.md."),
];

// ---------- chapter 5 ----------

const ch5 = [
  h1("CHƯƠNG 5: KẾT LUẬN VÀ HƯỚNG PHÁT TRIỂN"),
  h2("5.1. Kết luận"),
  h3("5.1.1. Kết quả đạt được"),
  ...bullets([
    "Hoàn thành đủ 37 use case của đặc tả: tài khoản với OTP qua email thật, danh mục với tìm kiếm, lọc theo giá và so sánh, giỏ hàng, đặt hàng với voucher, thanh toán COD và VNPay, MoMo giả lập, xử lý đơn hàng với email thông báo, nhập kho và kiểm tra tồn kho, bán hàng tại quầy, quản trị sản phẩm, voucher, khuyến mãi, giá và lịch sử giá, tài khoản và vai trò, đánh giá, báo cáo doanh thu xuất Excel và PDF, in hóa đơn (kể cả in nhiều hóa đơn cùng lúc).",
    "Sử dụng đúng các cơ chế của ASP.NET Core: MVC và Razor, Tag Helper, View Component, Area, Dependency Injection, Options pattern, model validation, anti-forgery, EF Core Code First với 23 migration, ASP.NET Core Identity trên bảng Users của đặc tả.",
    "Dữ liệu đúng khi nhiều người thao tác cùng lúc nhờ giao dịch và cập nhật có điều kiện; các tình huống này đều có test.",
    "636 test tự động và 15 kịch bản kiểm tra trên trình duyệt (301 mục), tất cả đều đạt.",
  ]),
  h3("5.1.2. Ý nghĩa của đề tài"),
  p("Đề tài giúp em hiểu cách một ứng dụng ASP.NET Core được tổ chức từ cơ sở dữ liệu tới giao diện, và cách dùng các cơ chế có sẵn thay vì tự viết lại. Việc viết test trước và kiểm tra trên trình duyệt thật cho thấy nhiều lỗi chỉ lộ ra khi chạy ứng dụng thật hoặc khi hai yêu cầu đến cùng lúc."),
  h3("5.1.3. Hạn chế"),
  ...bullets([
    "VNPay và MoMo mới được giả lập trong ứng dụng, chưa kết nối sandbox thật.",
    "Trang tra cứu đơn chưa giới hạn số lần thử vì chưa chọn con số; các form nhập mã OTP đã giới hạn 5 lần sai.",
    "Ô mức giảm của voucher đọc \"1.000\" thành 1; mã SKU chưa có ràng buộc duy nhất.",
    "Đổi giá hàng loạt xong, bấm Quay lại của trình duyệt rồi bấm lại thì thay đổi được áp dụng thêm một lần (lịch sử giá có ghi lại); chưa chốt có cần chặn như phiếu nhập hay không.",
    "Một số trang làm từ đầu (địa chỉ, hồ sơ, mở trang thanh toán) khi cơ sở dữ liệu lỗi thì hiện trang lỗi chung thay vì thông báo riêng.",
    "Đơn trực tuyến chưa thanh toán vẫn giữ tồn kho cho tới khi bị hủy.",
    "Bảng thuộc tính sản phẩm còn trống nên trang so sánh dùng giá, cấu hình, màu, khu vực và đánh giá; chức năng yêu thích (bảng Favorites) chưa làm.",
    "Ứng dụng chưa được triển khai lên máy chủ; cơ sở dữ liệu hiện là SQLite, chưa chạy thử trên SQL Server.",
  ]),
  h2("5.2. Hướng phát triển"),
  ...bullets([
    "Kết nối VNPay và MoMo sandbox thật khi có tài khoản.",
    "Bật bộ giới hạn số lần thử có sẵn của ASP.NET Core cho trang tra cứu đơn.",
    "Thêm báo cáo nhập xuất theo kỳ (BM_REPORT_INOUT_01) từ dữ liệu phiếu nhập và đơn bán đã có.",
    "Chuyển sang SQL Server và triển khai bằng Docker hoặc dịch vụ đám mây, có HTTPS.",
    "Thêm thuộc tính sản phẩm theo danh mục để so sánh chi tiết hơn, chức năng yêu thích và thông báo qua SMS.",
    "Tự động hủy đơn trực tuyến quá hạn thanh toán để trả lại tồn kho.",
  ]),
];

// ---------- references and evaluation pages ----------

const refs = [
  h1("TÀI LIỆU THAM KHẢO"),
  ...steps([
    "Khoa Công nghệ Thông tin, Trường Đại học Trà Vinh (2025), Đồ án môn học Công nghệ phần mềm: Xây dựng website Apple Store (đặc tả yêu cầu, bản tóm tắt trong docs/requirements.md của kho mã nguồn).",
    "Microsoft, ASP.NET Core documentation, https://learn.microsoft.com/aspnet/core/",
    "Microsoft, Entity Framework Core documentation, https://learn.microsoft.com/ef/core/",
    "Microsoft, .NET documentation, https://learn.microsoft.com/dotnet/",
    "xUnit.net, https://xunit.net/",
    "Microsoft Playwright, https://playwright.dev/",
    "ClosedXML, https://github.com/ClosedXML/ClosedXML",
    "MailKit, https://github.com/jstedfast/MailKit",
    "VNPay, Tài liệu tích hợp cổng thanh toán, https://sandbox.vnpayment.vn/apis/",
    "Bootstrap, https://getbootstrap.com/",
  ], "refs"),
];

const lines = n => Array.from({ length: n }, () => new Paragraph({ spacing: { after: 0, line: 480 },
  border: { bottom: { style: BorderStyle.DOTTED, size: 6, color: "808080", space: 1 } }, children: [] }));

const evaluation = [
  new Paragraph({ style: "FrontTitle", pageBreakBefore: true, children: [new TextRun("NHẬN XÉT CỦA GIẢNG VIÊN HƯỚNG DẪN")] }),
  ...lines(20),
  ...blank(1),
  new Paragraph({ alignment: AlignmentType.RIGHT, children: [new TextRun({ text: "Trà Vinh, ngày ...... tháng ...... năm 2026", italics: true })] }),
  new Paragraph({ indent: { left: 5103 }, alignment: AlignmentType.CENTER, children: [new TextRun({ text: "Giảng viên hướng dẫn", bold: true })] }),
  new Paragraph({ indent: { left: 5103 }, alignment: AlignmentType.CENTER, children: [new TextRun({ text: "(Ký và ghi rõ họ tên)", italics: true })] }),
  new Paragraph({ style: "FrontTitle", pageBreakBefore: true, children: [new TextRun("NHẬN XÉT CỦA THÀNH VIÊN HỘI ĐỒNG")] }),
  ...lines(20),
  ...blank(1),
  new Paragraph({ alignment: AlignmentType.RIGHT, children: [new TextRun({ text: "Trà Vinh, ngày ...... tháng ...... năm 2026", italics: true })] }),
  new Paragraph({ indent: { left: 5103 }, alignment: AlignmentType.CENTER, children: [new TextRun({ text: "Thành viên hội đồng", bold: true })] }),
  new Paragraph({ indent: { left: 5103 }, alignment: AlignmentType.CENTER, children: [new TextRun({ text: "(Ký và ghi rõ họ tên)", italics: true })] }),
];

// ---------- document ----------

const pageProps = { page: { size: { width: 11906, height: 16838 }, margin: { top: 1134, bottom: 1134, left: 1701, right: 1134 } } };
const footer = format => new Footer({ children: [new Paragraph({ alignment: AlignmentType.CENTER, children: [new TextRun({ children: [PageNumber.CURRENT] })] })] });

const stepConfigs = Array.from({ length: stepLists }, (_, i) => ({
  reference: `steps${i + 1}`,
  levels: [{ level: 0, format: LevelFormat.DECIMAL, text: "%1.", alignment: AlignmentType.LEFT, style: { paragraph: { indent: { left: 720, hanging: 360 } } } }],
}));

const doc = new Document({
  creator: "Lê Bình",
  title: "Xây dựng website bán sản phẩm công nghệ Apple (Apple Store)",
  description: "Báo cáo đồ án môn Chuyên đề ASP.NET",
  features: { updateFields: true },
  styles: {
    default: { document: { run: { font: FONT, size: SIZE }, paragraph: { spacing: { line: 336 } } } },
    paragraphStyles: [
      { id: "Heading1", name: "Heading 1", basedOn: "Normal", next: "Normal", quickFormat: true,
        run: { font: FONT, size: 30, bold: true, color: "000000" }, paragraph: { alignment: AlignmentType.CENTER, spacing: { before: 0, after: 360 }, outlineLevel: 0 } },
      { id: "Heading2", name: "Heading 2", basedOn: "Normal", next: "Normal", quickFormat: true,
        run: { font: FONT, size: 28, bold: true, color: "000000" }, paragraph: { spacing: { before: 240, after: 120 }, outlineLevel: 1, keepNext: true } },
      { id: "Heading3", name: "Heading 3", basedOn: "Normal", next: "Normal", quickFormat: true,
        run: { font: FONT, size: 26, bold: true, italics: true, color: "000000" }, paragraph: { spacing: { before: 180, after: 100 }, outlineLevel: 2, keepNext: true } },
      { id: "FrontTitle", name: "Front Title", basedOn: "Normal", next: "Normal",
        run: { font: FONT, size: 30, bold: true }, paragraph: { alignment: AlignmentType.CENTER, spacing: { after: 360 } } },
      { id: "HinhCaption", name: "HinhCaption", basedOn: "Normal", next: "Normal",
        run: { font: FONT, size: 24, italics: true }, paragraph: { alignment: AlignmentType.CENTER, spacing: { after: 240 } } },
      { id: "BangCaption", name: "BangCaption", basedOn: "Normal", next: "Normal",
        run: { font: FONT, size: 24, italics: true, bold: true }, paragraph: { alignment: AlignmentType.CENTER, spacing: { before: 120, after: 80 }, keepNext: true } },
    ],
  },
  numbering: {
    config: [
      { reference: "dot", levels: [{ level: 0, format: LevelFormat.BULLET, text: "•", alignment: AlignmentType.LEFT, style: { paragraph: { indent: { left: 720, hanging: 360 } } } }] },
      { reference: "refs", levels: [{ level: 0, format: LevelFormat.DECIMAL, text: "[%1]", alignment: AlignmentType.LEFT, style: { paragraph: { indent: { left: 720, hanging: 720 } } } }] },
      ...stepConfigs,
    ],
  },
  sections: [
    { properties: { ...pageProps }, children: cover },
    { properties: { ...pageProps, type: SectionType.NEXT_PAGE, page: { ...pageProps.page, pageNumbers: { start: 1, formatType: NumberFormat.LOWER_ROMAN } } },
      footers: { default: footer() }, children: front },
    { properties: { ...pageProps, type: SectionType.NEXT_PAGE, page: { ...pageProps.page, pageNumbers: { start: 1, formatType: NumberFormat.DECIMAL } } },
      footers: { default: footer() }, children: [...ch1, ...ch2, ...ch3, ...ch4, ...ch5, ...refs, ...evaluation] },
  ],
});

Packer.toBuffer(doc).then(buf => {
  fs.writeFileSync(OUT, buf);
  console.log(`wrote ${OUT} (${buf.length} bytes), figures ${figCount}, tables ch2 ${tableNo["2"]} ch3 ${tableNo["3"]} ch4 ${tableNo["4"]}`);
});
