using Rewired;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BetterInputConfig;

public static class InputExtensions
{
    static int historyLength = 10;

    /// <summary>
    /// Determines how many ticks of history are stored for use in methods like <see cref="InputHistory(Player)"/>.
    /// </summary>
    /// <remarks>This value starts at 10 and can only be increased.</remarks>
    public static int HistoryLength {
        get => historyLength;
        set => historyLength = Mathf.Max(historyLength, value);
    }

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

    internal static string ButtonText(int player, KeyCode keyCode, out Color? color)
    {
        color = null;
        var text = keyCode.ToString();
        if (text.Length > 14 && text.Substring(0, 14) == "JoystickButton" && int.TryParse(text.Substring(14, text.Length - 14), out int btn)) {
            return ControllerButtonName(player, btn, out color);
        }
        if (text.Length > 15 && text.Substring(0, 8) == "Joystick" && int.TryParse(text.Substring(15, text.Length - 15), out int btn2)) {
            return ControllerButtonName(player, btn2, out color);
        }
        if (KeyboardButtonName(keyCode) is string name) {
            return name;
        }
        return text;
    }
    static string ControllerButtonName(int player, int joystickButton, out Color? color)
    {
        // Thank the internet honestly. It's not like I knew these mappings before googling them
        // Gets whatever controller `player` is using and displays the button name for that controller
        Options.ControlSetup.Preset ty = GetControllerType(player);

        if (ty == Options.ControlSetup.Preset.XBox) {
            color = joystickButton switch {
                0 => new Color32(60, 219, 78, 255),
                1 => new Color32(208, 66, 66, 255),
                2 => new Color32(64, 204, 208, 255),
                3 => new Color32(236, 219, 51, 255),
                _ => null
            };
            return joystickButton switch {
                0 => "A",
                1 => "B",
                2 => "X",
                3 => "Y",
                4 => "LB",
                5 => "RB",
                6 => "Menu",
                7 => "View",
                8 => "LSB",
                9 => "RSB",
                12 => "XBox",
                _ => $"Button {joystickButton}"
            };
        }
        else if (ty == Options.ControlSetup.Preset.PS4DualShock || ty == Options.ControlSetup.Preset.PS5DualSense) {
            color = joystickButton switch {
                0 => new Color32(155, 173, 228, 255),
                1 => new Color32(240, 110, 108, 255),
                2 => new Color32(213, 145, 189, 255),
                3 => new Color32(56, 222, 200, 255),
                _ => null
            };
            return joystickButton switch {
                0 => "X",
                1 => "O",
                2 => "Square",
                3 => "Triangle",
                4 => "L1",
                5 => "R1",
                6 => "Share",
                7 => "Options",
                8 => "LSB",
                9 => "RSB",
                12 => "PS",
                13 => "Touchpad",
                _ => $"Button {joystickButton}"
            };
        }
        else if (ty == Options.ControlSetup.Preset.SwitchProController) {
            color = null;
            return joystickButton switch {
                0 => "B",
                1 => "A",
                2 => "Y",
                3 => "X",
                4 => "L",
                5 => "R",
                6 => "ZL",
                7 => "ZR",
                8 => "-",
                9 => "+",
                10 => "LSB",
                11 => "RSB",
                12 => "Home",
                13 => "Capture",
                _ => $"Button {joystickButton}"
            };
        }
        color = null;
        if (ty == Options.ControlSetup.Preset.None) {
            return "< N / A >";
        }
        Plugin.Logger.LogWarning($"Unrecognized controller type {ty}");
        return $"Button {joystickButton}";
    }
    internal static Options.ControlSetup.Preset GetControllerType(int player)
    {
        // Beg the game to tell us what controller is being used
        IList<Joystick> j = ReInput.controllers.Joysticks;
        int i = RWCustom.Custom.rainWorld.options.controls[player].gamePadNumber;
        if (i > 0) i--; // account for "any controller" being 0
        if (i < 0 || i >= j.Count) {
            return Options.ControlSetup.Preset.None;
        }
        else if (RWInput.IsXboxControllerType(j[i].name, j[i].hardwareIdentifier)) {
            return Options.ControlSetup.Preset.XBox;
        }
        else if (RWInput.IsSwitchProControllerType(j[i].name, j[i].hardwareIdentifier)) {
            return Options.ControlSetup.Preset.SwitchProController;
        }
        else if (RWInput.IsPlaystationControllerType(j[i].name, j[i].hardwareIdentifier)) {
            return Options.ControlSetup.Preset.PS4DualShock;
        }
        return RWInput.PlayerControllerType(player, RWInput.PlayerRecentController(player, RWCustom.Custom.rainWorld), RWCustom.Custom.rainWorld);
    }

    static string KeyboardButtonName(KeyCode kc)
    {
        string ret = kc switch {
            KeyCode.Period => ".",
            KeyCode.Comma => ",",
            KeyCode.Slash => "/",
            KeyCode.Backslash => "\\",
            KeyCode.LeftBracket => "[",
            KeyCode.RightBracket => "]",
            KeyCode.Minus => "-",
            KeyCode.Equals => "=",
            KeyCode.Plus => "+",
            KeyCode.BackQuote => "`",
            KeyCode.Semicolon => ";",
            KeyCode.Exclaim => "!",
            KeyCode.Question=> "?",
            KeyCode.Dollar => "$",
            _ => null
        };
        if (ret == null) {
            if (kc.ToString().StartsWith("Left")) {
                return "L" + kc.ToString().Substring(4);
            }
            if (kc.ToString().StartsWith("Right")) {
                return "R" + kc.ToString().Substring(5);
            }
        }
        return ret;
    }

    internal static int KeybindsOfType(int playerNumber, KeyCode keyCode, int stopAt)
    {
        return AllKeybinds(playerNumber).Where(c => c == keyCode).Take(stopAt).Count();
    }
    internal static IEnumerable<KeyCode> AllKeybinds(int playerNumber)
    {
        // Deliberately only process player 0's pause button
        Options.ControlSetup vanillaPlayer0 = RWCustom.Custom.rainWorld.options.controls[0];
        if (vanillaPlayer0.gamePad)
            yield return vanillaPlayer0.gamePadButtons[0];
        else
            yield return vanillaPlayer0.keyboardKeys[0];

        // Start at index 1 to ignore pause button
        Options.ControlSetup vanilla = RWCustom.Custom.rainWorld.options.controls[playerNumber];
        if (vanilla.gamePad) {
            for (int i = 1; i < vanilla.gamePadButtons.Length; i++) {
                yield return vanilla.gamePadButtons[i];
            }
            foreach (var keybind in PlayerKeybind.keybinds) {
                yield return keybind.gamepad[playerNumber];
            }
        }
        else {
            for (int i = 1; i < vanilla.keyboardKeys.Length; i++) {
                yield return vanilla.keyboardKeys[i];
            }
            foreach (var keybind in PlayerKeybind.keybinds) {
                yield return keybind.keyboard[playerNumber];
            }
        }
    }
}
