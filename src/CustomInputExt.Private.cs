using Rewired;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace ImprovedInput;

public static partial class CustomInputExt
{
    internal static KeyCode ActionToKeyCode(int playerNumber, int actionId, bool positive)
    {
        // See RewiredConsts.Action

        int i = playerNumber;
        if (i is < 0 or > 3) throw new ArgumentOutOfRangeException("Invalid player number " + i);
        return actionId switch {
            0 or 8 => PlayerKeybind.Jump.CurrentBinding(i),
            1 or 6 => positive ? PlayerKeybind.Right.CurrentBinding(i) : PlayerKeybind.Left.CurrentBinding(i),
            2 or 7 => positive ? PlayerKeybind.Up.CurrentBinding(i) : PlayerKeybind.Down.CurrentBinding(i),
            3 => PlayerKeybind.Grab.CurrentBinding(i),
            4 or 9 => PlayerKeybind.Throw.CurrentBinding(i),
            5 => PlayerKeybind.Pause.CurrentBinding(i),
            11 => PlayerKeybind.Map.CurrentBinding(i),
            _ => KeyCode.None,
        };
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
        int i = RWCustom.Custom.rainWorld.options.controls[player].gamePadNumber;
        IList<Joystick> j = ReInput.controllers.Joysticks;
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
        return new("???");
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
        if (vanillaPlayer0.controlPreference == Options.ControlSetup.ControlToUse.KEYBOARD)
            yield return PlayerKeybind.Pause.keyboard[0];
        else
            yield return PlayerKeybind.Pause.gamepad[0];

        // Start at index 1 to ignore pause button
        Options.ControlSetup vanilla = RWCustom.Custom.rainWorld.options.controls[playerNumber];
        if (vanilla.controlPreference == Options.ControlSetup.ControlToUse.KEYBOARD) {
            for (int i = 1; i < PlayerKeybind.keybinds.Count; i++) {
                yield return PlayerKeybind.keybinds[i].keyboard[playerNumber];
            }
        }
        else {
            for (int i = 1; i < PlayerKeybind.keybinds.Count; i++) {
                yield return PlayerKeybind.keybinds[i].gamepad[playerNumber];
            }
        }
    }

    internal static string ConvertGamepadKeyCode(KeyCode kc)
    {
        string text = kc.ToString();
        if (text.Contains("Button")) {
            return text.Substring(text.IndexOf("Button"));
        }
        return "Button0";
    }
    internal static bool ResolveButtonDown(string buttonName, Rewired.Player player, Controller controller, Options.ControlSetup.Preset preset)
    {
        // This commented code fails with the warning "The Action {buttonName} does not exist. You can create Actions in the editor."
        // We cannot create actions in the editor. We are modders.
        // :(

        // if (controller.Templates.Count > 0) {
        //     return player.GetButton(buttonName);
        // }

        int num = 0;
        if (buttonName.Contains("Button")) {
            int num2 = buttonName.IndexOf("Button") + "Button".Length;
            num = int.Parse(buttonName.Substring(num2, buttonName.Length - num2), NumberStyles.Any, CultureInfo.InvariantCulture);
        }
        if (preset == Options.ControlSetup.Preset.XBox) {
            if (num == 8) {
                num = 9;
            }
            else if (num == 9) {
                num = 10;
            }
        }
        else if (preset == Options.ControlSetup.Preset.PS4DualShock || preset == Options.ControlSetup.Preset.PS5DualSense) {
            if (num == 0) {
                num = 2;
            }
            else if (num == 1) {
                num = 0;
            }
            else if (num == 2) {
                num = 1;
            }
            else if (num == 8) {
                num = 6;
            }
            else if (num == 9) {
                num = 7;
            }
            else if (num == 13) {
                num = 9;
            }
            else if (num == 12) {
                num = 8;
            }
        }
        else if (preset != Options.ControlSetup.Preset.SwitchProController) {
            return false;
        }
        return controller.GetButton(num);
    }
    internal static float ResolveAxis(bool horizontal, Rewired.Player player, Controller controller, Options.ControlSetup.Preset preset)
    {
        if (controller.Templates.Count > 0) {
            return player.GetAxisRaw(horizontal ? "MoveHorizontal" : "MoveVertical");
        }
        if (controller.type != ControllerType.Joystick || controller is not Joystick joystick) {
            return 0f;
        }
        if (preset == Options.ControlSetup.Preset.XBox) {
            if (horizontal) {
                if (controller.GetButton(12)) {
                    return 1f;
                }
                if (controller.GetButton(14)) {
                    return -1f;
                }
                return joystick.GetAxisRaw(0);
            }
            if (controller.GetButton(11)) {
                return 1f;
            }
            if (controller.GetButton(13)) {
                return -1f;
            }
            return joystick.GetAxisRaw(1);
        }
        else if (preset == Options.ControlSetup.Preset.PS4DualShock || preset == Options.ControlSetup.Preset.PS5DualSense) {
            if (horizontal) {
                if (controller.GetButton(13)) {
                    return 1f;
                }
                if (controller.GetButton(15)) {
                    return -1f;
                }
                return joystick.GetAxisRaw(0);
            }
            if (controller.GetButton(12)) {
                return 1f;
            }
            if (controller.GetButton(14)) {
                return -1f;
            }
            return -joystick.GetAxisRaw(1);
        }
        else if (preset == Options.ControlSetup.Preset.SwitchProController) {
            if (horizontal) {
                if (controller.GetButton(129) || controller.GetButton(130) || controller.GetButton(131)) {
                    return 1f;
                }
                if (controller.GetButton(133) || controller.GetButton(134) || controller.GetButton(135)) {
                    return -1f;
                }
                return joystick.GetAxisRaw(0);
            }
            if (controller.GetButton(129) || controller.GetButton(135) || controller.GetButton(128)) {
                return 1f;
            }
            if (controller.GetButton(131) || controller.GetButton(132) || controller.GetButton(133)) {
                return -1f;
            }
            return -joystick.GetAxisRaw(1);
        }
        return 0f;
    }
}
