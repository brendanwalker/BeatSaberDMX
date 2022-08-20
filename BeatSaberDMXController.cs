using CustomAvatar;
using BeatSaberDMX.Utilities;
using MikanXR.SDK.Unity;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zenject;
using BeatSaberDMX.Configuration;

namespace BeatSaberDMX
{
    public class BeatSaberDMXController : MonoBehaviour
    {
        public static readonly string MenuSceneName = "MainMenu";
        public static readonly string PostSongMenuSceneName = "MainMenu";
        public static readonly string GameSceneName = "GameCore";
        public static readonly string HealthWarningSceneName = "HealthWarning";
        public static readonly string EmptyTransitionSceneName = "EmptyTransition";
        public static readonly string CreditsSceneName = "Credits";
        public static readonly string BeatmapEditorSceneName = "BeatmapEditor";
        readonly string[] MainSceneNames = { GameSceneName, CreditsSceneName, BeatmapEditorSceneName };

        public static readonly string GlowShaderName = "BeatSaber/Unlit Glow";
        public static readonly string GlowPropertyName = "_Glow";

        private bool lastMainSceneWasNotMenu = false;

        private GameScenesManager gameScenesManager = null;

        public static BeatSaberDMXController Instance { get; private set; }
        public SaberManager GameSaberManager { get; private set; }
        public VRController LeftVRController { get; private set; }
        public VRController RightVRController { get; private set; }
        public VRIKManager AvatarIKManager { get; private set; }
        public TrackingHelper RoomTrackingUtils { get; private set; }

        public List<Transform> ColorANotes = new List<Transform>();
        public List<Transform> ColorBNotes = new List<Transform>();
        public Color ColorA = Color.red;
        public Color ColorB = Color.blue;

        private void SceneManager_activeSceneChanged(Scene oldScene, Scene newScene)
        {           
            try
            {
                Plugin.Log?.Info($"BeatSaberDMXController: Active scene changed: {oldScene.name} -> {newScene.name}");

                if (oldScene.name == GameSceneName || oldScene.name == MenuSceneName)
                {
                    //MikanClient.Instance.DespawnMikanCamera();
                    //DespawnDMXScene();
                    UnbindSceneComponents();
                }

                if (newScene.name == GameSceneName)
                {
                    //InvokeAll(gameSceneActive);

                    gameScenesManager = Resources.FindObjectsOfTypeAll<GameScenesManager>().FirstOrDefault();

                    if (gameScenesManager != null)
                    {
                        gameScenesManager.transitionDidFinishEvent -= GameSceneLoadedCallback;
                        gameScenesManager.transitionDidFinishEvent += GameSceneLoadedCallback;
                    }
                }
                else if (newScene.name == MenuSceneName)
                {
                    gameScenesManager = Resources.FindObjectsOfTypeAll<GameScenesManager>().FirstOrDefault();

                    //InvokeAll(menuSceneActive);

                    if (gameScenesManager != null)
                    {

                        if (oldScene.name == EmptyTransitionSceneName && !lastMainSceneWasNotMenu)
                        {
                            //     Utilities.Logger.log.Info("Fresh");

                            gameScenesManager.transitionDidFinishEvent -= OnMenuSceneWasLoadedFresh;
                            gameScenesManager.transitionDidFinishEvent += OnMenuSceneWasLoadedFresh;
                        }
                        else
                        {
                            gameScenesManager.transitionDidFinishEvent -= OnMenuSceneWasLoaded;
                            gameScenesManager.transitionDidFinishEvent += OnMenuSceneWasLoaded;
                        }
                    }

                    lastMainSceneWasNotMenu = false;
                }

                if (MainSceneNames.Contains(newScene.name))
                {
                    lastMainSceneWasNotMenu = true;
                }
            }
            catch (Exception e)
            {
                Plugin.Log?.Error(e);
            }
        }

        private void OnMenuSceneWasLoaded(ScenesTransitionSetupDataSO transitionSetupData, DiContainer diContainer)
        {
            gameScenesManager.transitionDidFinishEvent -= OnMenuSceneWasLoaded;
            //InvokeAll(menuSceneLoaded);
        }

        private void OnMenuSceneWasLoadedFresh(ScenesTransitionSetupDataSO transitionSetupData, DiContainer diContainer)
        {
            gameScenesManager.transitionDidFinishEvent -= OnMenuSceneWasLoadedFresh;

            //var levelDetailViewController = Resources.FindObjectsOfTypeAll<StandardLevelDetailViewController>().FirstOrDefault();
            //levelDetailViewController.didChangeDifficultyBeatmapEvent += delegate (StandardLevelDetailViewController vc, IDifficultyBeatmap beatmap) { InvokeAll(difficultySelected, vc, beatmap); };

            //var characteristicSelect = Resources.FindObjectsOfTypeAll<BeatmapCharacteristicSegmentedControlController>().FirstOrDefault();
            //characteristicSelect.didSelectBeatmapCharacteristicEvent += delegate (BeatmapCharacteristicSegmentedControlController controller, BeatmapCharacteristicSO characteristic) { InvokeAll(characteristicSelected, controller, characteristic); };

            //var packSelectViewController = Resources.FindObjectsOfTypeAll<LevelSelectionNavigationController>().FirstOrDefault();
            //packSelectViewController.didSelectLevelPackEvent += delegate (LevelSelectionNavigationController controller, IBeatmapLevelPack pack) { InvokeAll(levelPackSelected, controller, pack); };
            //var levelSelectViewController = Resources.FindObjectsOfTypeAll<LevelCollectionViewController>().FirstOrDefault();
            //levelSelectViewController.didSelectLevelEvent += delegate (LevelCollectionViewController controller, IPreviewBeatmapLevel level) { InvokeAll(levelSelected, controller, level); };

            //InvokeAll(earlyMenuSceneLoadedFresh, transitionSetupData);
            //InvokeAll(menuSceneLoadedFresh);
            //InvokeAll(lateMenuSceneLoadedFresh, transitionSetupData)

            if (BindMenuSceneComponents())
            {
                BeatSaberUtilities bsUtilities = diContainer.Resolve<BeatSaberUtilities>();

                SpawnDMXScene(bsUtilities);
                MikanClient.Instance.SpawnMikanCamera(bsUtilities);

                //if (AvatarIKManager != null)
                //{
                //    PluginUtils.SetMaterialFloatValueRecursive(AvatarIKManager.gameObject, GlowShaderName, GlowPropertyName, 1.0f);
                //}
            }
        }

        private void GameSceneLoadedCallback(ScenesTransitionSetupDataSO transitionSetupData, DiContainer diContainer)
        {
            RoomTrackingUtils = diContainer.Resolve<TrackingHelper>();

            // Prevent firing this event when returning to menu
            var gameScenesManager = Resources.FindObjectsOfTypeAll<GameScenesManager>().FirstOrDefault();
            gameScenesManager.transitionDidFinishEvent -= GameSceneLoadedCallback;

            var pauseManager = diContainer.TryResolve<PauseController>();
            if (pauseManager != null)
            {
                pauseManager.didResumeEvent += PauseManager_didResumeEvent;
                pauseManager.didPauseEvent += PauseManager_didPauseEvent;
            }

            var beatmapObjectManager = diContainer.TryResolve<BeatmapObjectManager>();
            if (beatmapObjectManager != null)
            {
                beatmapObjectManager.noteWasSpawnedEvent += BeatmapObjectManager_noteWasSpawnedEvent;
                beatmapObjectManager.noteWasDespawnedEvent += BeatmapObjectManager_noteWasDespawnedEvent;
                beatmapObjectManager.noteWasCutEvent += BeatmapObjectManager_noteWasCutEvent;
                beatmapObjectManager.noteWasMissedEvent += BeatmapObjectManager_noteWasMissedEvent;
            }

            var scoreController = diContainer.TryResolve<ScoreController>();
            if (scoreController != null)
            {
                scoreController.multiplierDidChangeEvent += ScoreController_multiplierDidChangeEvent;
                scoreController.scoreDidChangeEvent += ScoreController_scoreDidChangeEvent;
            }

            var saberCollisionManager = Resources.FindObjectsOfTypeAll<ObstacleSaberSparkleEffectManager>().LastOrDefault(x => x.isActiveAndEnabled);
            if (saberCollisionManager != null)
            {
                saberCollisionManager.sparkleEffectDidStartEvent += SaberCollisionManager_sparkleEffectDidStartEvent;
                saberCollisionManager.sparkleEffectDidEndEvent += SaberCollisionManager_sparkleEffectDidEndEvent;
            }

            var gameEnergyCounter = Resources.FindObjectsOfTypeAll<GameEnergyCounter>().LastOrDefault(x => x.isActiveAndEnabled);
            if (gameEnergyCounter != null)
            {
                gameEnergyCounter.gameEnergyDidReach0Event += GameEnergyCounter_gameEnergyDidReach0Event;
                gameEnergyCounter.gameEnergyDidChangeEvent += GameEnergyCounter_gameEnergyDidChangeEvent;
            }

            var transitionSetup = Resources.FindObjectsOfTypeAll<StandardLevelScenesTransitionSetupDataSO>().FirstOrDefault();
            if (transitionSetup)
            {
                transitionSetup.didFinishEvent -= TransitionSetup_didFinishEvent;
                transitionSetup.didFinishEvent += TransitionSetup_didFinishEvent;
            }

            if (BindGameSceneComponents())
            {                
                //BeatSaberUtilities bsUtilities = diContainer.Resolve<BeatSaberUtilities>();

                //SpawnDMXScene(bsUtilities);
                //MikanClient.Instance.SpawnMikanCamera(bsUtilities);

                //if (AvatarIKManager != null)
                //{
                //    PluginUtils.SetMaterialFloatValueRecursive(AvatarIKManager.gameObject, GlowShaderName, GlowPropertyName, 1.0f);
                //}
            }
        }

        private void TransitionSetup_didFinishEvent(
            StandardLevelScenesTransitionSetupDataSO setupData, 
            LevelCompletionResults results)
        {
            ColorA = setupData.colorScheme.saberAColor;
            ColorB = setupData.colorScheme.saberBColor;
        }

        private void GameEnergyCounter_gameEnergyDidChangeEvent(float obj)
        {
        }

        private void GameEnergyCounter_gameEnergyDidReach0Event()
        {
        }

        private void SaberCollisionManager_sparkleEffectDidEndEvent(SaberType obj)
        {
        }

        private void SaberCollisionManager_sparkleEffectDidStartEvent(SaberType obj)
        {
        }

        private void PauseManager_didPauseEvent()
        {
        }

        private void PauseManager_didResumeEvent()
        {
        }

        private void ScoreController_scoreDidChangeEvent(int arg1, int arg2)
        {
        }

        private void ScoreController_multiplierDidChangeEvent(int arg1, float arg2)
        {
        }

        private void BeatmapObjectManager_noteWasDespawnedEvent(NoteController noteController)
        {
            if (noteController.noteData.colorType == ColorType.ColorA)
            {
                ColorANotes.Remove(noteController.noteTransform);
            }
            else if (noteController.noteData.colorType == ColorType.ColorB)
            {
                ColorBNotes.Remove(noteController.noteTransform);
            }
        }

        private void BeatmapObjectManager_noteWasMissedEvent(NoteController noteController)
        {
        }

        private void BeatmapObjectManager_noteWasCutEvent(NoteController noteController, in NoteCutInfo noteCutInfo)
        {
        }

        private void BeatmapObjectManager_noteWasSpawnedEvent(NoteController noteController)
        {
            Plugin.Log?.Info("BeatSaberDMXController: Note Spawned");

            //PluginUtils.PrintObjectMaterials(noteController.gameObject);
            PluginUtils.SetMaterialFloatValueRecursive(noteController.gameObject, GlowShaderName, GlowPropertyName, 0.0f);

            if (noteController.noteData.colorType == ColorType.ColorA)
            {
                ColorANotes.Add(noteController.noteTransform);
            }
            else if (noteController.noteData.colorType == ColorType.ColorB)
            {
                ColorBNotes.Add(noteController.noteTransform);
            }
        }

        private void UpdateNotes()
        {
            float nearAlphaDist = PluginConfig.Instance.NoteNearAlphaDist;
            float farAlphaDist = PluginConfig.Instance.NoteFarAlphaDist;

            foreach (Transform noteTransform in ColorANotes)
            {
                if (noteTransform != null)
                {
                    UpdateNoteAlpha(noteTransform, nearAlphaDist, farAlphaDist);
                }
            }

            foreach (Transform noteTransform in ColorBNotes)
            {
                if (noteTransform != null)
                {
                    UpdateNoteAlpha(noteTransform, nearAlphaDist, farAlphaDist);
                }
            }
        }

        private void UpdateNoteAlpha(Transform noteTransform, float nearAlphaDist, float farAlphaDist)
        {
            Vector3 notePosition = noteTransform.position;
            Quaternion noteOrientation = noteTransform.rotation;
            RoomTrackingUtils.ApplyInverseRoomAdjust(ref notePosition, ref noteOrientation);

            if (notePosition.z <= farAlphaDist)
            {
                float newAlpha = Mathf.Clamp01((farAlphaDist - notePosition.z) / (farAlphaDist - nearAlphaDist));

                PluginUtils.SetMaterialFloatValueRecursive(noteTransform.gameObject, GlowShaderName, GlowPropertyName, newAlpha);
            }
        }

        bool BindGameSceneComponents()
        {
            Plugin.Log?.Info("BeatSaberDMXController: Binding Game Scene Components");

            GameObject localPlayerGameCore = GameObject.Find("LocalPlayerGameCore");
            //PluginUtils.PrintObjectTree(localPlayerGameCore, "");
            //PluginUtils.PrintComponents(localPlayerGameCore);
            if (localPlayerGameCore == null)
            {
                Plugin.Log?.Warn("BeatSaberDMXController: Failed to find LocalPlayerGameCore game object, bailing!");
                return false;
            }

            Transform GameOrigin = localPlayerGameCore.transform.Find("Origin");
            //PluginUtils.PrintComponents(GameOrigin?.gameObject);
            if (GameOrigin == null)
            {
                Plugin.Log?.Warn("BeatSaberDMXController: Failed to find Origin transform, bailing!");
                return false;
            }

            GameObject vrGameCore = GameOrigin?.Find("VRGameCore")?.gameObject;
            //PluginUtils.PrintComponents(vrGameCore);
            if (vrGameCore == null)
            {
                Plugin.Log?.Warn("BeatSaberDMXController: Failed to find VRGameCore game object, bailing!");
                return false;
            }

            // Fetch the game saber manager to get the left and right sabers
            GameSaberManager = vrGameCore.GetComponent<SaberManager>();

            AvatarIKManager = FindObjectOfType<VRIKManager>();
            if (AvatarIKManager == null)
            {
                Plugin.Log?.Warn("BeatSaberDMXController: Failed to find VRIKManager");
            }

            return true;
        }

        bool BindMenuSceneComponents()
        {
            Plugin.Log?.Info("BeatSaberDMXController: Binding Menu Scene Components");

            GameObject menuCore = GameObject.Find("MenuCore");
            //GameObject menuCore = PluginUtils.FindGameObjectRecursiveInScene(loadedScene, "MenuCore");

            //PluginUtils.PrintComponents(menuCore);
            if (menuCore == null)
            {
                Plugin.Log?.Warn("BeatSaberDMXController: Failed to find MenuCore game object, bailing!");
                return false;
            }

            Transform GameOrigin = menuCore.transform.Find("Origin");
            //PluginUtils.PrintObjectTree(GameOrigin?.gameObject, "");
            //PluginUtils.PrintComponents(GameOrigin?.gameObject);
            if (GameOrigin == null)
            {
                Plugin.Log?.Warn("BeatSaberDMXController: Failed to find Origin transform, bailing!");
                return false;
            }

            Transform menuControllers = GameOrigin?.Find("MenuControllers");
            //PluginUtils.PrintComponents(menuControllers?.gameObject);
            if (menuControllers == null)
            {
                Plugin.Log?.Warn("BeatSaberDMXController: Failed to find MenuControllers game object, bailing!");
                return false;
            }

            GameObject controllerLeft = menuControllers?.Find("ControllerLeft")?.gameObject;
            //PluginUtils.PrintComponents(controllerLeft?.gameObject);
            if (controllerLeft == null)
            {
                Plugin.Log?.Warn("BeatSaberDMXController: Failed to find ControllerLeft game object, bailing!");
                return false;
            }

            GameObject controllerRight = menuControllers?.Find("ControllerRight")?.gameObject;
            //PluginUtils.PrintComponents(controllerRight);
            if (controllerRight == null)
            {
                Plugin.Log?.Warn("BeatSaberDMXController: Failed to find ControllerRight game object, bailing!");
                return false;
            }

            LeftVRController = controllerLeft.GetComponent<VRController>();
            RightVRController = controllerRight.GetComponent<VRController>();

            AvatarIKManager = FindObjectOfType<VRIKManager>();
            if (AvatarIKManager == null)
            {
                Plugin.Log?.Warn("BeatSaberDMXController: Failed to find VRIKManager");
            }

            return true;
        }

        private void UnbindSceneComponents()
        {
            GameSaberManager = null;
            LeftVRController = null;
            RightVRController = null;
            AvatarIKManager = null;
            ColorANotes.Clear();
            ColorBNotes.Clear();
        }

        void SpawnDMXScene(BeatSaberUtilities bsUtilities)
        {
            if (DmxSceneManager.Instance.SceneInstance == null)
            {
                Plugin.Log?.Info("BeatSaberDMXController: Loading DMX Scene");
                if (bsUtilities != null)
                {
                    DmxSceneManager.Instance.LoadDMXScene(bsUtilities);
                }
                else
                {
                    DmxSceneManager.Instance.LoadDMXScene(null);
                }
            }
            else
            {
                Plugin.Log?.Info("BeatSaberDMXController: Ignoring Spawn DMX Scene request. Already spawned");
            }
        }

        void DespawnDMXScene()
        {
            Plugin.Log?.Info("BeatSaberDMXController: Unloading DMX Scene");
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
                    segmentColor = (GameSaberManager.leftSaber == saber) ? ColorA : ColorB;
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
                Plugin.Log?.Warn($"BeatSaberDMXController: Instance of {GetType().Name} already exists, destroying.");
                GameObject.DestroyImmediate(this);
                return;
            }
            
            GameObject.DontDestroyOnLoad(this); // Don't destroy this object on scene changes
            Instance = this;
            //Plugin.Log?.Debug($"{name}: Awake()");

            SceneManager.activeSceneChanged += SceneManager_activeSceneChanged;
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
            if (GameSaberManager != null)
            {
                UpdateNotes();
            }
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
            SceneManager.activeSceneChanged -= SceneManager_activeSceneChanged;

            //Plugin.Log?.Debug($"BeatSaberDMXController: {name}: OnDestroy()");
            if (Instance == this)
                Instance = null; // This MonoBehaviour is being destroyed, so set the static instance property to null.

        }
        #endregion
    }
}
