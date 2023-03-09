using Kittehface.Framework20;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BetterInputConfig;

public sealed class PlayerKeybind
{
    public static PlayerKeybind Register(string mod, string name, KeyCode defaultKeyboard, KeyCode defaultGamepad)
    {
        if (keybinds.Any(k => k.Mod == mod && k.Name == name)) {
            throw new ArgumentException($"An existing keybind with the same mod ({mod}) and name ({name}) has already been registered.");
        }
        keybinds.Add(new(mod, name, defaultKeyboard, defaultGamepad) { id = keybinds.Count });
        return keybinds.Last();
    }

    internal PlayerKeybind(string mod, string name, KeyCode keyboardDefault, KeyCode gamepadDefault)
    {
        Mod = mod;
        Name = name;
        KeyboardDefault = keyboardDefault;
        GamepadDefault = gamepadDefault;
        keyboard = new[] { keyboardDefault, keyboardDefault, keyboardDefault, keyboardDefault };
        gamepad = new[] { gamepadDefault, gamepadDefault, gamepadDefault, gamepadDefault };
    }

    internal static readonly List<PlayerKeybind> keybinds = new();
    internal int id = -1;

    public string Mod { get; }
    public string Name { get; }
    public KeyCode KeyboardDefault { get; }
    public KeyCode GamepadDefault { get; }

    /// <summary>
    /// If true, using the map suppresses the keybind.
    /// </summary>
    public bool MapSuppressed { get; set; } = true;
    /// <summary>
    /// If true, sleeping suppresses the keybind.
    /// </summary>
    public bool SleepSuppressed { get; set; } = true;

    internal readonly KeyCode[] keyboard;
    internal readonly KeyCode[] gamepad;

    public KeyCode Keyboard(int player)
    {
        if (player is < 0 or > 3) {
            throw new ArgumentOutOfRangeException(nameof(player));
        }
        return keyboard[player];
    }

    public KeyCode Gamepad(int player)
    {
        if (player is < 0 or > 3) {
            throw new ArgumentOutOfRangeException(nameof(player));
        }
        return gamepad[player];
    }

    public bool CheckRawPressed(int player)
    {
        // More or less copypasted from RWInput.PlayerInputPC
        if (player is < 0 or > 3) {
            throw new ArgumentOutOfRangeException(nameof(player));
        }

        var rw = RWCustom.Custom.rainWorld;
        var controller = RWInput.PlayerRecentController(player, rw);
        var controllerType = RWInput.PlayerControllerType(player, controller, rw);

        bool notMultiplayer = rw.processManager == null || !rw.processManager.IsGameInMultiplayerContext();
        if (!notMultiplayer && controllerType == Options.ControlSetup.Preset.None) {
            return false;
        }

        if (notMultiplayer && controllerType == Options.ControlSetup.Preset.None) {
            controllerType = Options.ControlSetup.Preset.KeyboardSinglePlayer;
        }

        bool gamePad = controllerType != Options.ControlSetup.Preset.KeyboardSinglePlayer;
        if (!gamePad) {
            return Input.GetKey(keyboard[player]);
        }

        PlayerHandler plrHandler = rw.GetPlayerHandler(player);
        if (plrHandler == null) {
            return false;
        }

        Profiles.Profile profile = plrHandler.profile;
        if (profile == null) {
            return false;
        }

        Rewired.Player plr = UserInput.GetRewiredPlayer(profile, plrHandler.playerIndex);
        if (plr == null) {
            return false;
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

        return RWInput.ResolveButtonDown(RWInput.ConvertGamepadKeyCode(gamepad[player]), plr, controller, controllerType);
    }
}
