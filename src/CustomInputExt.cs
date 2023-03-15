using UnityEngine;

namespace ImprovedInput;

/// <summary>
/// Extends the functionality of the vanilla input system.
/// </summary>
public static partial class CustomInputExt
{
    internal static int historyLength = 10;
    internal static bool historyLocked = false;

    /// <summary>
    /// Determines how many ticks of input are stored for <see cref="InputHistory(Player)"/> and <see cref="RawInputHistory(Player)"/>.
    /// </summary>
    /// <remarks>This value starts at 10 and can only be increased. Set it when your mod is being enabled. Avoid setting this to anything extremely high.</remarks>
    public static int HistoryLength {
        get => historyLength;
        set {
            if (historyLocked) {
                throw new System.InvalidOperationException("History length cannot be modified after the game has started.");
            }
            historyLength = Mathf.Max(historyLength, value);
        }
    }

    /// <summary>
    /// Checks if <paramref name="key"/> is being pressed by <paramref name="player"/>.
    /// </summary>
    /// <returns>True if the key is down.</returns>
    public static bool IsKeyBound(this Player player, PlayerKeybind key)
    {
        if (player.AI != null || player.playerState.playerNumber is < 0 or > 3) {
            return false;
        }
        return !key.Unbound(player.playerState.playerNumber);
    }

    /// <summary>
    /// Checks if <paramref name="key"/> is being pressed by <paramref name="player"/>.
    /// </summary>
    /// <returns>True if the key is down.</returns>
    public static bool IsPressed(this Player player, PlayerKeybind key)
    {
        return player.Input()[key];
    }

    /// <summary>
    /// Checks if <paramref name="key"/> is just now being pressed by <paramref name="player"/>.
    /// </summary>
    /// <returns>True if the key is down, but was not down last tick.</returns>
    public static bool JustPressed(this Player player, PlayerKeybind key)
    {
        var history = player.InputHistory();
        return history[0][key] && !history[1][key];
    }

    /// <summary>Gets all custom input for <paramref name="player"/> this tick.</summary>
    /// <remarks>Ignores keypresses made while unconscious, using the map, or being controlled. To use those keypresses, see <see cref="RawInput(Player)"/>.</remarks>
    public static CustomInput Input(this Player player)
    {
        return Plugin.players.GetValue(player, _ => new()).input[0];
    }

    /// <summary>Gets all custom input for <paramref name="player"/> this tick, including suppressed inputs. Avoid modifying anything here.</summary>
    public static CustomInput RawInput(this Player player)
    {
        return Plugin.players.GetValue(player, _ => new()).rawInput[0];
    }

    /// <summary>Gets all custom input for <paramref name="player"/> in recent history.</summary>
    /// <remarks>Ignores keypresses made while unconscious, using the map, or being controlled. To use those keypresses, see <see cref="RawInputHistory(Player)"/>.</remarks>
    public static CustomInput[] InputHistory(this Player player)
    {
        return Plugin.players.GetValue(player, _ => new()).input;
    }

    /// <summary>Gets all custom input for <paramref name="player"/> in recent history, including suppressed inputs. Avoid modifying anything here.</summary>
    public static CustomInput[] RawInputHistory(this Player player)
    {
        return Plugin.players.GetValue(player, _ => new()).rawInput;
    }
}
