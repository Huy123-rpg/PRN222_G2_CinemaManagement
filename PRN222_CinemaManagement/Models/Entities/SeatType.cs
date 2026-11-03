namespace PRN222_CinemaManagement.Models.Entities;

public class SeatType
{
    public int SeatTypeId { get; set; }
    public string TypeName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal PriceMultiplier { get; set; } = 1.000m;
    public bool IsActive { get; set; } = true;

    public ICollection<Seat> Seats { get; set; } = new List<Seat>();
}
