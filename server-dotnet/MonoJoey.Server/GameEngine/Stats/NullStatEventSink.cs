namespace MonoJoey.Server.GameEngine.Stats;

internal sealed class NullStatEventSink : IStatEventSink
{
    public static NullStatEventSink Instance { get; } = new();

    private NullStatEventSink()
    {
    }

    public void Emit(StatEvent evt)
    {
    }
}
