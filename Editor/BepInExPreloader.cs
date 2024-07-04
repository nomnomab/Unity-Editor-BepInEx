using UnityEngine;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using HarmonyLib;
using Mono.Cecil;
using Nomnom.BepInEx.Editor.Patches;
using UnityEditor;
using Logger = BepInEx.Logging.Logger;

namespace Nomnom.BepInEx.Editor {
    /// <summary>
    /// Loads BepInEx for the editor in a bit of a hacky way.
    /// If there is an easier way to do this, please tell me!
    /// </summary>
    internal static class BepInExPreloader {
        private static List<Assembly> _assemblies = new List<Assembly>();

        private static void Print(object message) {
            // Debug.Log(message);
        }
        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void OnLoad() {
            InitPaths();
            
            var obj = new GameObject(typeof(BepInExPreloader).FullName);
            var lifetime = obj.AddComponent<BepInExSceneLifetime>();
            lifetime.Init(OnDestroy);
            GameObject.DontDestroyOnLoad(obj);
            
            var stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();
            
            var settings = GetBepInExUserSettings();
            if (!EditorApplication.isPlayingOrWillChangePlaymode) return;
            
            Init(settings);

            var patches = GatherInjectPatches().ToArray();
            var harmony = new Harmony("com.nomnom.editor-bepinex");
            // do things if needed here
            if (settings.LoadProjectPlugins) {
                harmony.PatchAll(typeof(FindPluginTypesPatch));
            }

            Debug.Log($"Checking {patches.Length} patches for chainloader patches...");
            foreach (var patch in patches) {
                if (patch.Item2.Lifetime.HasFlag(PatchLifetime.DuringChainloader)) {
                    Debug.Log($" - found {patch.Item1}");
                    harmony.PatchAll(patch.Item1);
                }
            }
            
            LoadDlls(settings);
            LoadChainloader(settings);
            OverrideBepInExPaths(settings);
            AssignManagedData(settings.PluginsPath);

            Chainloader.Start();
            
            harmony.UnpatchSelf();
            // do final things if needed here
            Debug.Log($"Checking {patches.Length} patches for after chainloader patches...");
            foreach (var patch in patches) {
                if (patch.Item2.Lifetime.HasFlag(PatchLifetime.AfterChainloader)) {
                    Debug.Log($" - found {patch.Item1}");
                    harmony.PatchAll(patch.Item1);
                }
            }
            
            Debug.Log($"BepInEx - Loaded in {stopwatch.ElapsedMilliseconds}ms");
            stopwatch.Stop();
        }

        private static void Init(BepinexUserSettings settings) {
            var routerGameExePath = settings.RouterGameExePath;
            
            // override exe
            var setExecutableMethod = typeof(Paths).GetMethod("SetExecutablePath", BindingFlags.NonPublic | BindingFlags.Static);
            setExecutableMethod.Invoke(null, new object[] { routerGameExePath, null, null, null });
            Print($" - <color=gray>setExecutableMethod -> {routerGameExePath}</color>");
            
            // clear all loggers
            var sources = typeof(Logger).GetProperty("Sources", BindingFlags.Public | BindingFlags.Static);
            var sourcesValue = (ICollection<ILogSource>)sources.GetValue(null);
            sourcesValue.Clear();
            Print($" - <color=gray>cleared all loggers</color>");
            
            // reset _Listeners
            var listeners = AccessTools.Field(typeof(Logger), "_Listeners");
            var listenersValue = (ICollection<ILogListener>)listeners.GetValue(null);
            listenersValue.Clear();
            Print($" - <color=gray>cleared all listeners</color>");
        
            // reset Chainloader._initialized
            var initialized = AccessTools.Field(typeof(Chainloader), "_initialized");
            initialized.SetValue(null, false);
            Print($" - <color=gray>reset Chainloader._initialized</color>");
        
            // reset _loaded
            var loaded = AccessTools.Field(typeof(Chainloader), "_loaded");
            loaded.SetValue(null, false);
            Print($" - <color=gray>reset Chainloader._loaded</color>");
        
            // reset internalLogsInitialized
            var internalLogsInitialized = AccessTools.Field(typeof(Logger), "internalLogsInitialized");
            internalLogsInitialized.SetValue(null, false);
            Print($" - <color=gray>reset internalLogsInitialized</color>");
        
            // change UnityLogListener.WriteStringToUnityLog to debug log
            var writeStringToUnityLog = AccessTools.Field(typeof(UnityLogListener), "WriteStringToUnityLog");
            writeStringToUnityLog.SetValue(null, new Action<string>(Debug.Log));
            Print($" - <color=gray>set WriteStringToUnityLog to Debug.Log</color>");
            
            // need to copy bepinex dlls to the core folder
            var coreFolder = settings.CoreFolder;
            var packageDllFolder = Path.GetFullPath("Packages/com.nomnom.unity-editor-bepinex/Runtime/BepInEx");
            
            if (!Directory.Exists(coreFolder)) {
                Directory.CreateDirectory(coreFolder);
            }

            foreach (var file in Directory.GetFiles(packageDllFolder, "*.dll", SearchOption.AllDirectories)) {
                var newPath = Path.Combine(coreFolder, Path.GetFileName(file));
                if (File.Exists(newPath)) continue;
                
                try {
                    File.Copy(file, newPath, true);
                } catch {
                    Debug.LogWarning($"Failed to copy \"{file}\" to \"{newPath}\"");
                }
            }
        }

        private static void LoadDlls(BepinexUserSettings settings) {
            Debug.Log($"Loading assemblies from {settings.PluginsPath}");
            
            // let up do something dumb! :)
            // get all dlls from game's plugins and manually load them since
            // sub-dependencies are not always loaded
            // var settings = PatcherUtility.GetSettings();
            _assemblies.Clear();

            foreach (var assembly in GetPluginAssemblies(settings)) {
                Debug.Log($" - loading \"{assembly.GetName().Name}\"");
                _assemblies.Add(assembly);
            }
        }

        private static void LoadChainloader(BepinexUserSettings settings) {
            Debug.Log($"Loading Chainloader for \"{settings.RouterGameExePath}\"");
            Chainloader.Initialize(settings.RouterGameExePath, false);
        }
        
        private static void OverrideBepInExPaths(BepinexUserSettings settings) {
            Debug.Log($"Overriding BepInEx paths");
            
            var bepInExFolder = settings.RootFolder;
            var bepInExRootPath = typeof(Paths).GetProperty("BepInExRootPath", BindingFlags.Public | BindingFlags.Static);
            bepInExRootPath.SetValue(null, bepInExFolder);
            Print($" - <color=gray>set BepInExRootPath to \"{bepInExFolder}\"</color>");
        
            var configPath = typeof(Paths).GetProperty("ConfigPath", BindingFlags.Public | BindingFlags.Static);
            configPath.SetValue(null, Path.Combine(bepInExFolder, "config"));
            Print($" - <color=gray>set ConfigPath to \"{Path.Combine(bepInExFolder, "config")}\"</color>");
        
            var bepInExConfigPath = typeof(Paths).GetProperty("BepInExConfigPath", BindingFlags.Public | BindingFlags.Static);
            bepInExConfigPath.SetValue(null, Path.Combine(bepInExFolder, "config", "BepInEx.cfg"));
            Print($" - <color=gray>set BepInExConfigPath to \"{Path.Combine(bepInExFolder, "config", "BepInEx.cfg")}\"</color>");
        
            var patcherPluginPath = typeof(Paths).GetProperty("PatcherPluginPath", BindingFlags.Public | BindingFlags.Static);
            patcherPluginPath.SetValue(null, Path.Combine(bepInExFolder, "patchers"));
            Print($" - <color=gray>set PatcherPluginPath to \"{Path.Combine(bepInExFolder, "patchers")}\"</color>");
        
            var bepInExAssemblyDirectory = typeof(Paths).GetProperty("BepInExAssemblyDirectory", BindingFlags.Public | BindingFlags.Static);
            bepInExAssemblyDirectory.SetValue(null, Path.Combine(bepInExFolder, "core"));
            Print($" - <color=gray>set BepInExAssemblyDirectory to \"{Path.Combine(bepInExFolder, "core")}\"</color>");
        
            var bepInExAssemblyPath = typeof(Paths).GetProperty("BepInExAssemblyPath", BindingFlags.Public | BindingFlags.Static);
            bepInExAssemblyPath.SetValue(null, Path.Combine(Path.Combine(bepInExFolder, "core"), typeof(Paths).Assembly.GetName().Name + ".dll"));
            Print($" - <color=gray>set BepInExAssemblyPath to \"{Path.Combine(Path.Combine(bepInExFolder, "core"), typeof(Paths).Assembly.GetName().Name + ".dll")}\"</color>");
        
            var cachePath = typeof(Paths).GetProperty("CachePath", BindingFlags.Public | BindingFlags.Static);
            cachePath.SetValue(null, Path.Combine(bepInExFolder, "cache"));
            Print($" - <color=gray>set CachePath to \"{Path.Combine(bepInExFolder, "cache")}\"</color>");
        }
        
        private static void AssignManagedData(params string[] pluginsPaths) {
            // ManagedPath
            var managedPathProject = Path.Combine(Application.dataPath, "..", "Library", "ScriptAssemblies");
            var managedPath = @"C:\Program Files\Unity\Hub\Editor\2022.1.24f1\Editor\Data\Managed";
            var managedPath2 = @"C:\Program Files\Unity\Hub\Editor\2022.1.24f1\Editor\Data\NetStandard\ref\2.1.0";
            var managedPathProperty = typeof(Paths).GetProperty("ManagedPath");
            managedPathProperty.SetValue(null, managedPath);
            Print($" - <color=gray>set ManagedPath to \"{managedPath}\"</color>");
        
            // DllSearchPaths
            var dllSearchPathsProperty = typeof(Paths).GetProperty("DllSearchPaths");
            var newDllSearchPaths = new[] { managedPath, managedPath2, managedPathProject }.Concat(pluginsPaths).ToArray();
            dllSearchPathsProperty.SetValue(null, newDllSearchPaths);
            Print($" - <color=gray>set DllSearchPaths to {string.Join(", ", newDllSearchPaths)}</color>");
        }
        
        private static void OnDestroy() {
            Harmony.UnpatchAll();

            // DisposePlugins();
            _assemblies.Clear();

            // clean up hidden objects
            // foreach (var obj in Resources.FindObjectsOfTypeAll<GameObject>()) {
            //     if (EditorUtility.IsPersistent(obj.transform.root.gameObject)) {
            //         continue;
            //     }
            //
            //     if (obj.name.StartsWith("Scene") || obj.name == "Default Volume") {
            //         continue;
            //     }
            //
            //     if (obj.hideFlags == HideFlags.HideAndDontSave) {
            //         Debug.Log($"Destroying {obj}");
            //         GameObject.Destroy(obj);
            //     }
            // }
        }
        
        public static BepinexUserSettings GetBepInExUserSettings() {
            var assets = AssetDatabase.FindAssets($"t:{nameof(BepinexUserSettings)}");
            if (assets.Length == 0) {
                CreateBepInExUserSettings();
                Debug.LogWarning("Created BepInExUserSettings asset since it was missing");
                assets = AssetDatabase.FindAssets($"t:{nameof(BepinexUserSettings)}");
            }

            var assetPath = AssetDatabase.GUIDToAssetPath(assets[0]);
            return AssetDatabase.LoadAssetAtPath<BepinexUserSettings>(assetPath);    
        }
        
        private static void CreateBepInExUserSettings() {
            // create one at root
            var settings = ScriptableObject.CreateInstance<BepinexUserSettings>();
            AssetDatabase.CreateAsset(settings, "Assets/BepInExUserSettings.asset");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (EditorApplication.isPlaying) {
                EditorApplication.ExitPlaymode();
                EditorApplication.EnterPlaymode();
            }
        }
        
        // public static IEnumerable<PluginBlockerUserSettings> GetAllPluginBlockerUserSettings() {
        //     var assets = AssetDatabase.FindAssets($"t:{nameof(PluginBlockerUserSettings)}");
        //     if (assets.Length == 0) {
        //         CreatePluginBlockerUserSettings();
        //         Debug.LogWarning("Created PluginBlockerUserSettings asset since it was missing");
        //         assets = AssetDatabase.FindAssets($"t:{nameof(PluginBlockerUserSettings)}");
        //     }
        //
        //     foreach (var asset in assets) {
        //         var assetPath = AssetDatabase.GUIDToAssetPath(asset);
        //         yield return AssetDatabase.LoadAssetAtPath<PluginBlockerUserSettings>(assetPath);
        //     }
        //     
        //     // var assetPath = AssetDatabase.GUIDToAssetPath(assets[0]);
        //     // return AssetDatabase.LoadAssetAtPath<PluginBlockerUserSettings>(assetPath);    
        // }
        //
        // private static void CreatePluginBlockerUserSettings() {
        //     // create one at root
        //     var settings = ScriptableObject.CreateInstance<PluginBlockerUserSettings>();
        //     AssetDatabase.CreateAsset(settings, "Assets/PluginBlockerUserSettings.asset");
        //     AssetDatabase.SaveAssets();
        //     AssetDatabase.Refresh();
        //
        //     if (EditorApplication.isPlaying) {
        //         EditorApplication.ExitPlaymode();
        //         EditorApplication.EnterPlaymode();
        //     }
        // }
        
        public static IEnumerable<Assembly> GetPluginAssemblies(BepinexUserSettings settings) {
            return GetPluginAssemblies(settings.PluginsPath);
        }
        
        public static IEnumerable<Assembly> GetPluginAssemblies(string pluginsPath) {
            var gameDlls = Directory.GetFiles(pluginsPath, "*.dll", SearchOption.AllDirectories);
            foreach (var gameDll in gameDlls) {
                Assembly assembly = null;
                try {
                    assembly = Assembly.LoadFile(gameDll);
                } catch (Exception e) {
                    continue;
                }
                
                if (assembly == null) continue;

                yield return assembly;
            }
        }
        
        public static IEnumerable<string> GetExternalPluginAssemblyNames(BepinexUserSettings settings) {
            return GetExternalPluginAssemblyNames(settings.PluginsPath);
        }

        public static IEnumerable<string> GetExternalPluginAssemblyNames(string pluginsPath) {
            if (!Directory.Exists(pluginsPath)) {
                Debug.LogWarning($"Plugins path does not exist: \"{pluginsPath}\"");
                yield break;
            }
            
            var gameDlls = Directory.GetFiles(pluginsPath, "*.dll", SearchOption.AllDirectories);
            foreach (var gameDll in gameDlls) {
                yield return Path.GetFileNameWithoutExtension(gameDll);
            }
        }
        
        public static IEnumerable<string> GetInternalPluginAssemblyNames() {
            var modsPath = Path.Combine(Application.dataPath, "Mods");
            if (!Directory.Exists(modsPath)) {
                Debug.LogWarning($"Plugins path does not exist: \"{modsPath}\"");
                yield break;
            }
            
            var gameDlls = Directory.GetFiles(modsPath, "*.dll", SearchOption.AllDirectories);
            foreach (var gameDll in gameDlls) {
                yield return Path.GetFileNameWithoutExtension(gameDll);
            }
        }

        internal static IEnumerable<(string file, AssemblyDefinition assembly)> GetAllPluginAssemblies(bool onlyMods = false) {
            var modsPath = Path.Combine("Assets", "Mods");
            var modsDirectory = modsPath.Replace("\\", "/");
            modsDirectory = modsDirectory.Replace('/', Path.DirectorySeparatorChar);

            Debug.Log(Path.GetFullPath(modsDirectory));

            var files = AccessTools
                .AllTypes()
                .Select(x => x.Module.FullyQualifiedName)
                .Where(x => (!onlyMods && x.Contains("Assembly-CSharp")) || x.Contains(modsDirectory))
                .Distinct()
                .Select(x => (x, AssemblyDefinition.ReadAssembly(x, TypeLoader.ReaderParameters)));
        
            return files;
        }

        internal static IEnumerable<(Type, InjectPatchAttribute)> GatherInjectPatches() {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
                foreach (var type in GetValidTypes(assembly)) {
                    if (type.GetCustomAttributes<InjectPatchAttribute>().Any()) {
                        yield return (type, type.GetCustomAttributes<InjectPatchAttribute>().First());
                    }
                }
            }
        }
        
        internal static IEnumerable<Type> GetValidTypes(Assembly assembly) {
            Type[] types;
            try {
                types = assembly.GetTypes();
            } catch (ReflectionTypeLoadException e) {
                types = e.Types;
            }

            return types.Where(t => t != null);
        }

        internal static void InitPaths() {
            try {
                var settings = GetBepInExUserSettings();
                if (!Directory.Exists(settings.RootFolder)) {
                    Directory.CreateDirectory(settings.RootFolder);
                }
            
                var coreFolder = settings.CoreFolder;
                if (!Directory.Exists(coreFolder)) {
                    Directory.CreateDirectory(coreFolder);
                }
            
                if (!Directory.Exists(settings.PluginsPath)) {
                    Directory.CreateDirectory(settings.PluginsPath);
                }
            
                if (!File.Exists(settings.LocalExePath)) {
                    using (var file = File.Create(settings.LocalExePath)) { }
                }
            } catch (Exception e) {
                Debug.LogError($"Encountered an error while initializing BepInExPreloader paths:\n{e}");
            }
        }
    }
}