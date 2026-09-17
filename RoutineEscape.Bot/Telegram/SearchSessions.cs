namespace RoutineEscape.Bot.Telegram;

// Read-only search context: never captures the user's next plain-text message.
public sealed class SearchSessions
{
    private readonly Dictionary<long, Session> sessions = [];
    private readonly object gate = new();
    public sealed record Session(string Token, string Query, DateTimeOffset ExpiresAt);

    public Session Create(long user, string query, DateTimeOffset now)
    {
        lock (gate)
        {
            foreach (var key in sessions.Where(pair => pair.Value.ExpiresAt <= now).Select(pair => pair.Key).ToArray()) sessions.Remove(key);
            if (sessions.Count >= 1024) sessions.Remove(sessions.MinBy(pair => pair.Value.ExpiresAt).Key);
            var session = new Session(Guid.NewGuid().ToString("N"), query, now.AddMinutes(30));
            sessions[user] = session;
            return session;
        }
    }

    public Session? Get(long user, string token, DateTimeOffset now)
    {
        lock (gate) return sessions.TryGetValue(user, out var session) && session.Token == token && session.ExpiresAt > now ? session : null;
    }
    public void Clear(long user) { lock (gate) sessions.Remove(user); }
}
