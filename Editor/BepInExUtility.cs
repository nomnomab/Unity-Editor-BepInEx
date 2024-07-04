using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEditor;
using Object = UnityEngine.Object;

namespace Nomnom.BepInEx.Editor {
    public static class BepInExUtility {
        public static bool IsValidRef(object obj) {
            if (obj == null) return false;
            if (obj is Object o && !o) return false;
            return true;
        }
        
        public static bool TryRemoveNullsFromArray(object owner, FieldInfo fieldInfo, Func<object, bool> isValid = null) {
            var originalArray = (object[])fieldInfo.GetValue(owner);
            var newArray = originalArray.Where(x => BepInExUtility.IsValidRef(x) && (isValid == null || isValid(x))).ToArray();
            if (originalArray.Length == newArray.Length) {
                return false;
            }

            var typedArray = Array.CreateInstance(originalArray[0].GetType(), newArray.Length);
            newArray.CopyTo(typedArray, 0);
            fieldInfo.SetValue(owner, typedArray);
            return true;
        }
        
        public static bool TryRemoveNullsFromList(object owner, FieldInfo fieldInfo, Func<object, bool> isValid = null) {
            var originalList = (IList)fieldInfo.GetValue(owner);
            var count = originalList.Count;
            for (int i = originalList.Count - 1; i >= 0; i--) {
                var obj = originalList[i];
                if (BepInExUtility.IsValidRef(obj) && (isValid == null || isValid(obj))) {
                    continue;
                }
                
                originalList.RemoveAt(i);
            }
            
            if (originalList.Count == count) {
                return false;
            }
            
            fieldInfo.SetValue(owner, originalList);
            return true;
        }
        
        [MenuItem("Tools/Unity Project Patcher/Configs/" + nameof(BepinexUserSettings))]
        private static void OpenBepinexUserSettings() {
            var config = BepInExPreloader.GetBepInExUserSettings();
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);
        }
    }
}