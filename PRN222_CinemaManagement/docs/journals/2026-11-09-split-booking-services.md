# Tách các nghiệp vụ đặt vé thành service riêng

Ngày: 09/11/2026.

## Bối cảnh

Module đặt vé cần dễ đọc và tìm nghiệp vụ cho người mới học. `OnlineBookingService` trước đó tập trung nhiều phần xử lý trong một file.

## Thay đổi và quyết định

Đã tách xem phim, giữ ghế, thanh toán và xem vé sang bốn class trong `Services/OnlineBooking`. `BookingSupportService` chứa các bước dùng chung; `OnlineBookingService` giữ vai trò đầu mối chuyển lời gọi để controller và worker tiếp tục dùng interface hiện có.

Các service dùng Scoped và cùng DbContext trong request. Transaction vẫn nằm tại nghiệp vụ giữ ghế/thanh toán; lớp hỗ trợ không tự commit. `VnPayService` tạo URL thanh toán, còn `BookingPaymentService` xử lý kết quả và phát vé.

Cách chia theo việc cần làm giúp tìm code trực tiếp mà không cần thêm interface cho từng class. Không thay đổi auth, entity, schema hoặc giao diện request của module trong lần tách này.

## Tiếp theo

Review độc lập đã xác nhận 21 phương thức được chuyển giữ nguyên phần xử lý ngoài tên gọi lớp hỗ trợ; 11 API của facade và cách dùng Scoped/transaction được giữ nguyên. Chờ kết quả build và kiểm thử hồi quy; cập nhật trạng thái trong `docs/online-booking-simple.md` khi có kết quả thực tế.

