﻿using BepInEx;
using HarmonyLib;
using MonoMod.RuntimeDetour;
using Rewired;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Security.Permissions;
using Rewired.Data;

// Allows access to private members
#pragma warning disable CS0618
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618

namespace ImprovedInput;

[BepInPlugin("com.dual.improved-input-config", "Improved Input Config", "2.0.0")]
sealed class Plugin : BaseUnityPlugin
{
    public static new BepInEx.Logging.ManualLogSource Logger;

    // input update data
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

    internal static bool initModdedActions = false;
    internal static List<InputAction> vanillaInputActions;
    internal static int highestVanillaActionId;

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
        
        // Input Menu stuff
        InputMenuHooks.InitHooks();
        SaveAndLoadHooks.InitHooks();
    }

    // Adding new Input Actions when the game loads (+30), and other init stuff.
    private static List<InputAction> UserData_GetActions_Hook(Func<UserData, List<InputAction>> orig, UserData self)
    {
        List<InputAction> actions = orig(self);
        if (!initModdedActions)
        {
            vanillaInputActions = actions;
            foreach (InputAction inputAction in vanillaInputActions)
                if (inputAction.id > highestVanillaActionId)
                    highestVanillaActionId = inputAction.id;

            Logger.LogInfo("Adding new Input Actions");

            // add new actions (+30)
            for(int i = 0; i < 30; i++)
                self.AddAction(0);
            actions = orig(self);

            // log new actions
            string actionsString = "";
            foreach (var action in actions)
                actionsString += action.id + " ";
            Logger.LogInfo("New Input Actions " + actionsString);

            // init modded keybinds
            PlayerKeybind.addActionIds();

            initModdedActions = true;
        }
        return actions;
    }

    // Input logic updates
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
}
