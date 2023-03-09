using Kittehface.Framework20;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BetterInputConfig;

public sealed class PlayerKeybind
{
    public static PlayerKeybind Register(string id, string mod, string name, KeyCode keyboardPreset, KeyCode gamepadPreset, KeyCode xboxPreset = KeyCode.None)
    {
        if (id.Contains("<optA>") || id.Contains("<optB>")) {
            throw new ArgumentException($"The id {id} is invalid.");
        }
        if (keybinds.Any(k => k.Id == id)) {
            throw new ArgumentException($"A keybind with the id {id} has already been registered.");
        }
        keybinds.Add(new(id, mod, name, keyboardPreset, gamepadPreset, xboxPreset == KeyCode.None ? gamepadPreset : xboxPreset) { index = keybinds.Count });
        return keybinds.Last();
    }

    public static PlayerKeybind Get(string id) => keybinds.FirstOrDefault(k => k.Id == id);

    internal PlayerKeybind(string id, string mod, string name, KeyCode keyboardDefault, KeyCode gamepadDefault, KeyCode xboxDefault)
    {
        Id = id;
        Mod = mod;
        Name = name;
        KeyboardPreset = keyboardDefault;
        GamepadPreset = gamepadDefault;
        XboxPreset = xboxDefault;
        keyboard = new[] { keyboardDefault, keyboardDefault, keyboardDefault, keyboardDefault };
        gamepad = new[] { gamepadDefault, gamepadDefault, gamepadDefault, gamepadDefault };
    }

    internal static readonly List<PlayerKeybind> keybinds = new();
    internal int index = -1;

    public string Id { get; }
    public string Mod { get; }
    public string Name { get; }
    public KeyCode KeyboardPreset { get; }
    public KeyCode GamepadPreset { get; }
    public KeyCode XboxPreset { get; }

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

    public override string ToString() => Id;
}
