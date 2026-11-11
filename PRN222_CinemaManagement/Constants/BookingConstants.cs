namespace PRN222_CinemaManagement.Constants;

public static class BookingConstants
{
    public const int HoldDurationMinutes = 5;
    public const int MaxSeatsPerBooking = 20;
    public const int MaxQuantityPerConcessionItem = 20;
    public const int ExpirationBatchSize = 200;
    public const int VietnamUtcOffsetHours = 7;
    public const string CustomerRole = "Customer";
    public const string ActiveUserStatus = "Active";
    public const string UserIdSessionKey = "UserId";
    public const string ConcessionTicketPrefix = "CN-";

    public static class ConcessionStatuses
    {
        public const string Active = "Active";
        public const string Inactive = "Inactive";
    }

    public static class MovieSortOptions
    {
        public const string TitleAsc = "title_asc";
        public const string TitleDesc = "title_desc";
        public const string Newest = "newest";
        public const string DurationAsc = "duration_asc";
        public const string DurationDesc = "duration_desc";
        public const string ShowtimesDesc = "showtimes_desc";
    }

    public static class Channels
    {
        public const string Online = "Online";
        public const string Counter = "Counter";
    }

    public static class BookingStatuses
    {
        public const string Pending = "Pending";
        public const string Confirmed = "Confirmed";
        public const string Cancelled = "Cancelled";
        public const string Expired = "Expired";
        public const string Refunded = "Refunded";
    }

    public static class ShowtimeSeatStatuses
    {
        public const string Available = "Available";
        public const string Held = "Held";
        public const string Sold = "Sold";
        public const string Blocked = "Blocked";
    }

    public static class PhysicalSeatStatuses
    {
        public const string Active = "Active";
    }

    public static class ShowtimeStatuses
    {
        public const string Scheduled = "Scheduled";
    }

    public static class MovieStatuses
    {
        public const string Upcoming = "Upcoming";
        public const string Showing = "Showing";
        public const string Ended = "Ended";
    }

    public static class AuditoriumStatuses
    {
        public const string Active = "Active";
    }

    public static class TicketStatuses
    {
        public const string Valid = "Valid";
        public const string Cancelled = "Cancelled";
    }

    public static class PaymentProviders
    {
        public const string VNPay = "VNPay";
        public const string VNPaySimulated = "VNPay_Simulated";
        public const string MoMoSimulated = "MoMo_Simulated";
    }

    public static class PaymentMethods
    {
        public const string VNPaySimulated = "VNPay_Simulated";
        public const string MoMoSimulated = "MoMo_Simulated";
    }

    public static class PaymentOutcomes
    {
        public const string Success = "Success";
        public const string Failed = "Failed";
        public const string Cancelled = "Cancelled";
    }
}

