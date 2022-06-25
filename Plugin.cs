using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using IPA;
using IPA.Config;
using IPA.Config.Stores;
using UnityEngine;
using IPALogger = IPA.Logging.Logger;
using MikanXR.SDK.Unity;

namespace BeatSaberDMX
{
    [Plugin(RuntimeOptions.DynamicInit)]
    public class Plugin
    {
        public static string HarmonyId = "com.BrendanWalker.beatsaber.BeatSaberDMX";

        public string Name => "BeatSaberDMX";
        public string Version => "0.0.1";

        // TODO: If using Harmony, uncomment and change YourGitHub to the name of your GitHub account, or use the form "com.company.project.product"
        //       You must also add a reference to the Harmony assembly in the Libs folder.
        // public const string HarmonyId = "com.github.YourGitHub.BeatSaberDMX";
        // internal static readonly HarmonyLib.Harmony harmony = new HarmonyLib.Harmony(HarmonyId);

        internal static Plugin Instance { get; private set; }
        internal static IPALogger Log { get; private set; }
        internal static BeatSaberDMXController PluginController { get { return BeatSaberDMXController.Instance; } }
        internal static MikanClient MikanClientInstance { get { return MikanClient.Instance; } }

        internal static Harmony HarmonyInstance { get; private set; }

        [Init]
        /// <summary>
        /// Called when the plugin is first loaded by IPA (either when the game starts or when the plugin is enabled if it starts disabled).
        /// [Init] methods that use a Constructor or called before regular methods like InitWithConfig.
        /// Only use [Init] with one Constructor.
        /// </summary>
        public Plugin(IPALogger logger)
        {
            Instance = this;
            Plugin.Log = logger;
            Plugin.Log?.Debug("Logger initialized.");

            HarmonyInstance = new Harmony(HarmonyId);
        }

        #region BSIPA Config
        [Init]
        public void InitWithConfig(Config conf)
        {
            Configuration.PluginConfig.Instance = conf.Generated<Configuration.PluginConfig>();
            Plugin.Log?.Debug("Config loaded");
        }
        #endregion


        /// <summary>
        /// Called when the plugin is enabled (including when the game starts if the plugin is enabled).
        /// </summary>
        [OnEnable]
        public void OnEnable()
        {
            GameObject gameObject = new GameObject("BeatSaberDMXPlugin");
            gameObject.AddComponent<BeatSaberDMXController>();
            gameObject.AddComponent<MikanClient>();
            gameObject.AddComponent<DmxSceneManager>();
            ApplyHarmonyPatches();
        }

        /// <summary>
        /// Called when the plugin is disabled and on Beat Saber quit. It is important to clean up any Harmony patches, GameObjects, and Monobehaviours here.
        /// The game should be left in a state as if the plugin was never started.
        /// Methods marked [OnDisable] must return void or Task.
        /// </summary>
        [OnDisable]
        public void OnDisable()
        {
            if (PluginController != null)
                GameObject.Destroy(PluginController);
            if (MikanClientInstance != null)
                GameObject.Destroy(MikanClientInstance);
            RemoveHarmonyPatches();
        }

        /// <summary>
        /// Attempts to apply all the Harmony patches in this assembly.
        /// </summary>
        internal static void ApplyHarmonyPatches()
        {
            try
            {
                Plugin.Log?.Debug("Applying Harmony patches.");
                HarmonyInstance.PatchAll(Assembly.GetExecutingAssembly());
            }
            catch (Exception ex)
            {
                Plugin.Log?.Error("Error applying Harmony patches: " + ex.Message);
                Plugin.Log?.Debug(ex);
            }
        }

        /// <summary>
        /// Attempts to remove all the Harmony patches that used our HarmonyId.
        /// </summary>
        internal static void RemoveHarmonyPatches()
        {
            try
            {
                // Removes all patches with this HarmonyId
                HarmonyInstance.UnpatchAll(HarmonyId);
            }
            catch (Exception ex)
            {
                Plugin.Log?.Error("Error removing Harmony patches: " + ex.Message);
                Plugin.Log?.Debug(ex);
            }
        }
    }
}
