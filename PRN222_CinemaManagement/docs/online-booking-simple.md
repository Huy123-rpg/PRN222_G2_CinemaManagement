# Module đặt vé online — cách đọc code

Module được chia thành các lớp theo công việc: xem phim, giữ ghế, thanh toán và xem vé. Muốn sửa nghiệp vụ nào, bắt đầu từ lớp phụ trách nghiệp vụ đó. Cách chia này giữ các bước xử lý trong một phương thức đủ rõ để đọc và học.

## Trách nhiệm từng file

| File | Công việc chính |
|---|---|
| `Controllers/MoviesController.cs` | Nhận yêu cầu xem danh sách phim, chi tiết phim và sơ đồ ghế; trả View hoặc JSON. |
| `Controllers/BookingController.cs` | Đọc Session, kiểm tra dữ liệu form, gọi nghiệp vụ đặt vé; tạo yêu cầu VNPay và tiếp nhận kết quả trả về. |
| `Services/IOnlineBookingService.cs` | Khai báo các phương thức mà controller và worker sử dụng. |
| `Services/OnlineBookingService.cs` | Đầu mối gọi chung, còn gọi là facade: chuyển lời gọi đến đúng lớp bên dưới, không tự xử lý transaction. |
| `Services/OnlineBooking/MovieCatalogService.cs` | Đọc phim đang chiếu, suất chiếu sắp tới và trạng thái ghế. Các phương thức ở đây chỉ đọc dữ liệu. |
| `Services/OnlineBooking/SeatBookingService.cs` | Giữ hoặc thay đổi lựa chọn ghế, hủy đơn chưa thanh toán, giải phóng ghế của đơn hết hạn. |
| `Services/OnlineBooking/BookingPaymentService.cs` | Xử lý thanh toán giả lập và xác nhận kết quả VNPay; cập nhật đơn, ghế, Payment và Ticket trong transaction. |
| `Services/OnlineBooking/BookingTicketService.cs` | Đọc đơn/vé của khách hàng và tạo ảnh QR bằng QRCoder. Việc tạo bản ghi Ticket khi thanh toán nằm ở `BookingPaymentService`. |
| `Services/OnlineBooking/BookingSupportService.cs` | Những bước dùng chung: kiểm tra khách hàng, kiểm tra suất chiếu, khóa dòng đơn, nhả ghế, gửi thông báo SignalR và gắn múi giờ UTC. |
| `Services/VNPayService.cs` | Lớp `VnPayService` tạo URL thanh toán và tính chữ ký HMAC; không xác nhận đơn hay phát vé. |
| `Models/ViewModels/OnlineBookingViewModels.cs` | Các class chứa dữ liệu cho giao diện và dữ liệu nhận từ form. |
| `wwwroot/js/booking.js` | Gửi lựa chọn ghế, đọc lại sơ đồ, nhận cập nhật SignalR và hiển thị thời gian giữ còn lại. |
| `Hubs/SeatHub.cs` | Gom các kết nối đang xem cùng suất chiếu vào một nhóm để nhận thông báo. |
| `Services/SeatHoldExpirationService.cs` | Worker chạy định kỳ, tạo scope rồi gọi nghiệp vụ giải phóng ghế hết hạn. |

## Luồng gọi dễ theo dõi

Ví dụ khi khách chọn ghế:

```text
booking.js gửi POST /Booking/SetSeats
    → BookingController đọc UserId từ Session
    → OnlineBookingService.SetSeatsAsync
    → SeatBookingService.SetSeatsAsync
    → CinemaDbContext truy vấn/cập nhật SQL Server
    → commit xong mới thông báo SignalR
```

Để đọc các luồng còn lại, đi theo cùng thứ tự `Controller → OnlineBookingService → lớp nghiệp vụ → CinemaDbContext`:

- Xem phim hoặc sơ đồ ghế: `MovieCatalogService`.
- Giữ ghế, hủy đơn hoặc nhả ghế hết hạn: `SeatBookingService`.
- Thanh toán và ghi nhận vé được mua: `BookingPaymentService`.
- Xem đơn đã có, danh sách vé hoặc tải ảnh QR: `BookingTicketService`.

Các lớp nghiệp vụ gọi `BookingSupportService` khi cần một bước dùng chung. Lớp hỗ trợ không tự mở, commit hoặc rollback transaction.

## Phân biệt hai phần thanh toán

`VnPayService.CreatePaymentUrl` chuẩn bị địa chỉ để chuyển khách sang VNPay. Khi VNPay trả kết quả, `BookingController.VnPayReturn` kiểm tra chữ ký rồi gọi `OnlineBookingService.ConfirmPaymentAsync`.

Lời gọi xác nhận được chuyển đến `BookingPaymentService.ConfirmPaymentAsync`. Tại đây mới kiểm tra đơn và ghế, cập nhật kết quả thanh toán, chuyển ghế sang `Sold` và tạo Ticket khi thành công. Vì vậy, tạo URL thanh toán chưa có nghĩa là đơn đã được thanh toán.

`BookingPaymentService.PayAsync` vẫn chứa nghiệp vụ thanh toán giả lập đã có. Hai phương thức nằm cùng lớp vì đều xử lý kết quả thanh toán; việc tách file lần này giữ nguyên các đầu vào và luồng gọi hiện có.

## Scoped và transaction

Trong `Program.cs`, các service của module được đăng ký bằng `AddScoped`. Trong một request, các lớp nhận cùng một `CinemaDbContext` do DI cung cấp. Worker cũng tạo scope riêng cho mỗi lượt xử lý.

Transaction vẫn nằm ngay trong nghiệp vụ cần thay đổi dữ liệu: `SeatBookingService` và `BookingPaymentService`. Chẳng hạn, xác nhận thanh toán phải cập nhật đơn, ghế, Payment và Ticket cùng thành công; nếu một bước lỗi thì rollback toàn bộ.

Khi `BookingSupportService` khóa đơn hoặc nhả ghế trong transaction của lớp gọi, các câu SQL dùng cùng DbContext nên tham gia transaction đó. Không mở transaction riêng trong lớp hỗ trợ. Thông báo SignalR được gửi sau khi commit, để client đọc được trạng thái đã lưu.

## Những phần cần giữ dù mới học

- `async/await`: chờ truy vấn database mà không chặn luồng xử lý web.
- `Include/ThenInclude`: tải dữ liệu liên quan như phim, phòng và ghế của đơn.
- `AsNoTracking`: dữ liệu chỉ đọc, không cần EF theo dõi để ghi lại.
- `BeginTransactionAsync`, `CommitAsync`, `RollbackAsync`: nhiều thay đổi cùng thành công hoặc cùng hoàn tác.
- `ExecuteUpdateAsync`: kiểm tra điều kiện và cập nhật ghế trong cùng câu SQL. Kiểm tra bằng `if` rồi `SaveChanges` riêng không ngăn được hai người lấy cùng ghế.
- Khóa dòng đơn trước khi thay đổi ghế: các thao tác trên cùng đơn phải chờ nhau, tránh thanh toán và hủy chạy chồng lên nhau.
- `CancellationToken`: cho phép dừng truy vấn khi request bị hủy hoặc ứng dụng đang tắt.
- `IOnlineBookingService`: giữ đầu mối mà controller/worker đã dùng. Các lớp nghiệp vụ mới là class thông thường, không cần thêm một interface cho từng lớp.

## Quy tắc nghiệp vụ được giữ nguyên

- Ghế: `Available` → `Held` → `Sold`; hủy/hết hạn thì `Held` → `Available`.
- Đơn: `Pending` → `Confirmed`, `Cancelled` hoặc `Expired`.
- Hạn giữ là 5 phút do server quyết định; tải lại trang hoặc thử lại không gia hạn.
- Giá dùng `ShowtimeSeat.Price` từ server. Phim mở bán là `Showing` theo schema hiện có.
- Người dùng chỉ xem và thao tác đơn/vé của mình theo các kiểm tra hiện có.
- Database lưu UTC; giao diện hiển thị giờ Việt Nam.
- Lần tách này không sửa auth, phân quyền dùng chung, entity, schema, URL hay tên trường JSON/form.

## Tiến độ và kiểm chứng

- [x] Tách code thành bốn lớp nghiệp vụ và một lớp hỗ trợ dùng chung trong `Services/OnlineBooking`.
- [x] Giữ `OnlineBookingService` làm đầu mối cho controller và worker.
- [x] Đăng ký các lớp mới bằng Scoped; giữ transaction trong lớp xử lý nghiệp vụ.
- [x] Cập nhật tài liệu trách nhiệm từng file và luồng VNPay.
- [ ] Ghi nhận kết quả build và kiểm thử hồi quy sau khi tách.
- [x] Review độc lập: 21 phương thức được chuyển giữ nguyên phần xử lý, chỉ đổi tên gọi lớp hỗ trợ; 11 API của facade và cách dùng Scoped/transaction được bảo toàn.

Các kiểm thử cần đối chiếu gồm tranh ghế, rollback, hạn giữ cố định, thanh toán lặp/thất bại/hủy, giải phóng hết hạn và quyền sở hữu. Chưa đánh dấu hoàn tất kiểm chứng khi chưa có kết quả chạy và review.

