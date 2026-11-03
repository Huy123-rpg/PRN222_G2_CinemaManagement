namespace PRN222_CinemaManagement.Models.Entities;

public class Admin
{
    public int AdminId { get; set; }
    public string AdminCode { get; set; } = string.Empty;
    public int UserId { get; set; }

    public User User { get; set; } = null!;
}
