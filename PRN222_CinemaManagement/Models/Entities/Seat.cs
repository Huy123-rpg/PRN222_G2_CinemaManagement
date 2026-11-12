namespace PRN222_CinemaManagement.Models.Entities;

public class Seat
{
    public int SeatId { get; set; }
    public int AuditoriumId { get; set; }
    public int SeatTypeId { get; set; }
    public string RowLabel { get; set; } = string.Empty;
    public int SeatNumber { get; set; }
    public string Status { get; set; } = "Active";

    public Auditorium Auditorium { get; set; } = null!;
    public SeatType SeatType { get; set; } = null!;
    public ICollection<ScreeningSeat> ScreeningSeats { get; set; } = new List<ScreeningSeat>();
}
