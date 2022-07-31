using System;
using System.Collections.Generic;
using System.IO;
using BeatSaberDMX;
using BeatSaberDMX.Configuration;
using BeatSaberDMX.Utilities;
using Newtonsoft.Json;
using UnityEngine;

public class DMXSceneDefinition
{
    public bool IsVisible { get; set; }
    public DmxTransform SceneTransform { get; set; }
    public List<DmxLanternLayoutDefinition> LanternDefinitions { get; set; }
    public List<DmxGridLayoutDefinition> GridDefinitions { get; set; }

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
        catch (Exception e)
        {
            Plugin.Log?.Error($"DMXSceneDefinition: Failed to load/parse scene {scenePath}: {e.Message}");
        }

        return sceneDefinition;
    }

    public bool SaveSceneFile(string scenePath)
    {
        bool bSuccess = false;

        try
        {
            if (scenePath.Length > 0)
            {
                string jsonString = JsonConvert.SerializeObject(this);
                File.WriteAllText(scenePath, jsonString);
                bSuccess = true;
            }
        }
        catch (Exception e)
        {
            Plugin.Log?.Error($"DMXSceneDefinition: Failed to save scene {scenePath}: {e.Message}");
        }

        return bSuccess;
    }
}

public class DmxSceneInstance
{
    private bool _isVisible = true;
    private BeatSaberUtilities _bsUtilities = null;
    private GameObject _roomOrigin = null;
    private GameObject _sceneOrigin = null;
    private Dictionary<string, DmxLayoutDefinition> _layoutDefinitions = new Dictionary<string, DmxLayoutDefinition>();
    private Dictionary<string, DmxLayoutInstance> _layoutInstances = new Dictionary<string, DmxLayoutInstance>();

    public void Initialize(DMXSceneDefinition sceneDefinition, BeatSaberUtilities bsUtilities)
    {
        _roomOrigin = new GameObject("DMXRoomOrigin");

        if (bsUtilities != null)
        {
            _roomOrigin.transform.position = bsUtilities.roomCenter;
            _roomOrigin.transform.rotation = bsUtilities.roomRotation;

            _bsUtilities = bsUtilities;
            _bsUtilities.roomAdjustChanged += OnRoomOriginChanged;
        }

        RebuildLayoutDefinitions(sceneDefinition);

        // Create a scene root to attach all instances to
        _sceneOrigin = new GameObject("DmxSceneRoot");
        _sceneOrigin.transform.parent = _roomOrigin.transform;
        SetDMXTransform(sceneDefinition.SceneTransform);

        // Create an instance for each definition
        foreach (DmxLayoutDefinition layoutDefinition in _layoutDefinitions.Values)
        {
            SpawnLayoutInstance(layoutDefinition);
        }

        // Update scene visibility after everything is spawned
        SetSceneVisibility(sceneDefinition.IsVisible);            
    }

    public void SetSceneVisibility(bool bNewIsVisible)
    {
        Renderer[] renderers = _sceneOrigin.GetComponentsInChildren<Renderer>();
        foreach (Renderer childRenderer in renderers)
        {
            childRenderer.enabled = bNewIsVisible;
        }

        _isVisible = bNewIsVisible;
    }

    public void SetDMXTransform(DmxTransform transform)
    {
        _sceneOrigin.transform.localPosition =
            new Vector3(
                transform.XPosMeters,
                transform.YPosMeters,
                transform.ZPosMeters);
        _sceneOrigin.transform.localRotation =
            Quaternion.AngleAxis(
                transform.YRotationAngle,
                Vector3.up);
    }

    private void RebuildLayoutDefinitions(DMXSceneDefinition sceneDefinition)
    {
        _layoutDefinitions = new Dictionary<string, DmxLayoutDefinition>();

        foreach (DmxLayoutDefinition lanternDefinition in sceneDefinition.LanternDefinitions)
        {
            _layoutDefinitions.Add(lanternDefinition.Name, lanternDefinition);
        }

        foreach (DmxLayoutDefinition gridDefinition in sceneDefinition.GridDefinitions)
        {
            _layoutDefinitions.Add(gridDefinition.Name, gridDefinition);
        }
    }

    public void OnRoomOriginChanged(Vector3 roomCenter, Quaternion roomRotation)
    {
        if (_roomOrigin != null)
        {
            _roomOrigin.transform.position = roomCenter;
            _roomOrigin.transform.rotation = roomRotation;
        }
    }

    public void Patch(DMXSceneDefinition sceneDefinition)
    {
        RebuildLayoutDefinitions(sceneDefinition);

        SetDMXTransform(sceneDefinition.SceneTransform);

        foreach (DmxLayoutDefinition layoutDefinition in _layoutDefinitions.Values)
        {
            DmxLayoutInstance layoutInstance = null;
            if (_layoutInstances.TryGetValue(layoutDefinition.Name, out layoutInstance))
            {
                // Patch existing instance
                layoutInstance.Patch(layoutDefinition);
            }
            else
            {
                // Create a new instance that corresponds to the definition
                SpawnLayoutInstance(layoutDefinition);
            }
        }

        // Delete any instances that no longer have a corresponding definition
        List<string> instanceNames = new List<string>(_layoutInstances.Keys);
        foreach (string instanceName in instanceNames)
        {
            if (!_layoutDefinitions.ContainsKey(instanceName))
            {
                DespawnLayoutInstance(_layoutInstances[instanceName]);
            }
        }

        // Update scene visibility after everything is spawned / despawned
        SetSceneVisibility(sceneDefinition.IsVisible);
    }

    public void Dispose()
    {
        if (_bsUtilities != null)
        {
            _bsUtilities.roomAdjustChanged -= OnRoomOriginChanged;
            _bsUtilities = null;
        }

        foreach (DmxLanternLayoutInstance instance in _layoutInstances.Values)
        {
            Plugin.Log?.Info($"DmxSceneInstance: Despawned DMX instance {instance.gameObject}");
            GameObject.Destroy(instance.gameObject);
        }
        _layoutInstances.Clear();
        _layoutDefinitions.Clear();

        GameObject.Destroy(_sceneOrigin);
        _sceneOrigin = null;
    }

    void SpawnLayoutInstance(DmxLayoutDefinition definition)
    {
        if (_layoutInstances.ContainsKey(definition.Name))
        {
            Plugin.Log?.Info($"DmxSceneInstance: Failed to spawn instance for {definition.Name}, already exists!");
            return;
        }

        DmxLayoutInstance instance = null;

        if (definition is DmxLanternLayoutDefinition)
        {
            instance = DmxLanternLayoutInstance.SpawnInstance((DmxLanternLayoutDefinition)definition, _sceneOrigin.transform);
        }
        else if (definition is DmxGridLayoutDefinition)
        {
            instance = DmxGridLayoutInstance.SpawnInstance((DmxGridLayoutDefinition)definition, _sceneOrigin.transform);
        }

        if (instance != null)
        {
            Plugin.Log?.Info($"DmxSceneInstance: Spawned instance for {definition.Name}");
            _layoutInstances.Add(definition.Name, instance);
        }
    }

    void DespawnLayoutInstance(DmxLayoutInstance instance)
    {
        Plugin.Log?.Info($"DmxSceneInstance: Despawned Lantern {instance.gameObject.name}");
        _layoutInstances.Remove(instance.gameObject.name);
        GameObject.Destroy(instance.gameObject);
    }
}