using Menu;
using RWCustom;
using System.Text.RegularExpressions;
using UnityEngine;

namespace BetterInputConfig;

sealed class InputSelectButton : SimpleButton, IInputButton, ISelectableText
{
    private readonly MenuLabel keybindLabel;
    private readonly MenuLabel currentKey;
    private readonly FSprite circle;
    private readonly FSprite arrow;
    private readonly new InputOptionsMenu menu;
    private readonly bool compactMode;

    public readonly int keyIndex;
    public readonly PlayerKeybind keybind;

    private Color? buttonColor;
    private float filled;
    private float lastFilled;
    private int blinkCounter;
    private bool recentlyUsedGreyedOut;
    private float recentlyUsedFlash;
    private float lastRecentlyUsedFlash;
    private bool init;

    bool lastGamepad;
    public bool Gamepad => ControlSetup.gamePad;

    int lastPlayer;
    int Player => menu.manager.rainWorld.options.playerToSetInputFor;

    public bool IndependentOfPlayer => keyIndex == 0; // just pause button for now
    public bool MovementKey => keyIndex is 5 or 6 or 7 or 8;

    Options.ControlSetup ControlSetup => IndependentOfPlayer ? menu.manager.rainWorld.options.controls[0] : menu.CurrentControlSetup;

    public InputSelectButton(MenuObject owner, PlayerKeybind keybind, bool compact, Vector2 pos) : this(owner, -1 - keybind.id, owner.menu.Translate(keybind.Name), compact, pos)
    {
        this.keybind = keybind;
    }

    public InputSelectButton(MenuObject owner, int index, string keybindName, bool compact, Vector2 pos) : base(owner.menu, owner, "", "", pos, new Vector2(30f, 30f))
    {
        compactMode = compact;
        keyIndex = index;
        menu = (InputOptionsMenu)owner.menu;

        lastGamepad = Gamepad;
        lastPlayer = Player;

        keybindLabel = new MenuLabel(menu, this, keybindName, new Vector2(compact ? -26 : -30, compact ? 8 : 0), size, false);
        keybindLabel.label.alignment = FLabelAlignment.Right;
        subObjects.Add(keybindLabel);

        currentKey = new MenuLabel(menu, this, "", new Vector2(compact ? -26 : 30f, compact ? -8 : 0), size, false, null);
        currentKey.label.alignment = compact ? FLabelAlignment.Right : FLabelAlignment.Left;
        subObjects.Add(currentKey);

        Container.AddChild(circle = new("Futile_White") {
            shader = menu.manager.rainWorld.Shaders["VectorCircleFadable"]
        });
        Container.AddChild(arrow = new("keyShiftB"));
    }

    private KeyCode CurrentlyDisplayed()
    {
        if (keybind != null) {
            return ControlSetup.gamePad ? keybind.gamepad[ControlSetup.index] : keybind.keyboard[ControlSetup.index];
        }

        bool arrowKey = keyIndex is 5 or 6 or 7 or 8;
        return ControlSetup.gamePad && !arrowKey ? ControlSetup.gamePadButtons[keyIndex] : ControlSetup.keyboardKeys[keyIndex];
    }

    public override Color MyColor(float timeStacker)
    {
        KeyCode current = CurrentlyDisplayed();

        // Blink red if conflicting keys on current character
        int currentUses = Plugin.KeybindsOfType(Player, current, stopAt: 2);
        if ((blinkCounter % 20 < 10) && currentUses > 1) {
            return Color.red;
        }

        float t = (blinkCounter % 4 < 2) ? 0f : Custom.SCurve(Mathf.Lerp(lastRecentlyUsedFlash, recentlyUsedFlash, timeStacker), 0.4f);

        Color color = Color.Lerp(base.MyColor(timeStacker), Menu.Menu.MenuRGB(Menu.Menu.MenuColors.White), t);

        if (!buttonBehav.greyedOut && !IndependentOfPlayer) {
            bool DuplicateKeys(int player)
            {
                Options.ControlSetup[] controls = menu.manager.rainWorld.options.controls;
                if (Player == player || controls[Player].gamePad != controls[player].gamePad) {
                    return false;
                }
                if (controls[player].gamePad && controls[Player].gamePadNumber != controls[player].gamePadNumber) {
                    return false;
                }
                return Plugin.KeybindsOfType(player, current, 1) > 0;
            }
            // Hint at Survivor, Monk, Hunter, or Nightcat (respectively) having a duplicate key
            if (blinkCounter % 80 is < 20 && DuplicateKeys(0))              return Color.Lerp(color, new Color(1, 1, 1), 0.5f);
            if (blinkCounter % 80 is >= 20 and < 40 && DuplicateKeys(1))    return Color.Lerp(color, new Color(1, 1, 0), 0.15f);
            if (blinkCounter % 80 is >= 40 and < 60 && DuplicateKeys(2))    return Color.Lerp(color, new Color(1, 0, 0), 0.12f);
            if (blinkCounter % 80 is >= 60 && DuplicateKeys(3))             return Color.Lerp(color, new Color(0, 0, .5f), 0.2f);
        }

        return color;
    }

    public void InputAssigned(KeyCode keyCode)
    {
        string keyCodeString = keyCode.ToString();
        if (keyCodeString.Length > 4 && keyCodeString.Substring(0, 5) == "Mouse") {
            menu.PlaySound(SoundID.MENU_Error_Ping);
        }
        else if ((keyCodeString.Length > 7 && keyCodeString.Substring(0, 8) == "Joystick") != Gamepad) {
            menu.PlaySound(SoundID.MENU_Error_Ping);
        }
        else {
            menu.PlaySound(SoundID.MENU_Button_Successfully_Assigned);

            if (keybind != null) {
                if (Gamepad)
                    keybind.gamepad[Player] = keyCode;
                else
                    keybind.keyboard[Player] = keyCode;
            }
            else if (Gamepad) {
                ControlSetup.gamePadButtons[keyIndex] = keyCode;
            }
            else {
                ControlSetup.keyboardKeys[keyIndex] = keyCode;
            }
        }

        Flash();
    }

    public void Flash()
    {
        RefreshKeyDisplay();
        recentlyUsedGreyedOut = true;
        recentlyUsedFlash = 1f;
    }

    public override void Update()
    {
        if (!init) {
            init = true;
            RefreshKeyDisplay();
        }

        base.Update();

        blinkCounter++;
        lastFilled = filled;

        if (menu.forbiddenInputButton == this) {
            filled = Custom.LerpAndTick(filled, 1f, 0.05f, 0.05f);
            if (blinkCounter % 30 < 15) {
                currentKey.text = "?";
            }
            else {
                RefreshKeyDisplay();
            }
        }
        else {
            filled = Custom.LerpAndTick(filled, 0f, 0.05f, 0.05f);
        }

        if (recentlyUsedGreyedOut) {
            if (Selected) {
                buttonBehav.greyedOut = true;
            }
            else if (menu.selectedObject != null) {
                recentlyUsedGreyedOut = false;
                buttonBehav.greyedOut = false;
            }
        }
        else if (IndependentOfPlayer) {
            buttonBehav.greyedOut = menu.CurrentControlSetup.index != 0;
        }
        else if (MovementKey) {
            buttonBehav.greyedOut = Gamepad;
        }

        if (!MovementKey && lastGamepad != Gamepad || !IndependentOfPlayer && lastPlayer != Player && !(MovementKey && Gamepad)) {
            RefreshKeyDisplay();
        }

        lastPlayer = Player;
        lastGamepad = Gamepad;
        lastRecentlyUsedFlash = recentlyUsedFlash;
        recentlyUsedFlash = Mathf.Max(0f, recentlyUsedFlash - 0.025f);
    }

    public override void GrafUpdate(float timeStacker)
    {
        base.GrafUpdate(timeStacker);

        float a = currentKey.label.alpha;
        currentKey.label.color = currentKey.label.text != "?" && buttonColor.HasValue
            ? Color.Lerp(MyColor(timeStacker), buttonColor.Value, 0.5f)
            : MyColor(timeStacker);
        currentKey.label.alpha = a;

        circle.x = DrawX(timeStacker) + size.x / 2f;
        circle.y = DrawY(timeStacker) + size.y / 2f;
        circle.scale = Mathf.Lerp(0.5f, 0.875f, Mathf.Pow(Custom.SCurve(Mathf.Lerp(lastFilled, filled, timeStacker), 0.3f), 1.4f));
        circle.color = new Color(0f, 1f, Mathf.Pow(Mathf.Max(0f, Mathf.Lerp(lastFilled, filled, timeStacker)), 0.5f));
        arrow.x = DrawX(timeStacker) + size.x / 2f + (compactMode ? -26 - arrow.width / 2f : 30 + arrow.width / 2f);
        arrow.y = DrawY(timeStacker) + size.y / 2f + (compactMode ? -10 : 0);
        arrow.scale = 1f;

        a = arrow.alpha;
        arrow.color = MyColor(timeStacker);
        arrow.alpha = a;
    }

    public override void RemoveSprites()
    {
        base.RemoveSprites();
        circle.RemoveFromContainer();
        arrow.RemoveFromContainer();
    }

    public void RefreshKeyDisplay()
    {
        recentlyUsedFlash = Mathf.Max(recentlyUsedFlash, 0.65f);

        string text = Plugin.ButtonText(Player, CurrentlyDisplayed(), out buttonColor);
        if (text.EndsWith("Arrow")) {
            currentKey.label.alpha = 0;
            arrow.alpha = 1;
            arrow.rotation = text switch {
                "LeftArrow" => -90,
                "RightArrow" => 90,
                "DownArrow" => 180,
                _ => 0
            };
        }
        else {
            arrow.alpha = 0;
            currentKey.label.alpha = 1;
            currentKey.text = text;
        }
    }

    public override void Clicked()
    {
        menu.mouseModeBeforeAssigningInput = menu.manager.menuesMouseMode;
        if (this != menu.forbiddenInputButton) {
            menu.selectedObject = this;
            menu.forbiddenInputButton = this;
        }
        menu.PlaySound(SoundID.MENU_Button_Standard_Button_Pressed);
    }

    public string Text()
    {
        if (!recentlyUsedGreyedOut && IndependentOfPlayer && menu.CurrentControlSetup.index != 0) {
            return menu.Translate("Only available for player 1");
        }
        if (!recentlyUsedGreyedOut && MovementKey && Gamepad) {
            return menu.Translate("Only available for keyboard");
        }
        return Regex.Replace(menu.Translate("Bind <X> button"), "<X>", $"< {keybindLabel.text} >") + (keybind != null ? $" ({keybind.Mod})" : "");
    }
}
