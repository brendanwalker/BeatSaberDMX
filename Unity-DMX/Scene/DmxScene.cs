using System;
using System.Collections.Generic;
using System.IO;
using BeatSaberDMX;
using BeatSaberDMX.Configuration;
using Newtonsoft.Json;
using UnityEngine;

public class DMXSceneDefinition
{
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
            Plugin.Log?.Error($"Failed to load/parse scene {scenePath}: {e.Message}");
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
            Plugin.Log?.Error($"Failed to save scene {scenePath}: {e.Message}");
        }

        return bSuccess;
    }
}

public class DmxSceneInstance
{
    private Transform _gameOrigin = null;
    private Dictionary<string, DmxLayoutDefinition> _layoutDefinitions = new Dictionary<string, DmxLayoutDefinition>();
    private Dictionary<string, DmxLayoutInstance> _layoutInstances = new Dictionary<string, DmxLayoutInstance>();

    public void Initialize(DMXSceneDefinition sceneDefinition, Transform gameOrigin)
    {
        _gameOrigin = gameOrigin;

        RebuildLayoutDefinitions(sceneDefinition);

        // Create an instance for each definition
        foreach (DmxLayoutDefinition layoutDefinition in _layoutDefinitions.Values)
        {
            SpawnLayoutInstance(layoutDefinition);
        }
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

    public void Patch(DMXSceneDefinition sceneDefinition)
    {
        RebuildLayoutDefinitions(sceneDefinition);

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
    }

    public void Dispose()
    {
        foreach (DmxLanternLayoutInstance instance in _layoutInstances.Values)
        {
            Plugin.Log?.Info($"Despawned DMX instance {instance.gameObject}");
            GameObject.Destroy(instance.gameObject);
        }
        _layoutInstances.Clear();
        _layoutDefinitions.Clear();
    }

    void SpawnLayoutInstance(DmxLayoutDefinition definition)
    {
        if (_layoutInstances.ContainsKey(definition.Name))
        {
            Plugin.Log?.Info($"Failed to spawn instance for {definition.Name}, already exists!");
            return;
        }

        DmxLayoutInstance instance = null;

        if (definition is DmxLanternLayoutDefinition)
        {
            instance = DmxLanternLayoutInstance.SpawnInstance((DmxLanternLayoutDefinition)definition, _gameOrigin);
        }
        else if (definition is DmxGridLayoutDefinition)
        {
            instance = DmxGridLayoutInstance.SpawnInstance((DmxGridLayoutDefinition)definition, _gameOrigin);
        }

        if (instance != null)
        {
            Plugin.Log?.Info($"Spawned instance for {definition.Name}");
            _layoutInstances.Add(definition.Name, instance);
        }
    }

    void DespawnLayoutInstance(DmxLayoutInstance instance)
    {
        Plugin.Log?.Info($"Despawned Lantern {instance.gameObject.name}");
        _layoutInstances.Remove(instance.gameObject.name);
        GameObject.Destroy(instance.gameObject);
    }
}