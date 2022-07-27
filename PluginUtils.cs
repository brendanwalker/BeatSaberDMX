using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BeatSaberDMX
{
    public static class PluginUtils
    {
        public static GameObject FindGameObjectRecursiveInScene(Scene loadedScene, string objectNameToFind)
        {
            var rootGameObjects = loadedScene.GetRootGameObjects();

            foreach (var gameObject in rootGameObjects)
            {
                GameObject foundGameObject = FindGameObjectRecursive(gameObject, objectNameToFind);

                if (foundGameObject != null)
                {
                    return foundGameObject;
                }
            }

            return null;
        }

        public static GameObject FindGameObjectRecursive(GameObject gameObject, string objectNameToFind)
        {
            if (gameObject == null)
                return null;

            if (gameObject.name == objectNameToFind)
            {
                return gameObject;
            }

            for (int childIndex = 0; childIndex < gameObject.transform.childCount; ++childIndex)
            {
                var childTransform = gameObject.transform.GetChild(childIndex);

                if (childTransform != null)
                {
                    GameObject foundGameObject =
                        FindGameObjectRecursive(childTransform.gameObject, objectNameToFind);

                    if (foundGameObject != null)
                        return foundGameObject;
                }
            }

            return null;
        }

        public static void SetMaterialFloatValueRecursive(GameObject gameObject, string shaderName, string parameterName, float value)
        {
            Renderer[] childRenderers= gameObject.GetComponentsInChildren<Renderer>();

            foreach (Renderer renderer in childRenderers)
            {
                foreach (Material material in renderer.materials)
                {
                    if (material.shader.name == shaderName)
                    {
                        material.SetFloat(parameterName, value);
                    }
                }                
            }
        }
        public static void SetMaterialColorAlphaValue(GameObject gameObject, string parameterName, float alpha)
        {
            Renderer childRenderer = gameObject.GetComponent<Renderer>();

            if (childRenderer != null)
            {
                Material material = childRenderer.material;
                Color color = material.GetColor(parameterName);
                color.a = alpha;
                material.SetColor(parameterName, color);
            }
        }

        public static void PrintObjectMaterials(GameObject gameObject)
        {
            Renderer[] childRenderers = gameObject.GetComponentsInChildren<Renderer>();
            foreach (Renderer renderer in childRenderers)
            {
                Plugin.Log?.Info(" GameObject: " + renderer.gameObject.name);

                foreach (Material material in renderer.materials)
                {
                    Plugin.Log?.Info(" Material: " + material.name);

                    Shader shader = material.shader;
                    Plugin.Log?.Info(" Shader: " + shader.name);
                    for (int propIndex = 0; propIndex < shader.GetPropertyCount(); ++propIndex)
                    {
                        string propertyName = shader.GetPropertyName(propIndex);
                        Plugin.Log?.Info("   Property: " + propertyName);
                        switch(shader.GetPropertyType(propIndex))
                        {
                            case UnityEngine.Rendering.ShaderPropertyType.Color:
                                {
                                    Color color = material.GetColor(propertyName);
                                    Plugin.Log?.Info("     color: " + color.ToString());
                                }
                                break;
                            case UnityEngine.Rendering.ShaderPropertyType.Float:
                                {
                                    float value = material.GetFloat(propertyName);
                                    Plugin.Log?.Info("     float: " + value);
                                }
                                break;
                            case UnityEngine.Rendering.ShaderPropertyType.Range:
                                {
                                    Plugin.Log?.Info("     Range");
                                }
                                break;
                            case UnityEngine.Rendering.ShaderPropertyType.Texture:
                                {
                                    Texture value = material.GetTexture(propertyName);
                                    if (value != null)
                                        Plugin.Log?.Info("     texture: " + value.name);
                                    else
                                        Plugin.Log?.Info("     texture: <null>");
                                }
                                break;
                            case UnityEngine.Rendering.ShaderPropertyType.Vector:
                                {
                                    Vector4 value = material.GetVector(propertyName);
                                    Plugin.Log?.Info("     vector: " + value.ToString());
                                }
                                break;
                        }
                    }
                }
            }
        }

        public static void PrintObjectTreeInScene(Scene loadedScene)
        {
            var rootGameObjects = loadedScene.GetRootGameObjects();

            foreach (var gameObject in rootGameObjects)
            {
                PrintObjectTree(gameObject, "  ");
            }
        }

        public static void PrintObjectTree(GameObject gameObject, string prefix)
        {
            if (gameObject == null)
                return;

            Plugin.Log?.Warn($"{prefix}{gameObject.name}");

            for (int childIndex = 0; childIndex < gameObject.transform.childCount; ++childIndex)
            {
                var childTransform = gameObject.transform.GetChild(childIndex);

                if (childTransform != null)
                {
                    PrintObjectTree(childTransform.gameObject, prefix + "  ");
                }
            }
        }

        public static void PrintComponents(GameObject gameObject)
        {
            if (gameObject == null)
                return;

            Plugin.Log?.Warn($"[{gameObject.name} Components]");

            UnityEngine.Component[] components = gameObject.GetComponents(typeof(MonoBehaviour));
            foreach (UnityEngine.Component component in components)
            {
                Plugin.Log?.Warn($"  {component.GetType().Name}");
            }
        }
    }
}
