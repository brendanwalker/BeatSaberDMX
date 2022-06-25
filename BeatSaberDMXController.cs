using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BeatSaberDMX
{
    public class BeatSaberDMXController : MonoBehaviour
    {
        private static String GameSceneName = "StandardGameplay";
        private static String MenuSceneName = "MainMenu";

        public static BeatSaberDMXController Instance { get; private set; }
        public SaberManager GameSaberManager { get; private set; }
        public Transform GameOrigin { get; private set; }
        public VRController LeftVRController { get; private set; }
        public VRController RightVRController { get; private set; }

        private void SceneManager_sceneLoaded(Scene loadedScene, LoadSceneMode loadSceneMode)
        {
            Plugin.Log?.Info($"New Scene Loaded: {loadedScene.name}");

            if (loadedScene.name == GameSceneName)
            {
                Plugin.Log?.Warn("[Scene Game Objects]");
                PluginUtils.PrintObjectTreeInScene(loadedScene);

                Plugin.Log?.Info("Binding Devices...");
                if (BindGameSceneComponents(loadedScene))
                {
                    Plugin.Log?.Info("Spawning DMX Game Objects...");
                    SpawnDMXScene();
                }
            }
            else
            {
                Plugin.Log?.Info($"  ignoring scene");
            }
        }
        private void SceneManager_sceneUnloaded(Scene unloadedScene)
        {
            Plugin.Log?.Info($"Unloading scene {unloadedScene.name}");

            if (unloadedScene.name == GameSceneName || unloadedScene.name == MenuSceneName)
            {
                DespawnDMXScene();
                UnbindSceneComponents();
            }
        }

        bool BindGameSceneComponents(Scene loadedScene)
        {
            GameObject localPlayerGameCore = PluginUtils.FindGameObjectRecursiveInScene(loadedScene, "LocalPlayerGameCore");
            //PluginUtils.PrintComponents(localPlayerGameCore);
            if (localPlayerGameCore == null)
            {
                Plugin.Log?.Warn("Failed to find LocalPlayerGameCore game object, bailing!");
                return false;
            }

            GameOrigin = localPlayerGameCore.transform.Find("Origin");
            //PluginUtils.PrintComponents(GameOrigin?.gameObject);
            if (GameOrigin == null)
            {
                Plugin.Log?.Warn("Failed to find Origin transform, bailing!");
                return false;
            }

            GameObject vrGameCore = GameOrigin?.Find("VRGameCore")?.gameObject;
            //PluginUtils.PrintComponents(vrGameCoreTransform);
            if (vrGameCore == null)
            {
                Plugin.Log?.Warn("Failed to find VRGameCore game object, bailing!");
                return false;
            }

            // Fetch the game saber manager to get the left and right sabers
            GameSaberManager = vrGameCore.GetComponent<SaberManager>();

            Plugin.Log?.Info("Successfully bound game components!");
            return true;
        }

        bool BindMenuSceneComponents(Scene loadedScene)
        {
            GameObject menuCore = PluginUtils.FindGameObjectRecursiveInScene(loadedScene, "MenuCore");
            //PluginUtils.PrintComponents(menuCore);
            if (menuCore == null)
            {
                Plugin.Log?.Warn("Failed to find MenuCore game object, bailing!");
                return false;
            }

            GameOrigin = menuCore.transform.Find("Origin");
            //PluginUtils.PrintComponents(GameOrigin?.gameObject);
            if (GameOrigin == null)
            {
                Plugin.Log?.Warn("Failed to find Origin transform, bailing!");
                return false;
            }

            Transform menuControllers = GameOrigin?.Find("MenuControllers");
            //PluginUtils.PrintComponents(menuControllers);
            if (menuControllers == null)
            {
                Plugin.Log?.Warn("Failed to find MenuControllers game object, bailing!");
                return false;
            }

            GameObject controllerLeft = menuControllers?.Find("ControllerLeft")?.gameObject;
            //PluginUtils.PrintComponents(controllerLeft);
            if (controllerLeft == null)
            {
                Plugin.Log?.Warn("Failed to find ControllerLeft game object, bailing!");
                return false;
            }

            GameObject controllerRight = menuControllers?.Find("ControllerRight")?.gameObject;
            //PluginUtils.PrintComponents(controllerRight);
            if (controllerRight == null)
            {
                Plugin.Log?.Warn("Failed to find ControllerRight game object, bailing!");
                return false;
            }

            LeftVRController = controllerLeft.GetComponent<VRController>();
            RightVRController = controllerRight.GetComponent<VRController>();

            Plugin.Log?.Info("Successfully bound menu components!");
            return true;
        }

        private void UnbindSceneComponents()
        {
            GameSaberManager = null;
            GameOrigin = null;
            LeftVRController = null;
            RightVRController = null;
        }

        void SpawnDMXScene()
        {
            Plugin.Log?.Warn("[Loading DMX Scene]");
            DmxSceneManager.Instance.LoadDMXScene(GameOrigin);
        }

        void DespawnDMXScene()
        {
            Plugin.Log?.Warn("[Unloading DMX Scene]");
            DmxSceneManager.Instance.UnloadDMXScene();
        }

        public bool GetLedInteractionSegment(
            GameObject overlappingGameObject, 
            out Vector3 segmentStart, 
            out Vector3 segmentEnd, 
            out Color32 segmentColor)
        {
            segmentStart = Vector3.zero;
            segmentEnd = Vector3.zero;
            segmentColor = Color.white;

            if (GameSaberManager != null)
            {
                Saber saber = overlappingGameObject.GetComponent<Saber>();
                if (saber != null)
                {
                    segmentColor =
                        (GameSaberManager.leftSaber == saber)
                        ? new Color32(255, 0, 0, 255)
                        : new Color32(0, 0, 255, 255);

                    segmentStart = saber.saberBladeBottomPos;
                    segmentEnd = saber.saberBladeTopPos;

                    //Plugin.Log?.Warn($"{overlappingGameObject.name} start {segmentStart.x},{segmentStart.y},{segmentStart.z}");
                    //Plugin.Log?.Warn($"{overlappingGameObject.name} end {segmentEnd.x},{segmentEnd.y},{segmentEnd.z}");

                    return true;
                }
            }

            return false;
        }

        // These methods are automatically called by Unity, you should remove any you aren't using.
        #region Monobehaviour Messages
        /// <summary>
        /// Only ever called once, mainly used to initialize variables.
        /// </summary>
        private void Awake()
        {
            // For this particular MonoBehaviour, we only want one instance to exist at any time, so store a reference to it in a static property
            //   and destroy any that are created while one already exists.
            if (Instance != null)
            {
                Plugin.Log?.Warn($"Instance of {GetType().Name} already exists, destroying.");
                GameObject.DestroyImmediate(this);
                return;
            }
            
            GameObject.DontDestroyOnLoad(this); // Don't destroy this object on scene changes
            Instance = this;
            Plugin.Log?.Debug($"{name}: Awake()");

            SceneManager.sceneLoaded += SceneManager_sceneLoaded;
            SceneManager.sceneUnloaded += SceneManager_sceneUnloaded;
        }

        /// <summary>
        /// Only ever called once on the first frame the script is Enabled. Start is called after any other script's Awake() and before Update().
        /// </summary>
        private void Start()
        {
        }

        /// <summary>
        /// Called every frame if the script is enabled.
        /// </summary>
        private void Update()
        {
        }

        /// <summary>
        /// Called every frame after every other enabled script's Update().
        /// </summary>
        private void LateUpdate()
        {

        }

        /// <summary>
        /// Called when the script becomes enabled and active
        /// </summary>
        private void OnEnable()
        {

        }

        /// <summary>
        /// Called when the script becomes disabled or when it is being destroyed.
        /// </summary>
        private void OnDisable()
        {

        }

        /// <summary>
        /// Called when the script is being destroyed.
        /// </summary>
        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= SceneManager_sceneLoaded;
            SceneManager.sceneUnloaded -= SceneManager_sceneUnloaded;

            Plugin.Log?.Debug($"{name}: OnDestroy()");
            if (Instance == this)
                Instance = null; // This MonoBehaviour is being destroyed, so set the static instance property to null.

        }
        #endregion
    }
}
