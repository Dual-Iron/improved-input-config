using BepInEx;
using Menu;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Permissions;
using UnityEngine;

// Allows access to private members
#pragma warning disable CS0618
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618

namespace BetterInputConfig;

[BepInPlugin("com.dual.better-input-config", "Better Input Config", "1.0.0")]
sealed class Plugin : BaseUnityPlugin
{
    internal sealed class PlayerData
    {
        public PlayerData()
        {
            for (int i = 0; i < input.Length; i++) input[i] = new();
            for (int i = 0; i < input.Length; i++) rawInput[i] = new();
        }

        public readonly CustomInput[] input = new CustomInput[InputExtensions.HistoryLength];
        public readonly CustomInput[] rawInput = new CustomInput[InputExtensions.HistoryLength];
    }

    internal static readonly ConditionalWeakTable<Player, PlayerData> players = new();

    public static new BepInEx.Logging.ManualLogSource Logger;

    public void OnEnable()
    {
        Logger = base.Logger;

        // Updating custom inputs (basic API yaaay)
        On.Player.checkInput += Player_checkInput;

        // Presets
        On.Options.ControlSetup.Setup += SetPreset;

        // Input Settings screen ui
        IL.Menu.InputOptionsMenu.ctor += AddCustomButtonsIL;
        On.Menu.InputOptionsMenu.ctor += FixVanillaButtons;
        On.Menu.InputOptionsMenu.Update += InputOptionsMenu_Update;
        On.Menu.InputOptionsMenu.ApplyInputPreset += InputOptionsMenu_ApplyInputPreset;
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

    private void Player_checkInput(On.Player.orig_checkInput orig, Player self)
    {
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
        data.input[0].UpdateAll(key => {
            bool mapSuppressed = self.standStillOnMapButton && self.input[0].mp && (!ModManager.CoopAvailable || !self.jollyButtonDown);
            bool sleepSuppressed = self.Sleeping;
            if (key.MapSuppressed && mapSuppressed || key.SleepSuppressed && self.Sleeping) {
                return false;
            }
            return data.input[0][key];
        });

        orig(self);
    }

    private void SetPreset(On.Options.ControlSetup.orig_Setup orig, Options.ControlSetup self, Options.ControlSetup.Preset preset)
    {
        orig(self, preset);

        if (preset == Options.ControlSetup.Preset.KeyboardSinglePlayer) {
            foreach (var kb in PlayerKeybind.keybinds) {
                kb.keyboard[self.index] = kb.KeyboardPreset;
            }
        }
        else if (preset == Options.ControlSetup.Preset.PS4DualShock || preset == Options.ControlSetup.Preset.PS5DualSense || preset == Options.ControlSetup.Preset.SwitchProController) {
            foreach (var kb in PlayerKeybind.keybinds) {
                kb.gamepad[self.index] = kb.GamepadPreset;
            }
        }
        else if (preset == Options.ControlSetup.Preset.XBox) {
            foreach (var kb in PlayerKeybind.keybinds) {
                kb.gamepad[self.index] = kb.XboxPreset;
            }
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
        int columns = 1 + Mathf.CeilToInt(PlayerKeybind.keybinds.Count / 10f); // 10 per row
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

        foreach (var keybind in PlayerKeybind.keybinds) {
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
        s.Add(new InputSelectButton(self.pages[0], 0, self.Translate("Pause"), c, o - new Vector2(0, y += 20)));
        
        s.Add(GroupLabel("MOVEMENT", o - new Vector2(0, y += 45) + new Vector2(15, 30)));
        s.Add(new InputSelectButton(self.pages[0], 6, self.Translate("Up"), c, o - new Vector2(0, y += 20)));
        s.Add(new InputSelectButton(self.pages[0], 5, self.Translate("Left"), c, o - new Vector2(0, y += 40)));
        s.Add(new InputSelectButton(self.pages[0], 8, self.Translate("Down"), c, o - new Vector2(0, y += 40)));
        s.Add(new InputSelectButton(self.pages[0], 7, self.Translate("Right"), c, o - new Vector2(0, y += 40)));

        s.Add(GroupLabel("VANILLA", o - new Vector2(0, y += 45) + new Vector2(15, 30)));
        s.Add(new InputSelectButton(self.pages[0], 2, self.Translate("Grab"), c, o - new Vector2(0, y += 20)));
        s.Add(new InputSelectButton(self.pages[0], 3, self.Translate("Jump"), c, o - new Vector2(0, y += 40)));
        s.Add(new InputSelectButton(self.pages[0], 4, self.Translate("Throw"), c, o - new Vector2(0, y += 40)));
        s.Add(new InputSelectButton(self.pages[0], 1, self.Translate("Map"), c, o - new Vector2(0, y += 40)));
    }

    private void FixVanillaButtons(On.Menu.InputOptionsMenu.orig_ctor orig, InputOptionsMenu self, ProcessManager manager)
    {
        orig(self, manager);

        // Remove old buttons
        string keyboard = self.Translate("KEYBOARD");
        string gamepad = self.Translate("GAMEPAD");

        for (int i = self.pages[0].subObjects.Count - 1; i >= 0; i--) {
            MenuObject sub = self.pages[0].subObjects[i];

            if (sub is InputOptionsMenu.InputSelectButton || sub is MenuLabel label && (label.text == keyboard || label.text == gamepad || self.inputLabels.Contains(label))) {
                self.pages[0].RemoveSubObject(sub);
                sub.RemoveSprites();
            }
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
                checkBox.pos = new Vector2(632, 154);
                found++;
            }
            else if (checkBox.IDString == "YINV") {
                checkBox.pos = new Vector2(632, 184);
                found++;
            }
            if (found == 2) break;
        }
    }

    private void InputOptionsMenu_Update(On.Menu.InputOptionsMenu.orig_Update orig, InputOptionsMenu self)
    {
        self.settingInput = null;

        orig(self);

        if (self.forbiddenInputButton is not InputSelectButton button) {
            return;
        }

        self.freezeMenuFunctionsCounter++;
        self.selectedObject = button;

        if (self.lastAnyKeyDown || !self.anyKeyDown) {
            return;
        }

        foreach (object obj in Enum.GetValues(typeof(KeyCode))) {
            if (obj is KeyCode keyCode && Input.GetKey(keyCode)) {
                button.InputAssigned(keyCode);
                self.forbiddenInputButton = null;
                break;
            }
        }

        if (self.mouseModeBeforeAssigningInput) {
            self.forceMouseMode = 10;
            self.mouseModeBeforeAssigningInput = false;
        }
    }

    private void InputOptionsMenu_ApplyInputPreset(On.Menu.InputOptionsMenu.orig_ApplyInputPreset orig, InputOptionsMenu self, int preset)
    {
        orig(self, preset);

        // Extra hyperspecific flashies
        foreach (var sub in self.pages[0].subObjects) {
            if (sub is InputSelectButton s && (!s.IndependentOfPlayer || self.CurrentControlSetup.index == 0) && (!s.MovementKey && s.Gamepad) == (preset != 1)) {
                s.Flash();
            }
        }
    }

    private string InputOptionsMenu_UpdateInfoText(On.Menu.InputOptionsMenu.orig_UpdateInfoText orig, InputOptionsMenu self)
    {
        return self.selectedObject is ISelectableText t ? t.Text() : orig(self);
    }

    private void InputTester_ctor(On.Menu.InputTesterHolder.InputTester.orig_ctor orig, InputTesterHolder.InputTester self, Menu.Menu menu, MenuObject owner, int playerIndex)
    {
        orig(self, menu, owner, playerIndex);

        // Remove "pause" tester
        self.RemoveSubObject(self.testButtons[7]);
        self.testButtons[7].RemoveSprites();

        // Rename "Pick up / Eat" to "Grab"
        self.testButtons[4].labelText = menu.Translate("Grab");
        self.testButtons[4].menuLabel.text = menu.Translate("Grab");

        // Make room for map button below grab, jump, and throw
        self.testButtons[4].pos.y += 15;
        self.testButtons[5].pos.y += 15;
        self.testButtons[6].pos.y += 15;

        // Move map there
        self.testButtons[8].pos.x = self.testButtons[6].pos.x;
        self.testButtons[8].pos.y = self.testButtons[6].pos.y - 30;

        // Added modded keybinds
        // Keybind ID is stored as `btn.index = -1 - ID`, so retrieve actual ID by using `ID = -1 - btn.index`
        int i = self.testButtons.Length;
        float x = 280 + (menu.CurrLang == InGameTranslator.LanguageID.French || menu.CurrLang == InGameTranslator.LanguageID.German ? 30 : 0);
        float y = 45;
        Array.Resize(ref self.testButtons, i + PlayerKeybind.keybinds.Count);
        foreach (var keybind in PlayerKeybind.keybinds) {
            self.subObjects.Add(self.testButtons[i++] = new(menu, self, new Vector2(x, y), null, 0, menu.Translate(keybind.Name), -1 - keybind.index, playerIndex));
            y -= 30;
            if (y <= -45) {
                y = 45;
                x += 280;
            }
        }
    }

    private void InputTester_Update(On.Menu.InputTesterHolder.InputTester.orig_Update orig, InputTesterHolder.InputTester self)
    {
        orig(self);

        foreach (var btn in self.testButtons) {
            if (btn.buttonIndex < 0) {
                btn.pressed = PlayerKeybind.keybinds[-1 - btn.buttonIndex].CheckRawPressed(self.playerIndex);
            }
            btn.playerAssignedToAnything = self.playerAssignedToAnything;
        }
    }

    private void InputTester_UpdateTestButtons(On.Menu.InputTesterHolder.InputTester.orig_UpdateTestButtons orig, InputTesterHolder.InputTester self)
    {
        foreach (var btn in self.testButtons) {
            if (btn.menuLabel != null) {
                int keybindIdx = -1 - btn.buttonIndex;
                Options.ControlSetup setup = self.menu.manager.rainWorld.options.controls[btn.playerIndex];
                KeyCode keyCode = btn.buttonIndex < 0
                    ? setup.gamePad ? PlayerKeybind.keybinds[keybindIdx].gamepad[btn.playerIndex] : PlayerKeybind.keybinds[keybindIdx].keyboard[btn.playerIndex]
                    : setup.gamePad ? setup.gamePadButtons[btn.buttonIndex] : setup.keyboardKeys[btn.buttonIndex];

                btn.menuLabel.text = $"{btn.labelText} ( {InputExtensions.ButtonText(self.playerIndex, keyCode, out _)} )";
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
        if (key == "BetterInputConfig:Keybind" && split.Length > 3) {
            string id = split[1];
            string[] keyboard = split[2].Split(',');
            string[] gamepad = split[3].Split(',');

            if (keyboard.Length < 4 || gamepad.Length < 4) return true;

            for (int i = 0; i < 4; i++) {
                PlayerKeybind keybind = PlayerKeybind.keybinds.FirstOrDefault(k => k.Id == id);
                if (keybind == null) {
                    Logger.LogWarning($"Unregistered keybind {id} in save file");
                    return true;
                }
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
            ret += $"BetterInputConfig:Keybind<optB>{k.Id}<optB>{k.keyboard[0]},{k.keyboard[1]},{k.keyboard[2]},{k.keyboard[3]}<optB>{k.gamepad[0]},{k.gamepad[1]},{k.gamepad[2]},{k.gamepad[3]}<optA>";
        }
        return ret;
    }
}
