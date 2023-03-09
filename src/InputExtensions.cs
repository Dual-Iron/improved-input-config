namespace BetterInputConfig;

public static class InputExtensions
{
    public static bool IsPressed(this Player p, PlayerKeybind key)
    {
        return p.Input()[key];
    }

    public static bool JustPressed(this Player p, PlayerKeybind key)
    {
        var history = p.InputHistory();
        return history[0][key] && !history[1][key];
    }

    public static CustomInput Input(this Player p)
    {
        return Plugin.players.GetValue(p, _ => new()).input[0];
    }

    public static CustomInput RawInput(this Player p)
    {
        return Plugin.players.GetValue(p, _ => new()).rawInput[0];
    }

    public static CustomInput[] InputHistory(this Player p)
    {
        return Plugin.players.GetValue(p, _ => new()).input;
    }

    public static CustomInput[] RawInputHistory(this Player p)
    {
        return Plugin.players.GetValue(p, _ => new()).rawInput;
    }
}
