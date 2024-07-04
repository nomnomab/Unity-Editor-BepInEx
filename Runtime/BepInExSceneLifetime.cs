using System;
using UnityEngine;

namespace Nomnom {
    [DisallowMultipleComponent]
    public sealed class BepInExSceneLifetime: MonoBehaviour {
        private Action _callback;
        
        public void Init(Action callback) {
            _callback = callback;
        }
        
        private void OnDestroy() {
            _callback?.Invoke();
        }
    }
}