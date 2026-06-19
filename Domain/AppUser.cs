using Microsoft.AspNetCore.Identity;

namespace CallbackListener.Domain;

public sealed class AppUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<Listener> Listeners { get; set; } = [];
    public List<Client> Clients { get; set; } = [];
}
