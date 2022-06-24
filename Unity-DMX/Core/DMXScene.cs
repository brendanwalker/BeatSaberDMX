using System;
using System.Collections.Generic;
using System.IO;
using BeatSaberDMX;
using BeatSaberDMX.Configuration;
using Newtonsoft.Json;
using UnityEngine;

public class DMXSceneManager : MonoBehaviour
{
    private static DMXSceneManager _instance = null;
    private DMXSceneInstance _sceneInstance = null;

    public static DMXSceneManager Instance
    {
        get
        {
            return _instance;
        }
    }

    private FileSystemWatcher _filesystemWatcher = null;
    private string _dmxSceneFilePath = "";

    private void Awake()
    {
        // For this particular MonoBehaviour, we only want one instance to exist at any time, so store a reference to it in a static property
        //   and destroy any that are created while one already exists.
        if (_instance != null)
        {
            Plugin.Log?.Warn($"DMXSceneManager: Instance of {GetType().Name} already exists, destroying.");
            GameObject.DestroyImmediate(this);
            return;
        }

        GameObject.DontDestroyOnLoad(this); // Don't destroy this object on scene changes
        _instance = this;
        Plugin.Log?.Debug($"DMXSceneManager: {name}: Awake()");
    }

    public void TryUpdateDMXScenePath()
    {
        if (_dmxSceneFilePath != PluginConfig.Instance.DMXSceneFilePath)
        {
            _dmxSceneFilePath = Path.GetFullPath(PluginConfig.Instance.DMXSceneFilePath);

            if (_filesystemWatcher != null)
            {
                _filesystemWatcher.Dispose();
                _filesystemWatcher = null;
            }

            if (_dmxSceneFilePath.Length > 0 && File.Exists(_dmxSceneFilePath))
            {
                string dmxSceneDirectory = Path.GetDirectoryName(_dmxSceneFilePath);
                string dmxSceneExtension = Path.GetExtension(_dmxSceneFilePath);

                _filesystemWatcher = new FileSystemWatcher(dmxSceneDirectory);
                _filesystemWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size;
                _filesystemWatcher.Changed += OnChanged;
                _filesystemWatcher.Filter = "*"+dmxSceneExtension;
                _filesystemWatcher.IncludeSubdirectories = true;
                _filesystemWatcher.EnableRaisingEvents = true;
            }

            PatchLoadedDMXScene();
        }
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        if (e.ChangeType != WatcherChangeTypes.Changed)
        {
            return;
        }

        if (e.FullPath == _dmxSceneFilePath)
        {
            Plugin.Log?.Info(string.Format("Scene File {0} updated", e.FullPath));
            PatchLoadedDMXScene();
        }
    }

    public void UnloadDMXScene()
    {
        if (_sceneInstance != null)
        {
            _sceneInstance.Dispose();
            _sceneInstance = null;
        }
    }

    public void PatchLoadedDMXScene()
    {
        DMXSceneDefinition sceneDefinition = DMXSceneDefinition.LoadSceneFile(_dmxSceneFilePath);

        if (sceneDefinition != null && _sceneInstance != null)
        {
            _sceneInstance.Patch(sceneDefinition);
        }
    }

    public void LoadDMXScene(Transform gameOrigin)
    {
        UnloadDMXScene();

        DMXSceneDefinition sceneDefinition = DMXSceneDefinition.LoadSceneFile(_dmxSceneFilePath);

        if (sceneDefinition != null)
        {
            _sceneInstance = new DMXSceneInstance();
            _sceneInstance.Initialize(sceneDefinition, gameOrigin);
        }
    }
}

public class DMXSceneInstance
{
    private Transform _gameOrigin = null;
    private DMXSceneDefinition _sceneDefinition= null;
    private Dictionary<string, DMXLantern> _lanterns = new Dictionary<string, DMXLantern>();

    public void Initialize(DMXSceneDefinition sceneDefinition, Transform gameOrigin)
    {
        _gameOrigin = gameOrigin;
        _sceneDefinition = sceneDefinition;

        foreach (DMXLanternDefinition lanternDefinition in sceneDefinition.Lanterns)
        {
            SpawnLanternInstance(lanternDefinition, sceneDefinition.LanternGeometry);
        }
    }

    public void Patch(DMXSceneDefinition sceneDefinition)
    {
        foreach (DMXLanternDefinition lanternDefinition in sceneDefinition.Lanterns)
        { 
            DMXLantern lantern= null;
            if (_lanterns.TryGetValue(lanternDefinition.Name, out lantern))
            {
                // Patch existing lantern
                lantern.Patch(lanternDefinition);
            }
            else
            {
                // Create a new lantern that corresponds to the definition
                SpawnLanternInstance(lanternDefinition, sceneDefinition.LanternGeometry);
            }
        }

        // Delete any lanterns that no longer exist in the definition
        List<string> lanternNames = new List<string>(_lanterns.Keys);
        foreach (string lanternName in lanternNames)
        {
            if (sceneDefinition.Lanterns.FindIndex(x => x.Name == lanternName) == -1)
            {                
                DisposeLanternInstance(_lanterns[lanternName]);
            }
        }
    }

    public void Dispose()
    {
        Plugin.Log?.Info($"Despawned all DMX ");

        foreach (DMXLantern lantern in _lanterns.Values)
        {
            GameObject.Destroy(lantern.gameObject);
        }
        _lanterns.Clear();
    }

    void SpawnLanternInstance(DMXLanternDefinition definition, DMXLanternGeometry geometry)
    {
        if (_lanterns.ContainsKey(definition.Name))
        {
            Plugin.Log?.Info($"Failed to apawned Lantern {definition.Name}, already exists!");
            return;              
        }

        DMXLantern lantern = DMXLantern.InstantateGameObject(definition.Name);
        GameObject.DontDestroyOnLoad(lantern.gameObject);
        
        lantern.SetupPixelGeometry(geometry);
        
        lantern.gameObject.transform.parent = _gameOrigin;
        lantern.SetDMXTransform(definition.Transform);       

        lantern.Controller.useBroadcast = false;
        lantern.Controller.remoteIP = definition.DeviceIP;
        lantern.Controller.startUniverseId = definition.StartUniverse;
        lantern.Controller.fps = 30;
        lantern.Controller.AppendDMXLayout(lantern);

        Plugin.Log?.Info($"Spawned Lantern {definition.Name}");
    }

    void DisposeLanternInstance(DMXLantern lantern)
    {
        Plugin.Log?.Info($"Despawned Lantern {lantern.gameObject.name}");
        _lanterns.Remove(lantern.gameObject.name);
        GameObject.Destroy(lantern.gameObject);
    }
}

public class DMXTransform
{
    public float XPosMeters { get; set; }
    public float YPosMeters { get; set; }
    public float ZPosMeters { get; set; }
    public float YRotationAngle { get; set; }
}

public class DMXLanternGeometry
{
    public float PhysicalRadiusMeters { get; set; }
    public float PhysicalHightMeters { get; set; }

    public int HorizontalPanelPixelCount { get; set; }
    public int VerticalPanelPixelCount { get; set; }
    public int PanelCount { get; set; }
}

public class DMXLanternDefinition
{
    public string Name { get; set; }
    public int StartUniverse { get; set; }
    public string DeviceIP { get; set; }
    public DMXTransform Transform { get; set; }
}

public class DMXSceneDefinition
{
    public DMXTransform Transform { get; set; }
    public DMXLanternGeometry LanternGeometry { get; set; }
    public List<DMXLanternDefinition> Lanterns { get; set; }

    public static DMXSceneDefinition LoadSceneFile(string scenePath)
    {
        DMXSceneDefinition sceneDefinition = null;

        try
        {
            if (scenePath.Length > 0 && File.Exists(scenePath))
            {
                string jsonString = File.ReadAllText(scenePath);
                sceneDefinition = JsonConvert.DeserializeObject<DMXSceneDefinition>(jsonString);
            }
        }
        catch(Exception e)
        {
            Plugin.Log?.Error($"Failed to load/parse scene {scenePath}: {e.Message}");
        }

        return sceneDefinition;
    }
}