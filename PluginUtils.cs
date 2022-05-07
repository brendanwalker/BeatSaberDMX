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
