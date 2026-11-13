using PRN222_CinemaManagement.Models.Entities;
using PRN222_CinemaManagement.Repositories.IRepositories;
using PRN222_CinemaManagement.Services.IService;

namespace PRN222_CinemaManagement.Services.ImpleService;

/// <summary>
/// Triển khai nghiệp vụ tính toán báo cáo doanh thu và đối soát.
/// Chuẩn Lab 2 PRN222: Sử dụng LINQ, foreach, if/else trực tiếp, dễ đọc và giải thích.
/// </summary>
public class RevenueReportService(IRevenueReportRepository repository) : IRevenueReportService
{
    public async Task<RevenueSummaryResult> CalculateRevenueSummaryAsync(DateTime utcStart, DateTime utcEnd, int? staffId = null)
    {
        var payments = await repository.GetPaymentsAsync(utcStart, utcEnd, staffId);
        var concessionSales = await repository.GetConcessionSalesAsync(utcStart, utcEnd, staffId);

        decimal ticketRev = 0m;
        decimal bookingConcessionRev = 0m;
        decimal totalRefund = 0m;
        int totalTicketsSold = 0;

        // Gom nhóm thanh toán theo BookingId để tránh cộng lặp BookingConcessions hoặc Tickets nếu 1 Booking có nhiều Payment
        var paymentsByBooking = payments.GroupBy(p => p.BookingId);

        foreach (var group in paymentsByBooking)
        {
            var booking = group.First().Booking;
            decimal totalPaid = group.Sum(p => p.Amount);
            decimal totalRefundForBooking = group.Sum(p => p.RefundAmount);
            decimal net = totalPaid - totalRefundForBooking;

            totalRefund += totalRefundForBooking;

            // Kiểm tra bắp nước mua kèm trong đơn (nếu có ở đơn online)
            decimal rawConc = booking.BookingConcessions.Sum(bc => (decimal?)bc.Subtotal) ?? 0m;

            if (net <= 0)
            {
                // Nếu đơn đã được hoàn tiền toàn bộ (100%), không ghi nhận doanh thu vé hoặc bắp nước
                // net <= 0 nghĩa là rạp không giữ lại tiền
            }
            else if (totalRefundForBooking > 0)
            {
                // Nếu hoàn tiền một phần: do CSDL không có cột phân bổ riêng hoàn vé hay hoàn bắp nước,
                // hệ thống phân bổ ước tính theo tỷ lệ tiền thực giữ lại (net / totalPaid) để đảm bảo tổng doanh thu không vượt quá số tiền thực giữ
                decimal ratio = totalPaid > 0 ? (net / totalPaid) : 0m;
                decimal effectiveConc = Math.Round(rawConc * ratio, 2);
                decimal effectiveTicket = net - effectiveConc;

                bookingConcessionRev += effectiveConc;
                ticketRev += effectiveTicket;
            }
            else
            {
                // Không có hoàn tiền: tính đúng bắp nước và vé thuần
                decimal effectiveConc = rawConc;
                decimal effectiveTicket = net - effectiveConc;
                if (effectiveTicket < 0) effectiveTicket = 0m;

                bookingConcessionRev += effectiveConc;
                ticketRev += effectiveTicket;
            }

            // Đếm số vé bán thực tế (chỉ đếm 1 lần cho mỗi booking, loại trừ vé đã hủy hoặc đơn hủy)
            if (booking.Status != "Cancelled" && booking.Status != "Refunded")
            {
                totalTicketsSold += booking.Tickets.Count(t => t.Status != "Cancelled");
            }
        }

        // Doanh thu bắp nước bán riêng tại quầy (ConcessionSale)
        decimal standaloneRev = concessionSales.Sum(cs => cs.TotalAmount);
        decimal totalConcRev = standaloneRev + bookingConcessionRev;
        decimal grandTotal = ticketRev + totalConcRev;

        int concessionOrderCount = concessionSales.Count + paymentsByBooking.Count(g => g.First().Booking.BookingConcessions.Count > 0);

        return new RevenueSummaryResult(
            payments,
            concessionSales,
            ticketRev,
            totalConcRev,
            standaloneRev,
            bookingConcessionRev,
            grandTotal,
            totalRefund,
            payments.Count,
            totalTicketsSold,
            concessionOrderCount,
            concessionSales.Count);
    }

    public Task<List<Staff>> GetStaffListAsync() =>
        repository.GetStaffListAsync();

    public async Task<CancellationReportResult> GetCancellationReportAsync(DateTime? fromDate, DateTime? toDate, string? statusFilter)
    {
        DateTime? utcFrom = fromDate.HasValue ? fromDate.Value.Date.AddHours(-7) : null;
        DateTime? utcTo = toDate.HasValue ? toDate.Value.Date.AddDays(1).AddHours(-7) : null;

        var list = await repository.GetCancelledBookingsAsync(utcFrom, utcTo, statusFilter);

        decimal totalCancelledVal = list.Sum(b => b.TotalAmount);
        decimal totalRefund = list.Sum(b => b.Payments.Sum(p => p.RefundAmount));
        int totalCancelledTickets = list.Sum(b => b.Tickets.Count(t => t.Status == "Cancelled" || b.Status == "Cancelled" || b.Status == "Refunded"));

        return new CancellationReportResult(list, totalCancelledVal, totalRefund, totalCancelledTickets, list.Count);
    }

    public async Task<ReconciliationResult> GetReconciliationAsync(DateTime utcStart, DateTime utcEnd)
    {
        // 1. Lấy toàn bộ Booking phát sinh trong kỳ hoặc có thanh toán trong kỳ (bao gồm cả đơn thiếu payment hoặc trùng payment)
        var bookings = await repository.GetBookingsForReconciliationAsync(utcStart, utcEnd);
        var concessionSales = await repository.GetConcessionSalesAsync(utcStart, utcEnd);

        var items = new List<ReconciliationItem>();

        decimal grossRevenue = 0m;
        decimal totalRefund = 0m;
        decimal standaloneRevenue = concessionSales.Sum(cs => cs.TotalAmount);
        decimal bookingConcRevenue = 0m;
        decimal ticketRevenue = 0m;

        // 2. Đối soát từng hóa đơn bắp nước bán riêng tại quầy
        foreach (var cs in concessionSales)
        {
            var item = new ReconciliationItem
            {
                TransactionType = "Bắp nước quầy",
                Code = cs.SaleCode,
                TransactionTime = cs.SaleDate.AddHours(7),
                Actor = cs.Staff?.User?.FullName != null ? $"{cs.Staff.User.FullName} ({cs.Staff.StaffCode})" : "Thu ngân",
                HeaderAmount = cs.TotalAmount,
                PaidAmount = cs.TotalAmount,
                RefundAmount = 0m
            };

            if (cs.ConcessionSaleItems.Count == 0)
            {
                if (cs.TotalAmount > 0)
                {
                    item.DetailAmount = 0m;
                    item.Difference = cs.TotalAmount;
                    item.Status = "InsufficientData";
                    item.Reason = "Hóa đơn có tiền nhưng thiếu danh sách chi tiết món (ConcessionSaleItems rỗng)";
                }
                else
                {
                    item.DetailAmount = 0m;
                    item.Status = "Matched";
                    item.Reason = "Hóa đơn 0đ không có dòng món";
                }
            }
            else
            {
                bool hasSubtotalError = cs.ConcessionSaleItems.Any(i => i.Subtotal != i.Quantity * i.UnitPrice);
                decimal itemsSum = cs.ConcessionSaleItems.Sum(i => i.Subtotal);
                item.DetailAmount = itemsSum;

                if (hasSubtotalError)
                {
                    item.Difference = Math.Abs(cs.TotalAmount - itemsSum);
                    item.Status = "Discrepancy";
                    item.Reason = "Lỗi tính dòng hàng: Subtotal khác Số lượng * Đơn giá";
                }
                else if (cs.TotalAmount != itemsSum)
                {
                    item.Difference = cs.TotalAmount - itemsSum;
                    item.Status = "Discrepancy";
                    item.Reason = $"Tổng tiền hóa đơn ({cs.TotalAmount:N0}đ) khác tổng chi tiết món ({itemsSum:N0}đ)";
                }
                else
                {
                    item.Status = "Matched";
                    item.Reason = "Khớp hoàn toàn giữa hóa đơn và chi tiết món";
                }
            }
            items.Add(item);
        }

        // 3. Đối soát từng đơn đặt vé & bắp nước (kiểm tra thiếu payment, tiền lệch, split payment)
        foreach (var b in bookings)
        {
            var successPayments = b.Payments.Where(p => p.PaymentStatus == "Success" && p.Amount > 0).ToList();
            decimal totalPaid = successPayments.Sum(p => p.Amount);
            decimal refAmt = successPayments.Sum(p => p.RefundAmount);

            decimal ticketSum = b.Tickets.Sum(t => t.Price);
            decimal concSum = b.BookingConcessions.Sum(bc => bc.Subtotal);
            decimal detailSum = ticketSum + concSum;

            grossRevenue += totalPaid;
            totalRefund += refAmt;

            string actor = b.BookingChannel == "Counter"
                ? (b.Staff?.User?.FullName != null ? $"{b.Staff.User.FullName} ({b.Staff.StaffCode})" : "Thu ngân quầy")
                : (b.Customer?.User?.FullName ?? "Khách online");

            string paymentCodes = successPayments.Count > 0
                ? string.Join(", ", successPayments.Select(p => p.PaymentCode))
                : "CHƯA_THANH_TOÁN";

            var item = new ReconciliationItem
            {
                TransactionType = b.BookingConcessions.Count > 0 ? "Vé & Bắp nước" : "Vé xem phim",
                Code = $"{b.BookingCode} / {paymentCodes}",
                TransactionTime = (b.Payments.FirstOrDefault()?.PaidAt ?? b.BookingDate).AddHours(7),
                Actor = actor,
                HeaderAmount = b.TotalAmount,
                DetailAmount = detailSum,
                PaidAmount = totalPaid,
                RefundAmount = refAmt
            };

            // Kiểm tra các trường hợp bất thường
            if (successPayments.Count == 0 && (b.Status == "Confirmed" || b.TotalAmount > 0))
            {
                item.Difference = b.TotalAmount;
                item.Status = "Discrepancy";
                item.Reason = $"Đơn hàng trạng thái '{b.Status}' (Tổng: {b.TotalAmount:N0}đ) nhưng THIẾU BẢN GHI THANH TOÁN THÀNH CÔNG!";
            }
            else if (refAmt < 0 || refAmt > totalPaid)
            {
                item.Difference = refAmt;
                item.Status = "Discrepancy";
                item.Reason = $"Tiền hoàn ({refAmt:N0}đ) không hợp lệ so với tiền đã trả ({totalPaid:N0}đ)";
            }
            else if (totalPaid != b.TotalAmount && b.Status != "Pending")
            {
                item.Difference = totalPaid - b.TotalAmount;
                item.Status = "Discrepancy";
                item.Reason = totalPaid > b.TotalAmount
                    ? $"Thanh toán thừa ({totalPaid:N0}đ > đơn {b.TotalAmount:N0}đ)"
                    : $"Thanh toán thiếu ({totalPaid:N0}đ < đơn {b.TotalAmount:N0}đ)";
            }
            else if (b.Tickets.Count == 0 && b.BookingConcessions.Count == 0 && b.TotalAmount > 0)
            {
                item.Difference = b.TotalAmount;
                item.Status = "InsufficientData";
                item.Reason = "Đơn có giá trị nhưng thiếu dữ liệu chi tiết vé và bắp nước để đối chiếu";
            }
            else if (detailSum != b.TotalAmount && b.Status != "Pending")
            {
                item.Difference = b.TotalAmount - detailSum;
                item.Status = "Discrepancy";
                item.Reason = $"Tổng chi tiết ({detailSum:N0}đ) khác tổng đơn ({b.TotalAmount:N0}đ)";
            }
            else if (b.Status == "Cancelled" || b.Status == "Refunded")
            {
                if (totalPaid > 0 && refAmt == 0)
                {
                    item.Difference = totalPaid;
                    item.Status = "InsufficientData";
                    item.Reason = $"Đơn {b.Status} đã thu {totalPaid:N0}đ nhưng tiền hoàn ghi nhận 0đ (Chờ xác nhận duyệt hoàn tiền hoặc chứng từ chính sách)";
                }
                else if (totalPaid > 0 && refAmt > 0 && refAmt < totalPaid)
                {
                    item.Difference = totalPaid - refAmt;
                    item.Status = "InsufficientData";
                    item.Reason = $"Đơn {b.Status} đã hoàn một phần ({refAmt:N0}đ / {totalPaid:N0}đ) - Chờ xác nhận phần chênh lệch còn lại";
                }
                else
                {
                    item.Status = "Matched";
                    item.Reason = refAmt > 0
                        ? $"Đơn {b.Status}, đã hoàn trả toàn bộ {refAmt:N0}đ, số liệu khớp"
                        : $"Đơn {b.Status} chưa thanh toán (0đ), số liệu khớp";
                }
            }
            else if (successPayments.Count > 1)
            {
                item.Status = "Matched";
                item.Reason = $"Thanh toán nhiều phần ({successPayments.Count} lượt), khớp đúng tổng đơn ({totalPaid:N0}đ)";
            }
            else
            {
                item.Status = "Matched";
                item.Reason = "Khớp hoàn toàn giữa thanh toán, giá trị đơn và chi tiết vé/bắp nước";
            }

            items.Add(item);

            // Bổ sung doanh thu đối soát nếu thanh toán thành công và đơn đã khớp
            if (totalPaid > 0 && item.Status == "Matched")
            {
                decimal net = totalPaid - refAmt;
                if (net > 0)
                {
                    decimal ratio = totalPaid > 0 ? (net / totalPaid) : 0m;
                    decimal effConc = Math.Round(concSum * ratio, 2);
                    bookingConcRevenue += effConc;
                    ticketRevenue += (net - effConc);
                }
            }
        }

        grossRevenue += standaloneRevenue;
        decimal netRevenue = grossRevenue - totalRefund;
        decimal totalConcRevenue = standaloneRevenue + bookingConcRevenue;

        int totalChecked = items.Count;
        int discrepancyCount = items.Count(i => i.Status == "Discrepancy");
        int insufficientCount = items.Count(i => i.Status == "InsufficientData");

        string overallStatus = totalChecked == 0
            ? "NoData"
            : (discrepancyCount > 0 ? "Discrepancy" : (insufficientCount > 0 ? "InsufficientData" : "Matched"));

        return new ReconciliationResult(
            items,
            grossRevenue,
            totalRefund,
            netRevenue,
            ticketRevenue,
            totalConcRevenue,
            standaloneRevenue,
            bookingConcRevenue,
            totalChecked,
            discrepancyCount,
            insufficientCount,
            overallStatus);
    }
}
