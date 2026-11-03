namespace PRN222_CinemaManagement.Models.Entities;

public class User
{
    public int UserId { get; set; }
    public int RoleId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string Status { get; set; } = "Active";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public byte[] RowVersion { get; set; } = [];

    public Role Role { get; set; } = null!;
    public Admin? Admin { get; set; }
    public CinemaManager? CinemaManager { get; set; }
    public Staff? Staff { get; set; }
    public Customer? Customer { get; set; }
}
