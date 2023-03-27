using BepInEx;
using Menu;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using Rewired;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Permissions;
using System.Text.RegularExpressions;
using UnityEngine;

// Allows access to private members
#pragma warning disable CS0618
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618

namespace ImprovedInput;

[BepInPlugin("com.dual.improved-input-config", "Improved Input Config", "1.1.0")]
sealed class Plugin : BaseUnityPlugin
{
    internal sealed class PlayerData
    {
        public PlayerData()
        {
            for (int i = 0; i < input.Length; i++) input[i] = new();
            for (int i = 0; i < input.Length; i++) rawInput[i] = new();
        }

        public readonly CustomInput[] input = new CustomInput[CustomInputExt.HistoryLength];
        public readonly CustomInput[] rawInput = new CustomInput[CustomInputExt.HistoryLength];
    }

    internal static readonly ConditionalWeakTable<Player, PlayerData> players = new();

    public static new BepInEx.Logging.ManualLogSource Logger;

    public void OnEnable()
    {
        // TODO Add `Any` controller option:
        // - use a checkbox on input screen to configure Gamepad or Keyboard input
        // - iterate controllers in getButton and getAxisRaw, OR them all.

        Logger = base.Logger;

        // Reverting vanilla input to 1.9.06 system
        On.Options.ControlSetup.KeyCodeFromAction += KeyCodeFromAction;
        new Hook(typeof(Rewired.Player).GetMethod("GetButton", new Type[] { typeof(int) }), getButton);
        new Hook(typeof(Rewired.Player).GetMethod("GetAxisRaw", new Type[] { typeof(int) }), getAxisRaw);

        // Updating custom inputs (basic API yaaay)
        On.Player.checkInput += UpdateInput;
        On.Player.UpdateMSC += UpdateNoInputCounter;

        // Presets
        On.Menu.InputOptionsMenu.Singal += Presets;

        // Input Settings screen ui
        IL.Menu.InputOptionsMenu.ctor += AddCustomButtonsIL;
        On.Menu.InputOptionsMenu.ctor += FixVanillaButtons;
        On.Menu.InputOptionsMenu.Update += InputOptionsMenu_Update;
        On.Menu.InputOptionsMenu.SetCurrentlySelectedOfSeries += FixSelection;
        On.Menu.InputOptionsMenu.UpdateInfoText += InputOptionsMenu_UpdateInfoText;

        // Input testing
        On.Menu.InputTesterHolder.InputTester.ctor += InputTester_ctor;
        On.Menu.InputTesterHolder.InputTester.Update += InputTester_Update;
        On.Menu.InputTesterHolder.InputTester.UpdateTestButtons += InputTester_UpdateTestButtons;
        On.Menu.InputTesterHolder.Back.Update += Back_Update;

        // Saving
        On.Options.ApplyOption += Options_ApplyOption;
        On.Options.ToString += Options_ToString;
    }

    private KeyCode KeyCodeFromAction(On.Options.ControlSetup.orig_KeyCodeFromAction orig, Options.ControlSetup self, int actionID, int categoryID, bool axisPositive)
    {
        if (self.recentController == null || self.recentController.type != ControllerType.Keyboard) {
            return KeyCode.None;
        }

        // See RewiredConsts.Action
        int i = self.index;
        if (i is < 0 or > 3) throw new InvalidOperationException("Invalid ControlSetup index " + i);
        return actionID switch {
            0 => PlayerKeybind.Jump.keyboard[i],
            1 => axisPositive ? PlayerKeybind.Right.keyboard[i] : PlayerKeybind.Left.keyboard[i],
            2 => axisPositive ? PlayerKeybind.Up.keyboard[i] : PlayerKeybind.Down.keyboard[i],
            3 => PlayerKeybind.Grab.keyboard[i],
            4 => PlayerKeybind.Throw.keyboard[i],
            5 => PlayerKeybind.Pause.keyboard[i],
            11 => PlayerKeybind.Map.keyboard[i],
            _ => KeyCode.None,
        };
    }

    // Cursed shit copied from 1.9.06 input code. Overwrites ReWired's Player.GetButton and Player.GetAxisRaw functions.

    private static Controller EnsureController(Options.ControlSetup setup)
    {
        Controller ctrl = RWInput.PlayerRecentController(setup.index, RWCustom.Custom.rainWorld);
        if (ctrl != null) {
            return ctrl;
        }
        setup.UpdateControlPreference(setup.controlPreference, true);
        return RWInput.PlayerRecentController(setup.index, RWCustom.Custom.rainWorld);
    }

    private static readonly Func<Func<Rewired.Player, int, bool>, Rewired.Player, int, bool> getButton = (orig, player, actionId) => {
        Options.ControlSetup setup = RWCustom.Custom.rainWorld.options.controls[player.id];
        Options.ControlSetup.Preset ty = setup.GetActivePreset();
        bool gamePad = ty != Options.ControlSetup.Preset.KeyboardSinglePlayer && ty != Options.ControlSetup.Preset.None;
        if (!gamePad) {
            return Input.GetKey(CustomInputExt.ActionToKeyCode(player.id, actionId, true));
        }
        KeyCode keyCode = CustomInputExt.ActionToKeyCode(player.id, actionId, true);
        Controller ctrl = EnsureController(setup);
        return CustomInputExt.ResolveButtonDown(keyCode, ctrl, ty);
    };

    private static readonly Func<Func<Rewired.Player, int, float>, Rewired.Player, int, float> getAxisRaw = (orig, player, actionId) => {
        Options.ControlSetup setup = RWCustom.Custom.rainWorld.options.controls[player.id];
        Options.ControlSetup.Preset ty = setup.GetActivePreset();
        bool gamePad = ty != Options.ControlSetup.Preset.KeyboardSinglePlayer && ty != Options.ControlSetup.Preset.None;
        if (!gamePad) {
            // Checking axis for keyboard involves just checking if right/left is pressed
            bool neg = Input.GetKey(CustomInputExt.ActionToKeyCode(player.id, actionId, false));
            bool pos = Input.GetKey(CustomInputExt.ActionToKeyCode(player.id, actionId, true));
            if (neg && !pos) return -1;
            if (pos && !neg) return 1;
            return 0;
        }
        Controller ctrl = EnsureController(setup);
        return CustomInputExt.ResolveAxis(actionId is 1 or 6, player, ctrl, ty);
    };

    private void UpdateInput(On.Player.orig_checkInput orig, Player self)
    {
        CustomInputExt.historyLocked = true;

        PlayerData data = players.GetValue(self, _ => new());

        // Age input.
        for (int i = data.input.Length - 1; i > 0; i--) {
            data.input[i] = data.input[i - 1];
        }
        for (int i = data.rawInput.Length - 1; i > 0; i--) {
            data.rawInput[i] = data.rawInput[i - 1];
        }

        // Get local player number so we can set inputs using it later.
        int playerNumber = self.playerState.playerNumber;
        if (ModManager.MSC && self.abstractCreature.world.game.IsArenaSession && self.abstractCreature.world.game.GetArenaGameSession.chMeta != null) {
            playerNumber = 0;
        }
        if (playerNumber is < 0 or > 3) {
            orig(self);
            return;
        }

        // Assign inputs!
        data.rawInput[0] = CustomInput.GetRawInput(playerNumber);
        if (self.stun == 0 && !self.dead && self.controller == null && self.AI == null) {
            data.input[0] = data.rawInput[0].Clone();
        }
        else {
            data.input[0] = new();
        }

        // Suppress input if we're pressing MAP or currently sleeping.
        data.input[0].Apply(key => {
            bool mapSuppressed = self.standStillOnMapButton && self.input[0].mp && (!ModManager.CoopAvailable || !self.jollyButtonDown);
            bool sleepSuppressed = self.Sleeping;
            if (key.MapSuppressed && mapSuppressed || key.SleepSuppressed && self.Sleeping) {
                return false;
            }
            return data.input[0][key];
        });

        orig(self);
    }

    private void UpdateNoInputCounter(On.Player.orig_UpdateMSC orig, Player self)
    {
        PlayerData data = players.GetValue(self, _ => new());
        if (data.input[0].AnyPressed) {
            self.touchedNoInputCounter = 0;
        }
        orig(self);
    }

    private void Presets(On.Menu.InputOptionsMenu.orig_Singal orig, InputOptionsMenu self, MenuObject sender, string message)
    {
        orig(self, sender, message);

        if (message == "BIC CUSTOM PRESET") {
            var playerNumber = self.CurrentControlSetup.index;
            var preset = self.manager.rainWorld.options.controls[playerNumber].GetActivePreset();

            if (preset == Options.ControlSetup.Preset.KeyboardSinglePlayer) {
                foreach (var kb in PlayerKeybind.keybinds) {
                    kb.keyboard[playerNumber] = kb.KeyboardPreset;
                }
            }
            else if (preset == Options.ControlSetup.Preset.PS4DualShock || preset == Options.ControlSetup.Preset.PS5DualSense || preset == Options.ControlSetup.Preset.SwitchProController) {
                foreach (var kb in PlayerKeybind.keybinds) {
                    kb.gamepad[playerNumber] = kb.GamepadPreset;
                }
            }
            else if (preset == Options.ControlSetup.Preset.XBox) {
                foreach (var kb in PlayerKeybind.keybinds) {
                    kb.gamepad[playerNumber] = kb.XboxPreset;
                }
            }

            foreach (var sub in self.pages[0].subObjects) {
                if (sub is InputSelectButton s
                    && (!s.IndependentOfPlayer || self.CurrentControlSetup.index == 0)
                    && (s.MovementKey || !s.Gamepad) == (preset == Options.ControlSetup.Preset.KeyboardSinglePlayer)) {
                    s.Flash();
                }
            }

            self.PlaySound(SoundID.MENU_Button_Successfully_Assigned);
        }
    }

    private void AddCustomButtonsIL(ILContext il)
    {
        ILCursor cursor = new(il);

        cursor.GotoNext(i => i.MatchNewobj<InputTesterHolder>());
        cursor.Emit(OpCodes.Ldarg_0);
        cursor.EmitDelegate(AddCustomButtons);
    }

    private void AddCustomButtons(InputOptionsMenu self)
    {
        // --- Keybind buttons ---

        int columns = 1 + Mathf.CeilToInt((PlayerKeybind.keybinds.Count - 9) / 10f); // 10 per row
        if (columns > 4) {
            throw new InvalidOperationException("How are there possibly more than 30 modded keybinds at one time?");
        }

        var s = self.pages[0].subObjects;
        var c = columns > 1; // compact mode
        var columnWidth = c ? 120 : 200;
        var o = columns == 1
            ? new Vector2(960, 642)
            : new Vector2(columns > 2 ? 1136 : 1024, 642);
        var y = 0f;

        // Start at 9, after all vanilla keybinds
        for (int i = 9; i < PlayerKeybind.keybinds.Count; i++) {
            PlayerKeybind keybind = PlayerKeybind.keybinds[i];
            s.Add(new InputSelectButton(self.pages[0], keybind, c, new Vector2(o.x, o.y - y)));
            y += 40;
            if (y >= 40 * 10) {
                y = 0;
                o.x -= columnWidth;
            }
        }

        if (y != 0) {
            y = 0;
            o.x -= columnWidth;
        }

        MenuLabel GroupLabel(string text, Vector2 pos)
        {
            MenuLabel label = new(self, self.pages[0], text, pos + new Vector2(c ? 15 : 0, 0), Vector2.zero, false);
            label.label.anchorX = c ? 1f : 0.5f;
            label.label.anchorY = 1;
            label.label.color = Menu.Menu.MenuRGB(Menu.Menu.MenuColors.MediumGrey);
            return label;
        }

        // Add vanilla buttons
        s.Add(GroupLabel("PLAYER ONE", o + new Vector2(15, 30)));
        s.Add(new InputSelectButton(self.pages[0], PlayerKeybind.Pause, c, o - new Vector2(0, y += 20)));
        
        s.Add(GroupLabel("MOVEMENT", o - new Vector2(0, y += 45) + new Vector2(15, 30)));
        s.Add(new InputSelectButton(self.pages[0], PlayerKeybind.Up, c, o - new Vector2(0, y += 20)));
        s.Add(new InputSelectButton(self.pages[0], PlayerKeybind.Left, c, o - new Vector2(0, y += 40)));
        s.Add(new InputSelectButton(self.pages[0], PlayerKeybind.Down, c, o - new Vector2(0, y += 40)));
        s.Add(new InputSelectButton(self.pages[0], PlayerKeybind.Right, c, o - new Vector2(0, y += 40)));

        s.Add(GroupLabel("VANILLA", o - new Vector2(0, y += 45) + new Vector2(15, 30)));
        s.Add(new InputSelectButton(self.pages[0], PlayerKeybind.Grab, c, o - new Vector2(0, y += 20)));
        s.Add(new InputSelectButton(self.pages[0], PlayerKeybind.Jump, c, o - new Vector2(0, y += 40)));
        s.Add(new InputSelectButton(self.pages[0], PlayerKeybind.Throw, c, o - new Vector2(0, y += 40)));
        s.Add(new InputSelectButton(self.pages[0], PlayerKeybind.Map, c, o - new Vector2(0, y += 40)));

        // --- Preset button ---
        self.pages[0].subObjects.Add(new SimpleButton(self, self.pages[0], self.Translate("PRESET"), "BIC CUSTOM PRESET", new(self.testButton.pos.x, 140), new(110, 30)));
    }

    private void FixVanillaButtons(On.Menu.InputOptionsMenu.orig_ctor orig, InputOptionsMenu self, ProcessManager manager)
    {
        orig(self, manager);

        foreach (var setup in manager.rainWorld.options.controls) {
            if (setup.controlPreference == Options.ControlSetup.ControlToUse.ANY) {
                setup.UpdateControlPreference(Options.ControlSetup.ControlToUse.KEYBOARD, false);
            }
        }

        // Remove old buttons
        string keyboard = self.Translate("KEYBOARD");
        string gamepad = self.Translate("GAMEPAD");

        for (int i = self.pages[0].subObjects.Count - 1; i >= 0; i--) {
            MenuObject sub = self.pages[0].subObjects[i];

            if (sub == self.deviceButtons[0]
                || sub == self.keyboardDefaultsButton
                || sub == self.gamepadDefaultsButton
                || sub is InputOptionsMenu.InputSelectButton
                || sub is MenuLabel label && (label.text == keyboard || label.text == gamepad || self.inputLabels.Contains(label))) {
                self.pages[0].RemoveSubObject(sub);
                sub.RemoveSprites();
            }
        }

        // Prevent self.gamepadDefaultsButton from being selected
        self.xInvCheck.nextSelectable[1] = null;

        // Move keyboard button up
        self.deviceButtons[1].pos.y += 90;
        self.deviceButtons[1].lastPos.y += 90;

        // Update player arrow positions after moving keyboard button
        foreach (InputOptionsMenu.PlayerButton plrButton in self.playerButtons) {
            plrButton.pointPos = plrButton.IdealPointHeight();
            plrButton.lastPointPos = plrButton.pointPos;
        }

        self.keyBoardKeysButtons = new InputOptionsMenu.InputSelectButton[0];
        self.gamePadButtonButtons = new InputOptionsMenu.InputSelectButton[0];

        // Remove device side-labels
        foreach (var btn in self.deviceButtons) {
            btn.displayName = "";
            btn.menuLabel.text = "";
        }

        // Move "invert x/y" checkboxes
        int found = 0;
        foreach (CheckBox checkBox in self.pages[0].subObjects.OfType<CheckBox>()) {
            if (checkBox.IDString == "XINV") {
                checkBox.pos = new Vector2(450, 50);
                found++;
            }
            else if (checkBox.IDString == "YINV") {
                checkBox.pos = new Vector2(450, 80);
                found++;
            }
            if (found == 2) break;
        }
    }

    bool lastAnyKey = false;
    bool anyKey = false;
    private void InputOptionsMenu_Update(On.Menu.InputOptionsMenu.orig_Update orig, InputOptionsMenu self)
    {
        orig(self);

        Controller ctrl = EnsureController(self.CurrentControlSetup);

        lastAnyKey = anyKey;
        anyKey = Input.anyKey || (ctrl?.GetAnyButton() ?? false);

        if (self.settingInput == null || self.selectedObject is not InputSelectButton button) {
            return;
        }

        self.freezeMenuFunctionsCounter++;

        if (lastAnyKey || !anyKey) {
            return;
        }

        foreach (KeyCode keyCode in Enum.GetValues(typeof(KeyCode)).OfType<KeyCode>()) {
            if (Input.GetKey(keyCode) || CustomInputExt.ResolveButtonDown(keyCode, ctrl, self.CurrentControlSetup.GetActivePreset())) {
                button.InputAssigned(keyCode);

                self.settingInput = null;

                if (self.mouseModeBeforeAssigningInput) {
                    self.forceMouseMode = 10;
                    self.mouseModeBeforeAssigningInput = false;
                }

                return;
            }
        }

        Logger.LogDebug("No known way to track input!");
    }

    private void FixSelection(On.Menu.InputOptionsMenu.orig_SetCurrentlySelectedOfSeries orig, InputOptionsMenu self, string series, int to)
    {
        orig(self, series, to);

        if (series == "DeviceButtons" && to != 1) {
            self.CurrentControlSetup.gamePadGuid = null;
            self.CurrentControlSetup.gamePadNumber = to - 2;
            self.CurrentControlSetup.UpdateControlPreference(Options.ControlSetup.ControlToUse.SPECIFIC_GAMEPAD, false);
        }
    }

    private string InputOptionsMenu_UpdateInfoText(On.Menu.InputOptionsMenu.orig_UpdateInfoText orig, InputOptionsMenu self)
    {
        if (self.selectedObject is SimpleButton button && button.signalText == "BIC CUSTOM PRESET") {
            return Regex.Replace(self.Translate("Assign player <X> to the default controls"), "<X>", (self.manager.rainWorld.options.playerToSetInputFor + 1).ToString());
        }
        return self.selectedObject is InputSelectButton t ? t.HoverText() : orig(self);
    }

    private void InputTester_ctor(On.Menu.InputTesterHolder.InputTester.orig_ctor orig, InputTesterHolder.InputTester self, Menu.Menu menu, MenuObject owner, int playerIndex)
    {
        orig(self, menu, owner, playerIndex);

        // Remove buttons. Leave the first four ( the arrow keys ).
        foreach (var testButton in self.testButtons) {
            if (testButton.buttonIndex is 5 or 6 or 7 or 8) continue;

            self.RemoveSubObject(testButton);
            testButton.RemoveSprites();
        }

        // Added keybinds
        float x = 120 + (menu.CurrLang == InGameTranslator.LanguageID.French || menu.CurrLang == InGameTranslator.LanguageID.German ? 30 : 0);
        int row = 0;
        int btn = 4;
        Array.Resize(ref self.testButtons, 4 - 5 + PlayerKeybind.keybinds.Count);
        foreach (PlayerKeybind keybind in PlayerKeybind.keybinds) {
            if (keybind.index is 0 or 5 or 6 or 7 or 8) {
                continue;
            }

            self.subObjects.Add(self.testButtons[btn++] = new(menu, self, new Vector2(x, 45 - row * 30), null, 0, menu.Translate(keybind.Name), keybind.index, playerIndex));

            row += 1;
            if (row > 3) {
                row = 0;
                x += 200;
            }
        }
    }

    private void InputTester_Update(On.Menu.InputTesterHolder.InputTester.orig_Update orig, InputTesterHolder.InputTester self)
    {
        orig(self);

        foreach (var btn in self.testButtons) {
            if (btn.buttonIndex is not 5 and not 6 and not 7 and not 8) {
                btn.pressed = PlayerKeybind.keybinds[btn.buttonIndex].CheckRawPressed(self.playerIndex);
            }
            btn.playerAssignedToAnything = self.playerAssignedToAnything;
        }
    }

    private void InputTester_UpdateTestButtons(On.Menu.InputTesterHolder.InputTester.orig_UpdateTestButtons orig, InputTesterHolder.InputTester self)
    {
        foreach (var btn in self.testButtons) {
            if (btn.menuLabel != null) {
                Options.ControlSetup setup = self.menu.manager.rainWorld.options.controls[btn.playerIndex];
                KeyCode keyCode = setup.UsingGamepad() ? PlayerKeybind.keybinds[btn.buttonIndex].gamepad[btn.playerIndex] : PlayerKeybind.keybinds[btn.buttonIndex].keyboard[btn.playerIndex];
                btn.menuLabel.text = $"{btn.labelText} ( {CustomInputExt.ButtonText(self.playerIndex, keyCode, out _)} )";
            }
        }
    }

    private void Back_Update(On.Menu.InputTesterHolder.Back.orig_Update orig, InputTesterHolder.Back self)
    {
        orig(self);
        if (self.holder.active)
            self.holdButton.held = RWInput.CheckPauseButton(0, self.menu.manager.rainWorld);
    }

    private bool Options_ApplyOption(On.Options.orig_ApplyOption orig, Options self, string[] split)
    {
        // Return TRUE if invalid or unrecognized data
        bool unrecognized = orig(self, split);
        if (!unrecognized) {
            return false;
        }
        string key = split[0];
        if (key == "iic:keybind" && split.Length > 3) {
            string id = split[1];
            string[] keyboard = split[2].Split(',');
            string[] gamepad = split[3].Split(',');

            if (keyboard.Length < 4 || gamepad.Length < 4) return true;

            PlayerKeybind keybind = PlayerKeybind.keybinds.FirstOrDefault(k => k.Id == id);
            if (keybind == null) {
                Logger.LogWarning($"Unregistered keybind {id} in save file");
                return true;
            }

            for (int i = 0; i < 4; i++) {
                if (Enum.TryParse(keyboard[i], out KeyCode k)) keybind.keyboard[i] = k;
                if (Enum.TryParse(gamepad[i], out KeyCode k2)) keybind.gamepad[i] = k2;
            }
            return false;
        }
        return true;
    }

    private string Options_ToString(On.Options.orig_ToString orig, Options self)
    {
        string ret = orig(self);
        foreach (PlayerKeybind k in PlayerKeybind.keybinds) {
            ret += $"iic:keybind<optB>{k.Id}<optB>{k.keyboard[0]},{k.keyboard[1]},{k.keyboard[2]},{k.keyboard[3]}<optB>{k.gamepad[0]},{k.gamepad[1]},{k.gamepad[2]},{k.gamepad[3]}<optA>";
        }
        return ret;
    }
}
