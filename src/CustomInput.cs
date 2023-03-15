using Kittehface.Framework20;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ImprovedInput;

/// <summary>
/// One tick of input for a given player.
/// </summary>
public sealed class CustomInput : IEquatable<CustomInput>
{
    /// <summary>Gets all modded input for the given <paramref name="player"/> at this time.</summary>
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

    bool[] pressed;
    bool[] Pressed {
        get {
            // Updates automatically as keybinds are registered
            if (pressed.Length < PlayerKeybind.keybinds.Count)
                Array.Resize(ref pressed, PlayerKeybind.keybinds.Count);
            return pressed;
        }
    }

    /// <summary>
    /// Gets if any key is pressed.
    /// </summary>
    public bool AnyPressed => pressed.Any(b => b);

    /// <summary>
    /// Gets or sets whether <paramref name="key"/> is active.
    /// </summary>
    /// <returns>True if the key is active.</returns>
    public bool this[PlayerKeybind key] {
        get => Pressed[key.index];
        set => Pressed[key.index] = value;
    }

    /// <summary>
    /// Gets or sets whether any key is active by applying the <paramref name="apply"/> function to all of them.
    /// </summary>
    /// <remarks>This is primarily useful if you want to conditionally enable or disable all inputs, like in vanilla when the player is using their map.</remarks>
    public void Apply(Func<PlayerKeybind, bool> apply)
    {
        for (int i = 0; i < pressed.Length; i++) {
            Pressed[i] = apply(PlayerKeybind.keybinds[i]);
        }
    }

    /// <summary>Deeply copies <see langword="this"/>.</summary>
    public CustomInput Clone()
    {
        CustomInput ret = new();
        pressed.CopyTo(ret.pressed, 0);
        return ret;
    }

    /// <inheritdoc/>
    public override bool Equals(object obj)
    {
        return Equals(obj as CustomInput);
    }

    /// <inheritdoc/>
    public bool Equals(CustomInput other)
    {
        return other is not null && pressed.SequenceEqual(other.pressed);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return 377040109 + EqualityComparer<bool[]>.Default.GetHashCode(pressed);
    }

    /// <inheritdoc/>
    public static bool operator ==(CustomInput left, CustomInput right)
    {
        return left is null && right is null || left?.Equals(right) == true;
    }

    /// <inheritdoc/>
    public static bool operator !=(CustomInput left, CustomInput right)
    {
        return !(left == right);
    }
}
