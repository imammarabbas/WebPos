namespace WebPos.Core.Models;

public class Role
{
    public Guid Id { get; set; }

    public string RoleName { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<User> Users { get; set; } = new List<User>();
}
