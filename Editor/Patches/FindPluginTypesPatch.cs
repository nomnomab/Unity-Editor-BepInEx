#if ENABLE_BEPINEX
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;

namespace Nomnom.BepInEx.Editor.Patches {
    [HarmonyPatch]
    internal static class FindPluginTypesPatch {
        public static MethodBase TargetMethod() {
            return AccessTools
                .Method(typeof(TypeLoader), nameof(TypeLoader.FindPluginTypes))
                .MakeGenericMethod(typeof(PluginInfo));
        }

        public static Dictionary<string, List<PluginInfo>> Postfix(Dictionary<string, List<PluginInfo>> result) {
            var hasBepinPluginsFunction = AccessTools.Method(typeof(Chainloader), "HasBepinPlugins");
            var assemblies = BepInExPreloader.GetAllPluginAssemblies().ToArray();

            Debug.Log($"Checking {assemblies.Length}...");
            foreach (var pair in assemblies) {
                if (result.ContainsKey(pair.file)) {
                    // Debug.Log($"> Skipping \"{pair.file}\" as it has already been processed");
                    pair.assembly.Dispose();
                    continue;
                }

                if (!(bool)hasBepinPluginsFunction.Invoke(null, new object[] { pair.assembly })) {
                    result[pair.file] = new List<PluginInfo>();
                    pair.assembly.Dispose();

                    // Debug.Log($"No BepInEx plugins found in \"{pair.file}\"");
                    continue;
                }

                try {
                    Debug.Log($" - Found {pair.assembly} : {pair.assembly.MainModule}");
                    var list = pair.assembly.MainModule.Types
                        .Select(x => {
                            try {
                                return Chainloader.ToPluginInfo(x);
                            } catch (Exception e) {
                                Debug.LogWarning($"Failed to resolve {x.Name}:\n{e}");
                                return null;
                            }
                        })
                        .Where(t => t != null)
                        .ToList();

                    result[pair.file] = list;
                    pair.assembly.Dispose();

                    Debug.Log($"Found {list.Count} plugins in \"{pair.file}\"");
                    for (var i = 0; i < list.Count; i++) {
                        var plugin = list[i];
                        Debug.Log($" - {plugin.Metadata.GUID} - {plugin.Metadata.Name}");
                    }
                } catch (Exception e) {
                    Debug.LogWarning(e);
                }
            }

            Debug.Log("Final results:");
            foreach (var pair in result) {
                foreach (var plugin in pair.Value) {
                    Debug.Log($" - {plugin.Metadata.GUID} - {plugin.Metadata.Name}");
                }
            }

            return result;
        }
    }
}
#endif