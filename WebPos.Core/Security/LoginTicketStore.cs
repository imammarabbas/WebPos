using System.Collections.Concurrent;
using WebPos.Core.Models;

namespace WebPos.Core.Security;

/// <summary>
/// Short-lived one-time tickets so Blazor Interactive Server can complete
/// cookie SignIn via a full browser navigation (HttpContext.SignInAsync cannot
/// reliably set cookies from an established circuit).
/// </summary>
public sealed class LoginTicketStore
{
    private static readonly TimeSpan TicketLifetime = TimeSpan.FromSeconds(60);

    private readonly ConcurrentDictionary<string, TicketEntry> _tickets = new(StringComparer.Ordinal);

    public string Issue(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(user.Role);

        string ticket = Convert.ToHexString(Guid.NewGuid().ToByteArray())
            + Convert.ToHexString(Guid.NewGuid().ToByteArray());

        _tickets[ticket] = new TicketEntry(
            user.Id,
            user.Username,
            user.Role.RoleName,
            user.TenantId,
            DateTimeOffset.UtcNow.Add(TicketLifetime));

        return ticket;
    }

    public bool TryRedeem(string? ticket, out LoginTicketPayload payload)
    {
        payload = default!;
        if (string.IsNullOrWhiteSpace(ticket))
        {
            return false;
        }

        if (!_tickets.TryRemove(ticket, out TicketEntry? entry))
        {
            return false;
        }

        if (entry.ExpiresAtUtc < DateTimeOffset.UtcNow)
        {
            return false;
        }

        payload = new LoginTicketPayload(
            entry.UserId,
            entry.Username,
            entry.RoleName,
            entry.TenantId);
        return true;
    }

    private sealed record TicketEntry(
        Guid UserId,
        string Username,
        string RoleName,
        Guid TenantId,
        DateTimeOffset ExpiresAtUtc);
}

public sealed record LoginTicketPayload(
    Guid UserId,
    string Username,
    string RoleName,
    Guid TenantId);
