using BepInEx.Bootstrap;
using HarmonyLib;

namespace Nomnom.BepInEx.Editor.Patches {
    [HarmonyPatch(typeof(Chainloader))]
    [InjectPatch(PatchLifetime.Always)]
    internal static class ChainloaderEditorPatch {
        [HarmonyPatch("IsEditor", MethodType.Getter)]
        [HarmonyPostfix]
        private static void Get(ref bool __result) {
            __result = false;
        }
    }
}