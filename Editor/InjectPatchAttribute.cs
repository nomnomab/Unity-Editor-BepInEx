using System;

namespace Nomnom.BepInEx.Editor {
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class InjectPatchAttribute : Attribute {
        public readonly PatchLifetime Lifetime = PatchLifetime.AfterChainloader;

        public InjectPatchAttribute(PatchLifetime lifetime = PatchLifetime.AfterChainloader) {
            Lifetime = lifetime;
        }
    }

    public enum PatchLifetime {
        DuringChainloader = 1,
        AfterChainloader = 2,
        Always = DuringChainloader | AfterChainloader
    }
}