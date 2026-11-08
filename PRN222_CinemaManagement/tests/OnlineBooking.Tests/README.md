# Kiểm thử đặt vé online

Chạy từ thư mục dự án:

```powershell
dotnet test tests/OnlineBooking.Tests/OnlineBooking.Tests.csproj
```

Cần SQL Server cục bộ và tài khoản Windows có quyền tạo/xóa database kiểm thử. Mặc định kết nối `Server=.` bằng Windows Authentication. Có thể đặt biến môi trường `CINEMA_TEST_SQL` để dùng SQL Server khác; không ghi mật khẩu vào repository.

Mỗi lần chạy tự tạo database `CinemaBookingTests_<GUID>` và xóa đúng database đó khi kết thúc. Tên database trong chuỗi kết nối luôn được thay bằng tên kiểm thử này; bài test không sửa `CinemaDB`.

Các bài test dùng EF Core/SQL Server thật để kiểm tra tranh chấp ghế, rollback, thời hạn giữ ghế, thanh toán lặp, quyền sở hữu, QR và việc nhả ghế hết hạn. SignalR được thay bằng đối tượng giả để không cần mở trình duyệt. Bài test không thay thế kiểm tra giao diện thủ công.
