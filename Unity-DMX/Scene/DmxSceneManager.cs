﻿using System;
using System.Collections.Generic;
using System.IO;
using BeatSaberDMX;
using BeatSaberDMX.Configuration;
using BeatSaberDMX.Utilities;
using Newtonsoft.Json;
using UnityEngine;

public class DmxSceneManager : MonoBehaviour
{
    private static DmxSceneManager _instance = null;
    private DmxSceneInstance _sceneInstance = null;
    public DmxSceneInstance SceneInstance { 
        get { return _sceneInstance; } 
    }
    private bool _hasSceneFileChanged = false;

    public GameObject VRPlayerRig = null;
    public Transform VRHeadTransform = null;
    public Transform VRLeftHandTransform = null;
    public Transform VRRightHandTransform = null;
    public Transform VRLeftFootTransform = null;
    public Transform VRRightFootTransform = null;
    public Transform VRWaistTransform = null;

    public static DmxSceneManager Instance
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

        TryUpdateDMXScenePath();
    }

    private void Update()
    {
        if (_hasSceneFileChanged)
        {
            PatchLoadedDMXScene();
            _hasSceneFileChanged = false;
        }
    }

    public void TryUpdateDMXScenePath()
    {
        if (PluginConfig.Instance == null)
        {
            return;
        }

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
                _filesystemWatcher.Filter = "*" + dmxSceneExtension;
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
            Plugin.Log?.Info(string.Format("DMXSceneManager: Scene File {0} updated", e.FullPath));
            _hasSceneFileChanged = true;            
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

    public void LoadDMXScene(BeatSaberUtilities bsUtilities)
    {
        UnloadDMXScene();

        DMXSceneDefinition sceneDefinition = DMXSceneDefinition.LoadSceneFile(_dmxSceneFilePath);

        if (sceneDefinition != null)
        {
            _sceneInstance = new DmxSceneInstance();
            _sceneInstance.Initialize(sceneDefinition, bsUtilities);
        }
    }
}
