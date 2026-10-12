namespace CoreModels.Entities;

public sealed class AppUser
{
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = "Customer";
    public DateTime CreatedOn { get; set; }
    public bool IsActive { get; set; } = true;
}
