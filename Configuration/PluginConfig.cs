using System.Runtime.CompilerServices;
using IPA.Config.Stores;

[assembly: InternalsVisibleTo(GeneratedStore.AssemblyVisibilityTarget)]
namespace BeatSaberDMX.Configuration
{
    internal class PluginConfig
    {
        public static PluginConfig Instance { get; set; }

        // Must be 'virtual' if you want BSIPA to detect a value change and save the config automatically.
        public virtual float SaberPaintRadius { get; set; } = 0.05f;
        public virtual float SaberPaintDecayRate { get; set; } = 2.0f;
        public virtual string DMXSceneFilePath { get; set; } = "";

        /// <summary>
        /// This is called whenever BSIPA reads the config from disk (including when file changes are detected).
        /// </summary>
        public virtual void OnReload()
        {
            if (DMXSceneManager.Instance != null)
            {
                DMXSceneManager.Instance.PatchLoadedDMXScene();
            }
        }

        /// <summary>
        /// Call this to force BSIPA to update the config file. This is also called by BSIPA if it detects the file was modified.
        /// </summary>
        public virtual void Changed()
        {
            if (DMXSceneManager.Instance != null)
            {
                DMXSceneManager.Instance.PatchLoadedDMXScene();
            }
        }

        /// <summary>
        /// Call this to have BSIPA copy the values from <paramref name="other"/> into this config.
        /// </summary>
        public virtual void CopyFrom(PluginConfig other)
        {
            // This instance's members populated from other
        }
    }
}