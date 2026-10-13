# Kịch bản thuyết trình (18 slide)

Lấy nguyên văn phần ghi chú (`<aside>`) của từng slide trong `thesis/abs/slides-src/slides/`,
xếp theo đúng thứ tự trình chiếu (`deck.json`). Khoảng 1.190 từ, đọc bình thường mất
khoảng 7 đến 8 phút. Dùng để tập nói trước, không phải để đọc nguyên văn trên sân khấu.

1. **Bìa**

   Chào thầy và các bạn. Em là Lê Bình, lớp VX24TTK7. Đề tài của em là xây dựng website
   bán sản phẩm công nghệ Apple bằng ASP.NET Core MVC, dưới sự hướng dẫn của thầy
   Nguyễn Nhứt Lam.

2. **Mục tiêu đề tài**

   Đề tài có ba mục tiêu. Một là một cửa hàng trực tuyến hoàn chỉnh cho khách. Hai là
   trang quản trị cho admin và nhân viên. Ba, và quan trọng với môn học, là vận dụng
   đúng các cơ chế có sẵn của ASP.NET Core thay vì tự viết lại.

3. **Công nghệ sử dụng**

   Nền tảng là .NET 10 với ASP.NET Core MVC. Dữ liệu dùng EF Core theo kiểu Code-First,
   khi phát triển chạy SQLite nhưng kiểu dữ liệu đặt theo SQL Server để sau này chuyển
   sang dễ dàng. Tài khoản dùng ASP.NET Core Identity. Kiểm thử có hai lớp: xUnit chạy
   app trong bộ nhớ, và Playwright điều khiển trình duyệt thật.

4. **Ba tầng, phụ thuộc một chiều**

   Mã nguồn chia ba dự án. Web chỉ lo giao diện và điều hướng. Infrastructure giữ dữ
   liệu và các service nghiệp vụ. Domain chỉ có entity, không phụ thuộc gì. Phụ thuộc
   đi một chiều từ Web xuống Domain. Mỗi quy tắc nghiệp vụ có đúng một chỗ giữ, để các
   trang không thể lệch nhau.

5. **Đủ 37 trên 37 use case**

   Đặc tả có 37 use case chia cho bốn tác nhân. Em đã làm xong cả 37. Hai use case cuối
   là nhập kho và bán tại quầy của nhân viên, em sẽ trình bày ở phần kết quả. Số trên
   mỗi thẻ là số use case của nhóm đó.

6. **Cơ sở dữ liệu**

   Cơ sở dữ liệu giữ đúng 24 bảng của đặc tả và thêm năm phần có lý do rõ ràng: bảng
   voucher vì đặc tả mô tả quy tắc voucher mà không có chỗ lưu, cột Kind để khuyến mãi
   dùng chung bảng voucher, bảng lịch sử đổi giá theo BM_PRICE_01, phiếu nhập kho, và
   bảng khóa tra cứu đơn sau 5 lần nhập sai số điện thoại. Đơn bán tại quầy dùng chung
   bảng Orders, chỉ thêm cột kênh bán, người bán và khóa form để không lưu trùng. Mọi
   thay đổi đều đi qua EF Core migration.

7. **Xem và chọn sản phẩm**

   Đây là trang cấu hình sản phẩm. Khách chọn màu và vùng, giá đổi ngay. Khi có khuyến
   mãi, giá cũ được gạch ngang và hiện tên chương trình. Khách cũng có thể so sánh tối
   đa ba sản phẩm mà không cần đăng nhập.

8. **Đặt hàng an toàn**

   Bên trái là ô voucher và ba cách thanh toán, bên phải là tổng đơn sau khi áp voucher
   WELCOME10. Khi đặt hàng, ba việc trừ kho, dùng voucher và xóa giỏ chạy trong một
   transaction, nên hoặc thành công hết hoặc không có gì thay đổi. Nếu giá hay khuyến
   mãi đổi trong lúc khách đang mở trang, hệ thống hiện tổng mới để khách xác nhận lại.
   VNPay và MoMo đi qua một cổng giả lập ký số theo cách của VNPay vì không có tài
   khoản sandbox.

9. **Xử lý đơn hàng**

   Đây là trang chi tiết đơn của nhân viên. Đơn này đang giao với mã vận đơn GHN, nên
   nút tiếp theo là đánh dấu đã giao. Các bước chuyển trạng thái nằm trong một bảng quy
   tắc duy nhất. Nếu hai nhân viên cùng bấm một lúc thì chỉ một lần có hiệu lực.

10. **Báo cáo doanh thu**

    Báo cáo doanh thu chỉ tính đơn đã thanh toán và không bị hủy, gom theo ngày giờ
    Việt Nam. Dòng bên dưới các thẻ cho biết tổng trước giảm giá, số tiền giảm, và các
    đơn chưa thanh toán không được tính. Có xuất Excel bằng ClosedXML và in PDF qua
    trang in của trình duyệt.

11. **Khuyến mãi theo thời gian**

    Khuyến mãi tự áp dụng theo thời gian, không cần tiến trình chạy nền: giá đang bán
    được tính tại lúc đọc, ở một chỗ duy nhất là SalePrices, nên danh sách, trang sản
    phẩm, giỏ hàng và đơn hàng luôn khớp nhau. Phía trên là chương trình giảm 5 phần
    trăm, phía dưới là giá trên trang sản phẩm khi chương trình đang chạy. Hai chương
    trình chồng lên nhau thì lấy mức giảm lớn nhất, voucher tính tiếp trên giá đã giảm.

12. **Đổi giá hàng loạt**

    Đổi giá hàng loạt theo BM_PRICE_01. Ở đây Mac Mini vừa tăng 2 phần trăm, mỗi giá
    được làm tròn đến nghìn đồng và ghi một dòng lịch sử. Nếu có một giá về 0, hoặc có
    người sửa cùng lúc, thì không đổi gì cả. Lúc kiểm tra trên dữ liệu thật em gặp lỗi:
    các biến thể cũ đã ngừng bán có giá vài trăm đồng, làm tròn về 0 và chặn cả danh
    mục Watch. Cách sửa là chỉ đổi biến thể đang bán, có test giữ lại.

13. **Nhập kho có phiếu**

    Use case 20, nhập kho. Mỗi phiếu có nhà cung cấp và nhiều dòng, mỗi dòng ghi số
    lượng, giá nhập, và tồn kho trước và sau. Khi kiểm tra trên trình duyệt em phát
    hiện nếu bấm Back rồi lưu lại thì kho bị cộng hai lần. Em sửa bằng một khóa riêng
    cho mỗi form, lần lưu thứ hai sẽ mở lại phiếu đã lưu thay vì tạo phiếu mới.

14. **Bán tại quầy**

    Use case 21, bán tại quầy. Nhân viên chọn sản phẩm, bấm báo giá để xem giá đang
    bán, rồi hoàn tất. Đơn được ghi vào bảng Orders với kênh Tại quầy, nên báo cáo
    doanh thu tự tính luôn. Nếu khách có tài khoản, nhập email để đơn hiện trong lịch
    sử của khách. Nếu giá đổi sau khi đã báo giá, hệ thống báo lại thay vì tính tiền
    theo giá mới mà nhân viên chưa thấy.

15. **Cơ chế có sẵn được dùng**

    Môn học chấm phần ASP.NET, nên em ưu tiên cơ chế có sẵn. Identity lo đăng nhập và
    vai trò. Areas và Authorize chia khu quản trị theo vai trò. EF Core migrations tạo
    cơ sở dữ liệu. Mọi form có token chống giả mạo. Cấu hình được kiểm tra ngay khi
    ứng dụng khởi động.

16. **Kiểm thử trước, kiểm tra trên trình duyệt thật**

    Mỗi chức năng có logic đều viết test fail trước. Ngoài 647 test tự động, 16 script
    Playwright chạy app thật trên trình duyệt; riêng một script kiểm tra mọi trang ở
    năm cỡ màn hình, từ điện thoại nhỏ đến máy tính. Em còn cố tình làm hỏng code để
    chắc chắn test bắt được lỗi. Các lỗi tìm ra trong quá trình này đều được ghi lại và
    sửa có test kèm theo. Có 28 pull request đã merge, từ PR 12 đến PR 40.

17. **Hạn chế và hướng phát triển**

    Em nói rõ các hạn chế: thanh toán online mới là giả lập, chưa giới hạn số lần xin
    gửi mã OTP, và mới chạy trên SQLite. Đoán mã OTP thì đã khóa sau 5 lần sai, và tra
    cứu đơn cũng khóa 5 phút sau 5 lần nhập sai số điện thoại. Hướng phát triển đi
    thẳng vào các điểm đó, dùng tiếp các cơ chế có sẵn như rate limiting của ASP.NET
    Core.

18. **Cảm ơn**

    Em cảm ơn thầy và các bạn đã lắng nghe. Em xin nhận câu hỏi. Nếu được, em có thể
    demo trực tiếp: đặt một đơn hàng, áp voucher, rồi vào trang quản trị xác nhận và
    xem báo cáo.

## Câu hỏi có thể gặp, chuẩn bị trước

- **Vì sao SQLite chứ không phải SQL Server?** Máy phát triển là macOS, không có SQL
  Server chạy trực tiếp; SQLite không cần cài máy chủ. Kiểu dữ liệu (`nvarchar`,
  `decimal(12,2)`, `datetime2`) vẫn khai báo theo SQL Server trong EF Core, nên chuyển
  sang chỉ cần đổi nhà cung cấp và chuỗi kết nối.
- **VNPay, MoMo giả lập là giả lập như thế nào?** Một cổng trong chính ứng dụng
  (`/PaymentSimulator`), ký kết quả trả về bằng HMAC-SHA512 giống cách VNPay ký thật, để
  luồng thanh toán và kiểm tra chữ ký vẫn đúng như thật, không có tài khoản sandbox nên
  không gọi ra ngoài.
- **Vì sao không dùng bảng `AspNetUsers` mặc định của Identity?** Đặc tả đã có sẵn bảng
  `Users` với 24 bảng liên quan; viết một `UserStore` riêng để Identity đọc/ghi vào đúng
  bảng đó, giữ nguyên lược đồ báo cáo thay vì tạo bảng song song.
- **Hai người đặt hàng cùng lúc thì sao?** Mỗi thao tác đổi kho, dùng voucher, chuyển
  trạng thái đơn đều là một phép cập nhật có điều kiện (so sánh rồi mới ghi) trong một
  transaction; có test riêng dựng tình huống chạy song song.
- **Vì sao 28 PR mà có PR đến #40?** Số PR là 28 đã merge; các số thứ tự không merge
  (ví dụ PR đang chờ duyệt, hoặc đổi tên nhánh) không tính, nên dải số request chạy đến
  40 nhưng tổng merge là 28.
