namespace PRN222_CinemaManagement.Models.Entities;

public class CinemaManager
{
    public int ManagerId { get; set; }
    public string ManagerCode { get; set; } = string.Empty;
    public int UserId { get; set; }

    public User User { get; set; } = null!;
}
