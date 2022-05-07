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

        private List<GameObject> DMXGameObjects = new List<GameObject>();

        DMXPixelGrid panel;
        DmxController dmxController;

        private void SceneManager_sceneLoaded(Scene loadedScene, LoadSceneMode loadSceneMode)
        {
            Plugin.Log?.Info($"New Scene Loaded: {loadedScene.name}");

            //if (loadedScene.name == MenuSceneName)
            //{
            //    Plugin.Log?.Warn("[Scene Game Objects]");
            //    PrintObjectTreeInScene(loadedScene);

            //    Plugin.Log?.Info("Binding Devices...");
            //    if (BindMenuSceneComponents(loadedScene))
            //    {
            //        Plugin.Log?.Info("Spawning DMX Game Objects...");
            //        SpawnDMXGameObjects();
            //    }
            //}
            if (loadedScene.name == GameSceneName)
            {
                Plugin.Log?.Warn("[Scene Game Objects]");
                PluginUtils.PrintObjectTreeInScene(loadedScene);

                Plugin.Log?.Info("Binding Devices...");
                if (BindGameSceneComponents(loadedScene))
                {
                    Plugin.Log?.Info("Spawning DMX Game Objects...");
                    SpawnDMXGameObjects();
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
                DespawnDMXGameObjects();
                UnbindSceneComponents();
            }
        }

        bool BindGameSceneComponents(Scene loadedScene)
        {
            GameObject localPlayerGameCore = PluginUtils.FindGameObjectRecursiveInScene(loadedScene, "LocalPlayerGameCore");
            //PrintComponents(localPlayerGameCore);
            if (localPlayerGameCore == null)
            {
                Plugin.Log?.Warn("Failed to find LocalPlayerGameCore game object, bailing!");
                return false;
            }

            GameOrigin = localPlayerGameCore.transform.Find("Origin");
            //PrintComponents(GameOrigin?.gameObject);
            if (GameOrigin == null)
            {
                Plugin.Log?.Warn("Failed to find Origin transform, bailing!");
                return false;
            }

            GameObject vrGameCore = GameOrigin?.Find("VRGameCore")?.gameObject;
            //PrintComponents(vrGameCoreTransform);
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
            //PrintComponents(menuCore);
            if (menuCore == null)
            {
                Plugin.Log?.Warn("Failed to find MenuCore game object, bailing!");
                return false;
            }

            GameOrigin = menuCore.transform.Find("Origin");
            //PrintComponents(GameOrigin?.gameObject);
            if (GameOrigin == null)
            {
                Plugin.Log?.Warn("Failed to find Origin transform, bailing!");
                return false;
            }

            Transform menuControllers = GameOrigin?.Find("MenuControllers");
            //PrintComponents(menuControllers);
            if (menuControllers == null)
            {
                Plugin.Log?.Warn("Failed to find MenuControllers game object, bailing!");
                return false;
            }

            GameObject controllerLeft = menuControllers?.Find("ControllerLeft")?.gameObject;
            //PrintComponents(controllerLeft);
            if (controllerLeft == null)
            {
                Plugin.Log?.Warn("Failed to find ControllerLeft game object, bailing!");
                return false;
            }

            GameObject controllerRight = menuControllers?.Find("ControllerRight")?.gameObject;
            //PrintComponents(controllerRight);
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

        void SpawnDMXGameObjects()
        {
            panel = DMXPixelGrid.InstantateGameObject("LanternPanel");
            panel.SetupPixelGridGeometry(
                    DMXPixelGrid.ePixelGridLayout.VerticalLinesZigZagMirrored,
                    0.61f, 0.111f, 0.23f,
                    8, 14);
            Vector3 pos = GameOrigin.position + new Vector3(-0.58f, 1.56f, 1.00f);
            panel.gameObject.transform.position = pos;
            panel.gameObject.transform.rotation = Quaternion.AngleAxis(-90.0f, Vector3.up);
            GameObject.DontDestroyOnLoad(panel.gameObject);
            DMXGameObjects.Add(panel.gameObject);

            GameObject dmxControllerGameObject = new GameObject(
                "DmxController",
                new System.Type[] { typeof(DmxController) });
            dmxController = dmxControllerGameObject.GetComponent<DmxController>();
            dmxController.useBroadcast = false;
            dmxController.remoteIP = "wled-hanabi.local";
            //dmxController.remoteIP = "10.0.0.251";
            dmxController.fps = 30;
            dmxController.AddDMXDeviceToUniverse(1, panel);
            GameObject.DontDestroyOnLoad(dmxControllerGameObject);
            DMXGameObjects.Add(dmxControllerGameObject);

            Plugin.Log?.Info($"Spawned Lantern at {pos.x},{pos.y},{pos.z}");
        }

        void DespawnDMXGameObjects()
        {
            Plugin.Log?.Info("DespawnDMXGameObjects");
            foreach (GameObject go in DMXGameObjects)
            {
                Destroy(go);
            }
            DMXGameObjects.Clear();
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
