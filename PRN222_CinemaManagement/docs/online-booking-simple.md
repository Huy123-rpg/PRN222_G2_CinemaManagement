# Module đặt vé online — cách đọc code

## Kế hoạch đơn giản hóa đã rà soát

- Chỉ sửa module đặt vé của Bách và các dòng đăng ký dịch vụ của module trong `Program.cs`.
- Giữ nguyên auth, phân quyền dùng chung, entity, cấu hình database và schema của nhóm.
- Dùng ViewModel dạng class, khởi tạo bằng tên thuộc tính, if/else và foreach như module auth.
- Controller nhận request, đọc Session, kiểm tra ModelState rồi gọi một service nghiệp vụ.
- Bỏ lớp trung gian chỉ chuyển tiếp lời gọi: catalog, seat manager, gateway giả lập, QR wrapper, notifier/email wrapper.
- Service gọi EF Core, SignalR và QRCoder trực tiếp; transaction viết ngay trong từng nghiệp vụ, không dùng hàm nhận delegate hoặc expression predicate.
- Không thay đổi URL, tên trường JSON/form, thời hạn giữ 5 phút và các trạng thái trong database.
- Kiểm thử SQL Server: tranh ghế, rollback, giữ nguyên hạn, thanh toán lặp/thất bại/hủy, nhả ghế hết hạn và quyền sở hữu.

## Đọc theo thứ tự

1. `Models/ViewModels/OnlineBookingViewModels.cs`: dữ liệu gửi cho giao diện và dữ liệu nhận từ form.
2. `Controllers/MoviesController.cs`: danh sách phim, chi tiết phim, sơ đồ ghế.
3. `Controllers/BookingController.cs`: giữ ghế, checkout, thanh toán, xem vé.
4. `Services/OnlineBookingService.cs`: mỗi phương thức là một nghiệp vụ, các bước quan trọng có comment tiếng Việt.
5. `wwwroot/js/booking.js`: gửi lựa chọn ghế, cập nhật sơ đồ và đếm ngược.
6. `Hubs/SeatHub.cs`, `Services/SeatHoldExpirationService.cs`: thông báo thay đổi và giải phóng ghế hết hạn.

## Những phần cần giữ dù mới học

- `async/await`: chờ truy vấn database mà không chặn luồng xử lý web.
- `Include/ThenInclude`: tải dữ liệu liên quan như phim, phòng và ghế của đơn.
- `AsNoTracking`: dữ liệu chỉ đọc, không cần EF theo dõi để ghi lại.
- `BeginTransactionAsync`, `CommitAsync`, `RollbackAsync`: nhiều thay đổi cùng thành công hoặc cùng hoàn tác.
- `ExecuteUpdateAsync`: kiểm tra điều kiện và cập nhật ghế trong cùng câu SQL. Kiểm tra bằng if rồi SaveChanges riêng không ngăn được hai người lấy cùng ghế.
- Cập nhật đơn trước rồi cập nhật ghế: các thao tác trên cùng đơn phải chờ nhau, tránh thanh toán và hủy chạy chồng lên nhau.
- `CancellationToken`: cho phép dừng truy vấn khi request hoặc ứng dụng đang tắt; không phải nghiệp vụ riêng.
- `IOnlineBookingService`: giữ cách đăng ký interface/service tương tự `IPasswordService` của module auth.

## Trạng thái nghiệp vụ

- Ghế: `Available` → `Held` → `Sold`; hủy/hết hạn thì `Held` → `Available`.
- Đơn: `Pending` → `Confirmed`, `Cancelled` hoặc `Expired`.
- Thanh toán thất bại vẫn giữ đơn `Pending` đến hạn ban đầu; không gia hạn.
- Giá dùng `ShowtimeSeat.Price` từ server. Phim mở bán là `Showing` theo schema hiện có.
- Database lưu UTC; giao diện hiển thị giờ Việt Nam.

## Kiểm chứng

Đang thực hiện; kết quả build, kiểm thử và review sẽ được ghi lại sau khi hoàn tất.
