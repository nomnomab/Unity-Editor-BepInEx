using System.IO;
using UnityEngine;

namespace Nomnom {
    [CreateAssetMenu(fileName = "UPPatcherBepInExUserSettings", menuName = "Unity Project Patcher/BepInEx User Settings")]
    public sealed class BepinexUserSettings: ScriptableObject {
#if UNITY_EDITOR
        public bool LoadProjectPlugins => _loadProjectPlugins;
        
        public string RootFolder => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(LocalExePath), "BepInEx"));
        
        public string CoreFolder => Path.GetFullPath(Path.Combine(RootFolder, "core"));

        public string LocalExePath => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "BepInExGameName", $"BepInExGameName.exenot"));

        public string PluginsPath => Path.GetFullPath(Path.Combine(RootFolder, "plugins"));
        
        public string RouterGameExePath
        {
            get {
                if (!File.Exists(LocalExePath)) {
                    using (var fs = File.Create(LocalExePath)) { }
                }
                return LocalExePath;
            }
        }
#endif
        
        [Header("Project plugins are located in \"Assets > Mods\"")]
        [SerializeField] private bool _loadProjectPlugins = true;
    }
}