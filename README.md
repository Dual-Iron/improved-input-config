# Improved Input Config

## To install
Visit [its Steam Workshop page](https://steamcommunity.com/sharedfiles/filedetails/?id=2944727862) and subscribe!

## To add a keybind in your mod
Add `improved-input-config` as a requirement in [`modinfo.json`](https://rainworldmodding.miraheze.org/wiki/Downpour_Reference/Mod_Directories#ModInfo_JSON), then reference [the latest release](https://github.com/Dual-Iron/improved-input-config/releases/latest). Make sure to keep the `ImprovedInput.xml` file next to the `ImprovedInput.dll` file.

Registering a keybind boils down to something like this in your mod's plugin class:

```cs
public static readonly PlayerKeybind Explode = PlayerKeybind.Register("example:explode", "Example Mod", "Explode", KeyCode.C, KeyCode.JoystickButton3);
```

<details>
  <summary>If that doesn't work...</summary>
  
  ...assign the keybind in your `OnEnable()` method inside a try-catch block to make sure there's no issues.
  
  ```cs
  public static PlayerKeybind Explode;

  void OnEnable() {
    try {
      Explode = PlayerKeybind.Register("example:explode", "Example Mod", "Explode", KeyCode.C, KeyCode.JoystickButton3);
    }
    catch (Exception e) {
      Logger.LogError(e);
    }
  }
  ```
  
</details>
