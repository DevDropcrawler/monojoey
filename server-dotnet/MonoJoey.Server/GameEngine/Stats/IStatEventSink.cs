namespace MonoJoey.Server.GameEngine.Stats;

internal interface IStatEventSink
{
    void Emit(StatEvent evt);
}
