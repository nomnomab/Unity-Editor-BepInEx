<div align="center">
  <h1>Unity Editor BepInEx</h1>

  <p>
    Provides a way to use BepInEx inside of the UnityEditor to hook into various internal functionalities.
  </p>
</div>

<!-- Getting Started -->
## Getting Started

This project is bundled with the following packages:

- [BepInEx](https://github.com/BepInEx/BepInEx)
- [MonoMod](https://github.com/MonoMod/MonoMod)

> [!IMPORTANT]  
> The Unity Editor does not support BepInEx pre-loaders.

<!-- Prerequisites -->
### Prerequisites

- Minimum Unity 2020.3 afaik
    - Still needs more testing with how to handle older Unity versions
- [Git](https://git-scm.com/download/win)

<!-- Installation -->
## Installation

### Manual

If you need to change the BepInEx dlls around, this is the more common way to install the package.

1. Go to the top of this page and click on the `Code` button, then press `Download ZIP` in the dropdown.
2. Extract the zip file to a folder
3. Copy that folder into your unity project
4. Create a folder named `Mods` at the root of your `Assets`, this is where you will put your custom patches
5. Create a custom patch:

```csharp
using UnityEngine;

// an example target MonoBehaviour
public sealed class Foo: MonoBehaviour {
    // we will be targetting this!
    public int Property { get; private set; } = 500;

    // disable the component in Play Mode to trigger the patch
    public void OnDisable() {
        Property++;
    }
}
```

```csharp
using BepInEx;
using HarmonyLib;
using UnityEngine;

// this attribute is required so BepInEx can find it!
[BepInPlugin("com.unity.plugin", "Unity Plugin", "1.0.0")]
public sealed class TestPlugin: BaseUnityPlugin {
    private void Awake() {
        Harmony.CreateAndPatchAll(typeof(TestPatches));
        Debug.Log($"Loaded TestPlugin");
    }
}

// patching a property on Foo when it is used!
[HarmonyPatch(typeof(Foo), nameof(Foo.Property))]
static class TestPatches {
    [HarmonyPatch(MethodType.Setter)]
    [HarmonyPostfix]
    static void OnCameraSetter(int value, object __instance) {
        Debug.Log($"[set] {__instance}.Property set to {value}");
    }
    
    [HarmonyPatch(MethodType.Getter)]
    [HarmonyPostfix]
    static void OnCameraGetter(int __result, object __instance) {
        Debug.Log($"[get] {__instance}.Property set to {__result}");
    }
}
```

For more patching documentation, make sure to look at the [Harmony](https://harmony.pardeike.net/articles/patching.html) docs!

### Via Package Manager

> [!IMPORTANT]  
> These options require [git](https://git-scm.com/download/win) to be installed!
> 
> This does not allow you to change the dlls easily if the versioning is incorrect.

Install with the package manager:

1. Open the Package Manager from `Window > Package Manager`
2. Click the '+' button in the top-left of the window
3. Click 'Add package from git URL'
4. Provide the URL of the this git repository: `https://github.com/nomnomab/Unity-Editor-BepInEx.git`
- If you are using a specific version, you can append it to the end of the git URL, such as `#v1.2.3`
5. Click the 'add' button

Install with the manifest.json:

1. Open the manifest at `[PROJECT_NAME]\Packages\manifest.json`
2. Insert the following as an entry:

```json
"com.nomnom.unity-editor-bepinex": "https://github.com/nomnomab/Unity-Editor-BepInEx.git"
```

- If you are using a specific version, you can append it to the end of the git URL, such as `#v1.2.3`

## GitIgnore entries

```gitignore
# this is the folder above Assets that fakes a built unity project
# it's a quirk of how the BepInEx chainloader works 
BepInExGameName\

# if you installed manually
# this would be the name of the folder you dragged inside of Assets
[Aa]ssets\unity-editor-bepinex-master\
```

<br/>

[![ko-fi](https://ko-fi.com/img/githubbutton_sm.svg)](https://ko-fi.com/B0B6R2Z9U)