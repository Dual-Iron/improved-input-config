using BepInEx;
using HarmonyLib;
using Menu;
using MonoMod.RuntimeDetour;
using Rewired;
using RWCustom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Permissions;
using UnityEngine;
using Rewired.Data;

// Allows access to private members
#pragma warning disable CS0618
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618

namespace ImprovedInput;

[BepInPlugin("com.dual.improved-input-config", "Improved Input Config", "2.0.0")]
sealed class Plugin : BaseUnityPlugin
{
    // TODO revisit this later
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
        //Player player;

        Logger = base.Logger;

        // UserData
        new Hook(AccessTools.Method(typeof(UserData), "GetActions_Copy"), UserData_GetActions_Hook);

        // Updating custom inputs (basic API yaaay)
        On.Player.checkInput += UpdateInput;
        On.Player.UpdateMSC += UpdateNoInputCounter;

        // Saving
        //On.Options.ApplyOption += Options_ApplyOption;
        //On.Options.Load += Options_Load;
        //On.Options.ToString += Options_ToString;

        // Input Menu stuff
        InputMenuHooks.InitHooks();
    }

    // Adding new Input Actions when the game loads. +(35-64)
    private static List<InputAction> UserData_GetActions_Hook(Func<UserData, List<InputAction>> orig, UserData self)
    {
        List<InputAction> actions =  orig(self);
        if (actions.Count == 15)
        {
            Logger.LogMessage("ADDING NEW ACTIONS");
            for(int i = 0; i < 30; i++)
            {
                self.AddAction(0);
            }
            actions = orig(self);
            string actionsString = "";
            foreach (var action in actions)
            {
                actionsString += action.id + " ";
            }
            Logger.LogMessage("NEW ACTIONS " + actionsString);
        }
        return actions;
    }

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
        if (playerNumber < 0 || playerNumber >= CustomInputExt.MaxPlayers) {
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

    readonly List<string> unregistered = new();
    private bool Options_ApplyOption(On.Options.orig_ApplyOption orig, Options self, string[] split)
    {
        // Return TRUE if invalid or unrecognized data
        bool unrecognized = orig(self, split);
        if (!unrecognized) {
            return false;
        }
        string key = split[0];
        if (key == "iic:keybind") {
            string id = split[1];
            string[] keyboard = split[2].Split(',');
            string[] gamepad = split[3].Split(',');

            if (keyboard.Length < 4 || gamepad.Length < 4) return true;

            PlayerKeybind keybind = PlayerKeybind.keybinds.FirstOrDefault(k => k.Id == id);
            if (keybind == null) {
                unregistered.Add(id);
                return true;
            }

            // min to prevent OOB
            for (int i = 0; i < Mathf.Min(keyboard.Length, CustomInputExt.maxMaxPlayers); i++) {
                if (Enum.TryParse(keyboard[i], out KeyCode k)) keybind.keyboard[i] = k;
                if (Enum.TryParse(gamepad[i], out KeyCode k2)) keybind.gamepad[i] = k2;
            }
            return false;
        }
        return true;
    }

    private void Options_Load(On.Options.orig_Load orig, Options self)
    {
        unregistered.Clear();
        orig(self);
        if (unregistered.Count > 0) {
            Logger.LogDebug($"Unregistered keybinds in save file: [{string.Join(", ", unregistered)}]");
        }
    }

    private string Options_ToString(On.Options.orig_ToString orig, Options self)
    {
        string ret = orig(self);
        foreach (PlayerKeybind k in PlayerKeybind.keybinds) {
            //ret += $"iic:keybind<optB>{k.Id}<optB>{k.keyboard[0]},{k.keyboard[1]},{k.keyboard[2]},{k.keyboard[3]}<optB>{k.gamepad[0]},{k.gamepad[1]},{k.gamepad[2]},{k.gamepad[3]}<optA>";
            ret += $"iic:keybind<optB>{k.Id}<optB>";
            for (int l = 0; l < CustomInputExt.maxMaxPlayers; l++)
            {
                if (l != 0)
                    ret += ",";
                ret += $"{k.keyboard[l]}";
            }
            ret += "<optB>";
            for (int l = 0; l < CustomInputExt.maxMaxPlayers; l++)
            {
                if (l != 0)
                    ret += ",";
                ret += $"{k.gamepad[l]}";
            }
            ret += "<optA>";
            // CustomInputExt.MaxPlayers
        }
        return ret;
    }
}
