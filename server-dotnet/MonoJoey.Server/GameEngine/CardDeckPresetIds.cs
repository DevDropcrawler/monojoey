namespace MonoJoey.Server.GameEngine;

public static class CardDeckPresetIds
{
    public const string ClassicIsh = "classic_ish";
    public const string Chaos = "chaos";
    public const string CustomReady = "custom_ready";
    public const string Default = ClassicIsh;

    public static bool IsKnown(string presetId)
    {
        return presetId is ClassicIsh or Chaos or CustomReady;
    }
}
