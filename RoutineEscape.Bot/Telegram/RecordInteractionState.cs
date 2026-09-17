using System.Collections.Concurrent;
using RoutineEscape.Domain.Enums;
using System.Text.Json;

namespace RoutineEscape.Bot.Telegram;

public sealed record RecordInteraction(Guid Token, Intent Intent, Guid RecordId, string Field, string Version, DateTimeOffset ExpiresAt);

public sealed class RecordInteractionState
{
    private readonly ConcurrentDictionary<long, RecordInteraction> sessions = new();
    private readonly ConcurrentDictionary<long, int> lastInputs = new();
    private readonly ConcurrentDictionary<string, CardStamp> cards = new();
    private readonly SemaphoreSlim[] gates = Enumerable.Range(0, 64).Select(_ => new SemaphoreSlim(1, 1)).ToArray();
    private readonly object persistenceGate = new();
    private readonly string? path;

    public RecordInteractionState(string? path = null)
    {
        this.path = path;
        if (path is not null && File.Exists(path))
        {
            var saved = JsonSerializer.Deserialize<PersistedState>(File.ReadAllText(path)) ?? throw new InvalidDataException("Invalid interaction state.");
            foreach (var item in saved.Sessions) sessions[item.Key] = item.Value;
            foreach (var item in saved.LastInputs) lastInputs[item.Key] = item.Value;
            foreach (var item in saved.Cards ?? []) cards[item.Key] = item.Value;
        }
    }
    public SemaphoreSlim Gate(long userId) => gates[(int)((ulong)userId % (ulong)gates.Length)];
    public RecordInteraction? Get(long userId) => sessions.GetValueOrDefault(userId);
    public void Set(long userId, RecordInteraction session) { lock (persistenceGate) { sessions[userId] = session; Save(); } }
    public void Clear(long userId) { lock (persistenceGate) { sessions.TryRemove(userId, out _); Save(); } }
    public bool WasHandled(long userId, int messageId) => lastInputs.TryGetValue(userId, out var previous) && previous == messageId;
    public void MarkHandled(long userId, int messageId) { lock (persistenceGate) { lastInputs[userId] = messageId; Save(); } }
    public string IssueCard(long user, Guid id, string version)
    {
        lock (persistenceGate)
        {
            var stamp = new CardStamp(Guid.NewGuid().ToString("N")[..10], version);
            cards[$"{user}:{id:N}"] = stamp; Save(); return stamp.Token;
        }
    }
    public bool IsCurrentCard(long user, Guid id, string token, string version) =>
        cards.TryGetValue($"{user}:{id:N}", out var stamp) && stamp.Token == token && stamp.Version == version;
    public void InvalidateCard(long user, Guid id) { lock (persistenceGate) { cards.TryRemove($"{user}:{id:N}", out _); Save(); } }
    private void Save()
    {
        if (path is null) return;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(new PersistedState(new(sessions), new(lastInputs), new(cards))));
        File.Move(path + ".tmp", path, overwrite: true);
    }
    public sealed record CardStamp(string Token, string Version);
    private sealed record PersistedState(Dictionary<long, RecordInteraction> Sessions, Dictionary<long, int> LastInputs, Dictionary<string, CardStamp>? Cards = null);
}
