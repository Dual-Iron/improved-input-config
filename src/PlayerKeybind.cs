using Kittehface.Framework20;
using Rewired;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace ImprovedInput;

/// <summary>
/// A simple per-player keybind.
/// </summary>
public sealed class PlayerKeybind
{
    internal static readonly List<PlayerKeybind> keybinds = new();

    // Don't move these. The indices matter.
    internal static readonly PlayerKeybind Pause = Register("vanilla:pause", "Vanilla", "Pause", KeyCode.Escape, KeyCode.JoystickButton9, KeyCode.JoystickButton6);

    internal static readonly PlayerKeybind Map = Register("vanilla:map", "Vanilla", "Map", KeyCode.Space, KeyCode.JoystickButton5, KeyCode.JoystickButton5);
    internal static readonly PlayerKeybind Grab = Register("vanilla:grab", "Vanilla", "Grab", KeyCode.LeftShift, KeyCode.JoystickButton0, KeyCode.JoystickButton2);
    internal static readonly PlayerKeybind Jump = Register("vanilla:jump", "Vanilla", "Jump", KeyCode.Z, KeyCode.JoystickButton1, KeyCode.JoystickButton0);
    internal static readonly PlayerKeybind Throw = Register("vanilla:throw", "Vanilla", "Throw", KeyCode.X, KeyCode.JoystickButton2, KeyCode.JoystickButton1);

    internal static readonly PlayerKeybind Up = Register("vanilla:up", "Vanilla", "Up", KeyCode.UpArrow, KeyCode.None);
    internal static readonly PlayerKeybind Left = Register("vanilla:left", "Vanilla", "Left", KeyCode.LeftArrow, KeyCode.None);
    internal static readonly PlayerKeybind Down = Register("vanilla:down", "Vanilla", "Down", KeyCode.DownArrow, KeyCode.None);
    internal static readonly PlayerKeybind Right = Register("vanilla:right", "Vanilla", "Right", KeyCode.RightArrow, KeyCode.None);

    /// <summary>
    /// Registers a new keybind.
    /// </summary>
    /// <param name="id">The unique ID for the keybind.</param>
    /// <param name="mod">The display name of the mod that registered this keybind.</param>
    /// <param name="name">A short name to show in the Input Settings screen.</param>
    /// <param name="keyboardPreset">The default value for keyboards.</param>
    /// <param name="gamepadPreset">The default value for controllers.</param>
    /// <returns>A new <see cref="PlayerKeybind"/> to be used like <c>player.JustPressed(keybind)</c>.</returns>
    /// <exception cref="ArgumentException">The <paramref name="id"/> is invalid or already taken.</exception>
    public static PlayerKeybind Register(string id, string mod, string name, KeyCode keyboardPreset, KeyCode gamepadPreset)
    {
        return Register(id, mod, name, keyboardPreset, gamepadPreset, gamepadPreset);
    }

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
    public static PlayerKeybind Register(string id, string mod, string name, KeyCode keyboardPreset, KeyCode gamepadPreset, KeyCode xboxPreset)
    {
        if (id.Contains("<optA>") || id.Contains("<optB>")) {
            Debug.Log($"[ERROR] The keybind id {id} is invalid.");
            throw new ArgumentException($"The keybind id {id} is invalid.");
        }
        if (keybinds.Any(k => k.Id == id)) {
            Debug.Log($"[ERROR] A keybind with the id {id} has already been registered.");
            throw new ArgumentException($"A keybind with the id {id} has already been registered.");
        }
        keybinds.Add(new(id, mod, name, keyboardPreset, gamepadPreset, xboxPreset == KeyCode.None ? gamepadPreset : xboxPreset) {
            index = keybinds.Count
        });
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

    /// <summary>True if the binding for <paramref name="playerNumber"/> is not set.</summary>
    public bool Unbound(int playerNumber) => CurrentBinding(playerNumber) == KeyCode.None;

    /// <summary>The current keycode configured for the given <paramref name="playerNumber"/> on keyboard.</summary>
    public KeyCode Keyboard(int playerNumber)
    {
        if (playerNumber is < 0 or > 3) {
            throw new ArgumentOutOfRangeException(nameof(playerNumber));
        }
        return keyboard[playerNumber];
    }

    /// <summary>The current keycode configured for the given <paramref name="playerNumber"/> on a controller.</summary>
    public KeyCode Gamepad(int playerNumber)
    {
        if (playerNumber is < 0 or > 3) {
            throw new ArgumentOutOfRangeException(nameof(playerNumber));
        }
        return gamepad[playerNumber];
    }

    /// <summary>The current recognized keycode for the given <paramref name="playerNumber"/>.</summary>
    public KeyCode CurrentBinding(int playerNumber)
    {
        if (playerNumber is < 0 or > 3) {
            throw new ArgumentOutOfRangeException(nameof(playerNumber));
        }
        if (CustomInputExt.UsingGamepad(playerNumber)) {
            return gamepad[playerNumber];
        }
        return keyboard[playerNumber];
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
        rw.options.controls[playerNumber].UpdateActiveController(controller, false);
        var controllerType = rw.options.controls[playerNumber].GetActivePreset();

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

        return CustomInputExt.ResolveButtonDown(CustomInputExt.ConvertGamepadKeyCode(gamepad[playerNumber]), plr, controller, controllerType);
    }

    /// <summary>
    /// Returns <see cref="Id"/>.
    /// </summary>
    public override string ToString() => Id;
}
