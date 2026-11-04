using System.Collections.Concurrent;
using CISCProcessorLibrary;

namespace CISCProcessorWeb;

/// <summary>One processor instance owned by one browser tab.</summary>
public sealed class MachineSession
{
    private readonly object gate = new();

    public MachineSession(string id, AssembledProgram program)
    {
        Id = id;
        Program = program;
        Cpu = new CISCProcessor();
        Cpu.LoadProgram(program);
        Touch();
    }

    public string Id { get; }

    public AssembledProgram Program { get; }

    public CISCProcessor Cpu { get; }

    public DateTime LastUsedUtc { get; private set; }

    public void Touch() => LastUsedUtc = DateTime.UtcNow;

    /// <summary>Runs <paramref name="work"/> while holding the session lock, so concurrent requests cannot interleave steps.</summary>
    public T Locked<T>(Func<T> work)
    {
        lock (gate)
        {
            Touch();
            return work();
        }
    }
}

/// <summary>Keeps the live sessions and evicts the ones nobody used for a while.</summary>
public sealed class SessionStore
{
    private static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(30);
    private const int MaxSessions = 500;
    private readonly ConcurrentDictionary<string, MachineSession> sessions = new();

    public MachineSession Create(AssembledProgram program)
    {
        Sweep();
        var session = new MachineSession(Guid.NewGuid().ToString("N"), program);
        sessions[session.Id] = session;
        return session;
    }

    public MachineSession? Find(string id) => sessions.TryGetValue(id, out MachineSession? session) ? session : null;

    public bool Remove(string id) => sessions.TryRemove(id, out _);

    public int Count => sessions.Count;

    private void Sweep()
    {
        DateTime cutoff = DateTime.UtcNow - IdleTimeout;
        foreach (MachineSession session in sessions.Values)
        {
            if (session.LastUsedUtc < cutoff) sessions.TryRemove(session.Id, out _);
        }

        if (sessions.Count >= MaxSessions)
        {
            foreach (MachineSession session in sessions.Values.OrderBy(s => s.LastUsedUtc).Take(sessions.Count - MaxSessions + 1))
                sessions.TryRemove(session.Id, out _);
        }
    }
}
