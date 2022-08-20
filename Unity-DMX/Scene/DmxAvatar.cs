using System;
using System.Collections.Generic;
using System.IO;
using BeatSaberDMX;
using BeatSaberDMX.Configuration;
using CustomAvatar;
using Newtonsoft.Json;
using Unity.Collections;
using UnityEngine;


public class DmxAvatarDefinition
{
    public string Name { get; set; }
    public string AssetBundlePath { get; set; }
    public string AssetName { get; set; }
    public DmxTransform Transform { get; set; }
    public bool SpawnAsset { get; set; }
    public Dictionary<string, float> BlendShapeWeights { get; set; }
}

public class DmxAvatarInstance : MonoBehaviour
{
    private GameObject _ikRigGameObject = null;

    private SkinnedMeshRenderer _skinnedMeshRenderer = null;

    // VR Player Rig
    private GameObject _vrPlayerRig = null;

    // Prefab IK Controls
    private GameObject _headTarget = null;
    private GameObject _leftHandTarget = null;
    private GameObject _rightHandTarget = null;
    private GameObject _leftLegTarget = null;
    private GameObject _rightLegTarget = null;
    private GameObject _pelvisTarget = null;

    public static DmxAvatarInstance SpawnInstance(DmxAvatarDefinition avatarDefinition, Transform gameOrigin)
    {
        // Create the parent GameObject with the DmxAvatarInstance control script
        List<System.Type> componentTypes = new List<System.Type>();
        componentTypes.Add(typeof(DmxAvatarInstance));
        GameObject avatarGameObject = new GameObject(avatarDefinition.Name, componentTypes.ToArray());
        DmxAvatarInstance avatarInstance = avatarGameObject.GetComponent<DmxAvatarInstance>();

        if (avatarDefinition.SpawnAsset)
        {
            // Load and spawn the avatar prefab asset first
            GameObject _ikRigGameObject = LoadAssetGameObject(avatarDefinition);
            if (_ikRigGameObject != null)
            {
                // Attach the loaded prefab to the DmxAvatarInstance GameObject
                avatarInstance._ikRigGameObject = _ikRigGameObject;
                _ikRigGameObject.transform.parent = avatarGameObject.transform;

                // Attach the DmxAvatarInstance GameObject to the game origin
                avatarInstance.gameObject.transform.parent = gameOrigin;
            }

            // Attach to all of the avatar controls on the avatar prefab
            avatarInstance.BindIKControls();

            // Attach VR player rig game object to the DmxAvatarInstance GameObject
            // (puts VR player rig in same coordinate space as avatar prefab for IK purposes)
            avatarInstance.BindVRPlayerRig();

            // Find the skinned mesh renderer on the spawned _ikRigGameObject
            avatarInstance.BindSkinnedMeshRenderer();
        }
        else
        {
            // Find the first instance of a GameObject with a VRIKManager component
            avatarInstance.TryBindIkRigGameObject();

            // Find the skinned mesh renderer on the found _ikRigGameObject
            avatarInstance.BindSkinnedMeshRenderer();
        }

        // Apply blend shape weights defined in the definition
        avatarInstance.ApplyBlendShapeWeights(avatarDefinition.BlendShapeWeights);

        // Set the game origin relative transform of the DmxAvatarInstance GameObject
        avatarInstance.SetDMXTransform(avatarDefinition.Transform);

        return avatarInstance;
    }

    private void BindVRPlayerRig()
    {
        _vrPlayerRig = DmxSceneManager.Instance.VRPlayerRig;
    }

    private void BindIKControls()
    {
        if (_ikRigGameObject != null)
        {
            _headTarget = PluginUtils.FindGameObjectRecursive(_ikRigGameObject, "HeadTarget");
            _leftHandTarget = PluginUtils.FindGameObjectRecursive(_ikRigGameObject, "LeftHandTarget");
            _rightHandTarget = PluginUtils.FindGameObjectRecursive(_ikRigGameObject, "RightHandTarget");
            _leftLegTarget = PluginUtils.FindGameObjectRecursive(_ikRigGameObject, "LeftLegTarget");
            _rightLegTarget = PluginUtils.FindGameObjectRecursive(_ikRigGameObject, "RightLegTarget");
            _pelvisTarget = PluginUtils.FindGameObjectRecursive(_ikRigGameObject, "PelvisTarget");
        }
    }

    public void TryBindIkRigGameObject()
    {
        if (_ikRigGameObject == null)
        {
            VRIKManager avatarIkManager = FindObjectOfType<VRIKManager>();

            if (avatarIkManager != null)
            {
                _ikRigGameObject = avatarIkManager.gameObject;
            }
        }
    }

    public void BindSkinnedMeshRenderer()
    {
        if (_skinnedMeshRenderer == null && _ikRigGameObject != null)
        {
            _skinnedMeshRenderer = _ikRigGameObject.GetComponentInChildren<SkinnedMeshRenderer>();
        }
    }

    private void ApplyBlendShapeWeights(Dictionary<string, float> blendShapeWeights)
    {
        if (_skinnedMeshRenderer != null)
        {
            Mesh mesh = _skinnedMeshRenderer.sharedMesh;
            for (int blendShapeIndex = 0; blendShapeIndex < mesh.blendShapeCount; ++blendShapeIndex)
            {
                string blendShapeName = mesh.GetBlendShapeName(blendShapeIndex);

                float newUnitWeight = 0;
                if (blendShapeWeights.TryGetValue(blendShapeName, out newUnitWeight))
                {
                    float newWeight = Math.Max(Math.Min(newUnitWeight * 100.0f, 100.0f), 0.0f);
                    float currentWeight = _skinnedMeshRenderer.GetBlendShapeWeight(blendShapeIndex);

                    if (Math.Abs(currentWeight - newWeight) > 0.1f)
                    {
                        _skinnedMeshRenderer.SetBlendShapeWeight(blendShapeIndex, newWeight);
                    }
                }
            }
        }
    }

    private void UpdateAvatarIKTargets()
    {
        if (_vrPlayerRig == null)
            return;
        
        if (DmxSceneManager.Instance.VRHeadTransform != null && _headTarget != null)
        {
            _headTarget.transform.localPosition = DmxSceneManager.Instance.VRHeadTransform.localPosition;
        }

        if (DmxSceneManager.Instance.VRLeftHandTransform != null && _leftHandTarget != null)
        {
            _leftHandTarget.transform.localPosition = DmxSceneManager.Instance.VRLeftHandTransform.localPosition;
        }

        if (DmxSceneManager.Instance.VRRightHandTransform != null && _rightHandTarget != null)
        {
            _rightHandTarget.transform.localPosition = DmxSceneManager.Instance.VRRightHandTransform.localPosition;
        }

        if (DmxSceneManager.Instance.VRLeftFootTransform != null && _leftLegTarget != null)
        {
            _leftLegTarget.transform.localPosition = DmxSceneManager.Instance.VRLeftFootTransform.localPosition;
        }

        if (DmxSceneManager.Instance.VRRightFootTransform != null && _rightLegTarget != null)
        {
            _rightLegTarget.transform.localPosition = DmxSceneManager.Instance.VRRightFootTransform.localPosition;
        }

        if (DmxSceneManager.Instance.VRWaistTransform != null && _pelvisTarget != null)
        {
            _pelvisTarget.transform.localPosition = DmxSceneManager.Instance.VRWaistTransform.localPosition;
        }
    }

    private void Update()
    {
        UpdateAvatarIKTargets();
    }

    private static GameObject LoadAssetGameObject(DmxAvatarDefinition avatarDefinition)
    {
        GameObject avatarGameObject = null;

        var avatarAssetBundle = AssetBundle.LoadFromFile(avatarDefinition.AssetBundlePath);
        if (avatarAssetBundle != null)
        {
            var prefab = avatarAssetBundle.LoadAsset<GameObject>(avatarDefinition.AssetName);
            if (prefab != null)
            {
                var gameObject = GameObject.Instantiate(prefab);
                if (gameObject != null)
                {
                    avatarGameObject = gameObject;
                }
                else
                {
                    Debug.Log("Failed to instantiate prefab from asset");
                }
            }
            else
            {
                Debug.Log("Failed to load asset from bundle!");
            }

            avatarAssetBundle.Unload(false);
        }
        else
        {
            Debug.Log("Failed to load AssetBundle!");
        }

        return avatarGameObject;
    }

    public virtual void Patch(DmxAvatarDefinition avatarDefinition)
    {
        ApplyBlendShapeWeights(avatarDefinition.BlendShapeWeights);
        SetDMXTransform(avatarDefinition.Transform);
    }

    public void SetDMXTransform(DmxTransform transform)
    {
        gameObject.transform.localPosition =
            new Vector3(
                transform.XPosMeters,
                transform.YPosMeters,
                transform.ZPosMeters);
        gameObject.transform.localRotation =
            Quaternion.AngleAxis(
                transform.YRotationAngle,
                Vector3.up);
    }
}