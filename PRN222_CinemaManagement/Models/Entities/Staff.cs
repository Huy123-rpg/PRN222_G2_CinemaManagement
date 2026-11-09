namespace PRN222_CinemaManagement.Models.Entities;

public class Staff
{
    public int StaffId { get; set; }
    public string StaffCode { get; set; } = string.Empty;
    public int UserId { get; set; }

    public User User { get; set; } = null!;
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}
