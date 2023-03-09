using Kittehface.Framework20;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BetterInputConfig;

public sealed class CustomInput : IEquatable<CustomInput>
{
    public static CustomInput GetRawInput(int player)
    {
        // More or less copypasted from RWInput.PlayerInputPC
        // See PlayerKeybind.cs
        if (player is < 0 or > 3) {
            throw new ArgumentOutOfRangeException(nameof(player));
        }

        CustomInput ret = new();

        var rw = RWCustom.Custom.rainWorld;
        var controller = RWInput.PlayerRecentController(player, rw);
        var controllerType = RWInput.PlayerControllerType(player, controller, rw);

        bool notMultiplayer = rw.processManager == null || !rw.processManager.IsGameInMultiplayerContext();
        if (!notMultiplayer && controllerType == Options.ControlSetup.Preset.None) {
            return ret;
        }

        if (notMultiplayer && controllerType == Options.ControlSetup.Preset.None) {
            controllerType = Options.ControlSetup.Preset.KeyboardSinglePlayer;
        }

        bool gamePad = controllerType != Options.ControlSetup.Preset.KeyboardSinglePlayer;
        if (!gamePad) {
            foreach (var key in PlayerKeybind.keybinds) {
                ret.pressed[key.index] = Input.GetKey(key.keyboard[player]);
            }
            return ret;
        }

        PlayerHandler plrHandler = rw.GetPlayerHandler(player);
        if (plrHandler == null) {
            return ret;
        }

        Profiles.Profile profile = plrHandler.profile;
        if (profile == null) {
            return ret;
        }

        Rewired.Player plr = UserInput.GetRewiredPlayer(profile, plrHandler.playerIndex);
        if (plr == null) {
            return ret;
        }

        Options.ControlSetup.Preset preset = rw.options.controls[player].IdentifyGamepadPreset();

        if (preset != Options.ControlSetup.Preset.None) {
            if (controllerType == Options.ControlSetup.Preset.XBox && preset != Options.ControlSetup.Preset.XBox) {
                rw.options.controls[player].Setup(Options.ControlSetup.Preset.XBox);
            }
            else if (controllerType != Options.ControlSetup.Preset.XBox && preset == Options.ControlSetup.Preset.XBox) {
                rw.options.controls[player].Setup(Options.ControlSetup.Preset.PS4DualShock);
            }
        }

        foreach (var key in PlayerKeybind.keybinds) {
            ret.pressed[key.index] = RWInput.ResolveButtonDown(RWInput.ConvertGamepadKeyCode(key.gamepad[player]), plr, controller, controllerType);
        }
        return ret;
    }

    internal CustomInput()
    {
        pressed = new bool[PlayerKeybind.keybinds.Count];
    }

    readonly bool[] pressed;

    public bool this[PlayerKeybind key] {
        get => pressed[key.index];
        set => pressed[key.index] = value;
    }

    public void UpdateAll(Func<PlayerKeybind, bool> set)
    {
        for (int i = 0; i < pressed.Length; i++) {
            pressed[i] = set(PlayerKeybind.keybinds[i]);
        }
    }

    public CustomInput Clone()
    {
        CustomInput ret = new();
        pressed.CopyTo(ret.pressed, 0);
        return ret;
    }

    public override bool Equals(object obj)
    {
        return Equals(obj as CustomInput);
    }

    public bool Equals(CustomInput other)
    {
        return other is not null && pressed.SequenceEqual(other.pressed);
    }

    public override int GetHashCode()
    {
        return 377040109 + EqualityComparer<bool[]>.Default.GetHashCode(pressed);
    }

    public static bool operator ==(CustomInput left, CustomInput right)
    {
        return left is null && right is null || left?.Equals(right) == true;
    }

    public static bool operator !=(CustomInput left, CustomInput right)
    {
        return !(left == right);
    }
}
