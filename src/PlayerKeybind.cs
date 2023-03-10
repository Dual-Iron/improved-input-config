using Kittehface.Framework20;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BetterInputConfig;

/// <summary>
/// A simple per-player keybind.
/// </summary>
public sealed class PlayerKeybind
{
    /// <summary>
    /// Registers a new keybind.
    /// </summary>
    /// <param name="id">The unique ID for the keybind.</param>
    /// <param name="mod">The display name of the mod that registered this keybind.</param>
    /// <param name="name">A short name to show in the Input Settings screen.</param>
    /// <param name="keyboardPreset">The default value for keyboards.</param>
    /// <param name="gamepadPreset">The default value for PlayStation, Switch Pro, and other controllers.</param>
    /// <param name="xboxPreset">The default value for Xbox controllers.</param>
    /// <returns>A new <see cref="PlayerKeybind"/> to be used like <c>player.JustPressed(keybind)</c>.</returns>
    /// <exception cref="ArgumentException">The <paramref name="id"/> is invalid or already taken.</exception>
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

    /// <summary>
    /// Gets a keybind given its <paramref name="id"/>.
    /// </summary>
    /// <returns>The keybind, or <see langword="null"/> if none was found.</returns>
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
    
    /// <summary>A unique ID.</summary>
    public string Id { get; }
    /// <summary>The display name of the mod that registered this keybind.</summary>
    public string Mod { get; }
    /// <summary>The display name of the keybind.</summary>
    public string Name { get; }
    /// <summary>The default value for keyboards.</summary>
    public KeyCode KeyboardPreset { get; }
    /// <summary>The default value for PlayStation, Switch Pro, and other controllers.</summary>
    public KeyCode GamepadPreset { get; }
    /// <summary>The default value for Xbox controllers.</summary>
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

    /// <summary>The current keycode configured for the given <paramref name="playerNumber"/> on keyboard.</summary>
    public KeyCode Keyboard(int playerNumber)
    {
        if (playerNumber is < 0 or > 3) {
            throw new ArgumentOutOfRangeException(nameof(playerNumber));
        }
        return keyboard[playerNumber];
    }

    /// <summary>The current keycode configured for the given <paramref name="playerNumber"/> on a controller or gamepad.</summary>
    public KeyCode Gamepad(int playerNumber)
    {
        if (playerNumber is < 0 or > 3) {
            throw new ArgumentOutOfRangeException(nameof(playerNumber));
        }
        return gamepad[playerNumber];
    }

    /// <summary>Checks if the key is currently being pressed by <paramref name="playerNumber"/>.</summary>
    public bool CheckRawPressed(int playerNumber)
    {
        // More or less copypasted from RWInput.PlayerInputPC
        if (playerNumber is < 0 or > 3) {
            throw new ArgumentOutOfRangeException(nameof(playerNumber));
        }

        var rw = RWCustom.Custom.rainWorld;
        var controller = RWInput.PlayerRecentController(playerNumber, rw);
        var controllerType = RWInput.PlayerControllerType(playerNumber, controller, rw);

        bool notMultiplayer = rw.processManager == null || !rw.processManager.IsGameInMultiplayerContext();
        if (!notMultiplayer && controllerType == Options.ControlSetup.Preset.None) {
            return false;
        }

        if (notMultiplayer && controllerType == Options.ControlSetup.Preset.None) {
            controllerType = Options.ControlSetup.Preset.KeyboardSinglePlayer;
        }

        bool gamePad = controllerType != Options.ControlSetup.Preset.KeyboardSinglePlayer;
        if (!gamePad) {
            return Input.GetKey(keyboard[playerNumber]);
        }

        PlayerHandler plrHandler = rw.GetPlayerHandler(playerNumber);
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

        Options.ControlSetup.Preset preset = rw.options.controls[playerNumber].IdentifyGamepadPreset();

        if (preset != Options.ControlSetup.Preset.None) {
            if (controllerType == Options.ControlSetup.Preset.XBox && preset != Options.ControlSetup.Preset.XBox) {
                rw.options.controls[playerNumber].Setup(Options.ControlSetup.Preset.XBox);
            }
            else if (controllerType != Options.ControlSetup.Preset.XBox && preset == Options.ControlSetup.Preset.XBox) {
                rw.options.controls[playerNumber].Setup(Options.ControlSetup.Preset.PS4DualShock);
            }
        }

        return RWInput.ResolveButtonDown(RWInput.ConvertGamepadKeyCode(gamepad[playerNumber]), plr, controller, controllerType);
    }

    /// <summary>
    /// Returns <see cref="Id"/>.
    /// </summary>
    public override string ToString() => Id;
}
