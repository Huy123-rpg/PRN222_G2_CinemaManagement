# Module đặt vé online — kiến trúc & cách đọc code

Project áp dụng kiến trúc 3 tầng chuẩn thực tế (**Controller → Service → Repository**) với **Interfaces** và **Implementations** tách riêng tại mỗi tầng (`Services/Interfaces` vs `Services/Implementations`, `Repositories/Interfaces` vs `Repositories/Implementations`).

## Trách nhiệm từng file

| File | Công việc chính |
|---|---|
| `Controllers/MoviesController.cs` | Nhận yêu cầu xem, tìm kiếm/lọc danh sách phim, chi tiết phim và sơ đồ ghế; trả View hoặc JSON. |
| `Controllers/BookingController.cs` | Đọc Session, kiểm tra dữ liệu form, điều hướng Chọn bắp & nước → Thanh toán, gọi `IOnlineBookingService` và `IVnPayService`. |
| `Services/Interfaces/IOnlineBookingService.cs` | Khai báo điểm vào chung mà controller và worker sử dụng. |
| `Services/Implementations/OnlineBookingService.cs` | Facade chuyển lời gọi đến đúng service nghiệp vụ, không tự xử lý transaction. |
| `Services/Implementations/MovieCatalogService.cs` | Đọc, tìm kiếm, lọc phim đang chiếu, suất chiếu sắp tới và trạng thái ghế (chỉ đọc dữ liệu). |
| `Services/Implementations/SeatBookingService.cs` | Giữ hoặc thay đổi lựa chọn ghế (bảo toàn tiền bắp nước đã chọn), hủy đơn chưa thanh toán, giải phóng ghế của đơn hết hạn. |
| `Services/Implementations/BookingConcessionService.cs` | Hiển thị menu bắp/nước/combo, kiểm tra tồn kho và lưu `BookingConcessions` + cập nhật tổng `Booking.TotalAmount`. |
| `Services/Implementations/BookingPaymentService.cs` | Xử lý thanh toán giả lập và xác nhận VNPay; đối chiếu tổng tiền vé + bắp nước, trừ tồn kho nguyên tử, cập nhật đơn, ghế, Payment và Ticket trong transaction. |
| `Services/Implementations/BookingTicketService.cs` | Đọc đơn/vé/bắp nước của khách hàng và tạo ảnh QR vé phim + QR nhận bắp nước qua `IQrCodeService`. |
| `Services/Implementations/BookingSupportService.cs` | Những bước dùng chung: kiểm tra khách hàng, kiểm tra suất chiếu, khóa dòng đơn, nhả ghế và gọi `IBookingNotificationService`. |
| `Services/Implementations/VnPayService.cs` | Tạo URL thanh toán VNPay và xác minh chữ ký callback (`IVnPayService`). |
| `Services/Implementations/QrCodeService.cs` | Sinh ảnh QR PNG bằng `QRCoder` (`IQrCodeService`). |
| `Services/Implementations/BookingNotificationService.cs` | Gửi thông báo SignalR sau khi commit (`IBookingNotificationService`). |
| `Repositories/Interfaces/*` & `Repositories/Implementations/*` | Tầng truy vấn dữ liệu EF Core (`Movie`, `Showtime`, `Booking`, `Concession`, `BookingConcession`, `Ticket`, `Payment`, ...), cập nhật nguyên tử và `IUnitOfWork`. |
| `BackgroundServices/SeatHoldExpirationService.cs` | Worker chạy định kỳ, tạo scope mới rồi gọi `IOnlineBookingService.ReleaseExpiredAsync`. |
| `Hubs/SeatHub.cs` | Gom các kết nối đang xem cùng suất chiếu vào một nhóm để nhận thông báo (gọi qua `IMovieCatalogService`). |
| `Extensions/DependencyInjectionExtensions.cs` | Đăng ký tập trung `AddRepositories()`, `AddOnlineBookingServices()` và `AddApplicationServices()`. |

## Luồng gọi chuẩn

```text
1. Chọn ghế (POST /Booking/SetSeats)
    → SeatBookingService + IUnitOfWork → IBookingRepository / IShowtimeSeatRepository
    → commit xong mới gọi IBookingNotificationService (SignalR)

2. Chọn bắp & nước (GET/POST /Booking/Concessions/{id})
    → BookingConcessionService + IUnitOfWork
    → IConcessionRepository / IBookingConcessionRepository
    → Lưu BookingConcessions & cập nhật Booking.TotalAmount = Tiền vé + Tiền bắp nước

3. Xác nhận & thanh toán (GET /Booking/Checkout/{id} → POST /Booking/Pay/{id} → VNPay)
    → Gộp xác nhận thông tin và thanh toán trên cùng 1 màn hình; BookingPaymentService xác thực tổng tiền vé + bắp nước, trừ tồn kho nguyên tử, xuất vé QR (`/Booking/TicketQr` & `/Booking/ConcessionQr`) và Payment trong 1 transaction
    → Khi VNPay thất bại, người dùng hủy (`vnp_ResponseCode = "24"`), hoặc callback lỗi: ghi nhận `PaymentStatus = Failed`, giữ đơn `Pending` + ghế `Held` trong thời gian giữ ghế và điều hướng sang `GET /Booking/PaymentFailed/{id}` để thử thanh toán lại hoặc chọn lại ghế
```

## Tiến độ và kiểm chứng

- [x] Tách toàn bộ module đặt vé online thành Controller → Service → Repository với thư mục `Interfaces/` và `Implementations/` riêng biệt.
- [x] Tách `BookingController`, `MoviesController`, `SeatHub` và toàn bộ các Service đặt vé khỏi phụ thuộc trực tiếp vào `CinemaDbContext` (giữ nguyên toàn bộ code của các thành viên khác).
- [x] Bổ sung bước chọn bắp, đồ uống & combo sau khi chọn ghế và cộng tổng tiền vào quy trình Checkout / Thanh toán.
- [x] Build và chạy `OnlineBooking.Tests` để kiểm chứng ranh giới kiến trúc, DI, tìm kiếm/lọc phim, giữ ghế, chọn bắp/nước, VNPay và rollback SQL Server.
