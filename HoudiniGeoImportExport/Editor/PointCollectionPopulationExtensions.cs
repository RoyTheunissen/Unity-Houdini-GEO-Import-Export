/**
 * Houdini Geo File Importer for Unity
 *
 * Copyright 2015 by Waldo Bronchart <wbronchart@gmail.com>
 * Exporter added in 2021 by Roy Theunissen <roy.theunissen@live.nl>
 * Licensed under GNU General Public License 3.0 or later. 
 * Some rights reserved. See COPYING, AUTHORS.
 */

using System.Collections.Generic;
using System.IO;
using RoyTheunissen.UnityHoudiniGEOImportExport;
using UnityEditor;
using UnityEngine;

namespace Houdini.GeoImportExport
{
    /// <summary>
    /// Extensions for PointCollection specifically to populate it with prefabs.
    /// </summary>
    public static class PointCollectionPopulationExtensions
    {
        private const float InstanceReuseDistanceTolerance = 0.01f;
        
        private const string PrefabSuffix = ".prefab";

        private static readonly List<string> prefabsThatCouldntBeFound = new List<string>();

        private static readonly Dictionary<string, List<GameObject>> prefabPathToExistingInstances
            = new Dictionary<string, List<GameObject>>();

        private static readonly Dictionary<string, GameObject> prefabsByName = new Dictionary<string, GameObject>();

        /// <summary>
        /// Takes the point collection, and checks if it specifies a prefab in its name attribute. If so, we spawn
        /// an instance of that prefab at the specified point.
        /// </summary>
        public static void PopulateWithPrefabs<PointType>(
            this IList<PointType> points, Transform prefabInstanceContainer)
            where PointType : PointData, IPointDataPopulatable
        {
            // Keep track of what's already there so we can leave it if it already satisfies the conditions. This is not
            // just an optimization but it also allows users to override properties on these automatically placed
            // objects. That's useful so you can still go in and tweak some things.
            prefabPathToExistingInstances.Clear();
            for (int i = prefabInstanceContainer.childCount - 1; i >= 0; i--)
            {
                Transform child = prefabInstanceContainer.GetChild(i);
                string prefabPath = GetPrefabPathOfInstance(child.gameObject);
                bool existed = prefabPathToExistingInstances.TryGetValue(prefabPath, out List<GameObject> instances);
                
                if (!existed)
                {
                    instances = new List<GameObject>();
                    prefabPathToExistingInstances.Add(prefabPath, instances);
                }
                    
                instances.Add(child.gameObject);
            }

            // We only want to show a warning once per missing prefab type.
            prefabsThatCouldntBeFound.Clear();

            // Now populate the container with instances based on the specified prefabs.
            for (int i = 0; i < points.Count; i++)
            {
                PointType point = points[i];
                
                // Figure out what prefab this point wants.
                GameObject prefab = GetPrefabFromName(point.name);
                if (prefab == null)
                    continue;
                
                // Check if we already have an instance at this position.
                Vector3 worldPosition = prefabInstanceContainer.TransformPoint(point.P);
                
                GameObject existingPrefabInstanceThere = FindAndClaimExistingPrefabInstanceAt(prefab, worldPosition);
                
                // The requested prefab type was already there. Just leave it!
                if (existingPrefabInstanceThere != null)
                    continue;

                // The requested prefab type was not there. Let's place one there.
                PlacePrefab(point, prefab, prefabInstanceContainer);
            }
            
            // Whatever existing prefab instances were NOT claimed should now be destroyed.
            foreach (KeyValuePair<string,List<GameObject>> prefabExistingInstancesPair in prefabPathToExistingInstances)
            {
                for (int i = prefabExistingInstancesPair.Value.Count - 1; i >= 0; i--)
                {
                    Undo.DestroyObjectImmediate(prefabExistingInstancesPair.Value[i]);
                }
            }
            prefabPathToExistingInstances.Clear();
        }

        private static string GetPrefabPathOfInstance(GameObject instance)
        {
            if (instance == null || !PrefabUtility.IsPartOfPrefabThatCanBeAppliedTo(instance))
                return null;

            GameObject prefab = PrefabUtility.GetCorrespondingObjectFromSource(instance);
            return prefab == null ? null : AssetDatabase.GetAssetPath(prefab);
        }

        private static GameObject FindAndClaimExistingPrefabInstanceAt(GameObject prefab, Vector3 position)
        {
            // Check if we have instances of the specified prefab type.
            string prefabPath = AssetDatabase.GetAssetPath(prefab);
            bool hadInstances = prefabPathToExistingInstances.TryGetValue(prefabPath, out List<GameObject> instances);
            if (!hadInstances)
                return null;
            
            for (int i = 0; i < instances.Count; i++)
            {
                GameObject instance = instances[i];

                // If there's one roughly in the same position, use that instance.
                float distance = Vector3.Distance(instance.transform.position, position);
                if (distance < InstanceReuseDistanceTolerance)
                {
                    // Claim it so a further iteration cannot use the same instance.
                    instances.Remove(instance);
                    
                    return instance;
                }
            }

            return null;
        }

        private static void PlacePrefab(IPointDataPopulatable point, GameObject prefab, Transform container)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, container);
            instance.transform.localPosition = point.P;
            instance.transform.localRotation = point.orient;
            instance.transform.localScale = point.scale * point.pscale;
            Undo.RegisterCreatedObjectUndo(instance, "Placed Prefab From Point Collection");

            // If there are any components that would like to receive a callback when they are populated by metadata,
            // find those components and give them the callback now.
            IOnPopulatedByMetaData[] callbacks = instance.GetComponentsInChildren<IOnPopulatedByMetaData>();
            for (int i = 0; i < callbacks.Length; i++)
            {
                callbacks[i].OnPopulatedByMetaData(point);
            }
        }
        
        private static void FindPrefabCandidates(
            ref List<GameObject> candidates, string type, string name)
        {
            string nameDirectory = Path.GetDirectoryName(name);
            bool nameIncludesDirectories = !string.IsNullOrEmpty(nameDirectory);
            
            string[] guids = AssetDatabase.FindAssets($"t:{type} {name}");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid).ToUnityPath();
                string fileName = Path.GetFileNameWithoutExtension(path);

                // The file can't just contain the name on the point, it needs to match it exactly.
                if (fileName != name)
                    continue;

                // If the name includes directories, filter out any prefabs whose directory doesn't match.
                if (nameIncludesDirectories)
                {
                    string directory = Path.GetDirectoryName(path).ToUnityPath();

                    // Make sure that the path of this candidate ends with the specified directories.
                    if (!directory.EndsWith(nameDirectory))
                        continue;
                }

                GameObject candidatePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                candidates.Add(candidatePrefab);
            }
        }

        private static GameObject GetPrefabFromName(string name)
        {
            // Get rid of the prefab suffix and format it with forward slashes.
            name = name.RemoveSuffix(PrefabSuffix).ToUnityPath();

            if (string.IsNullOrEmpty(name))
                return null;

            string originalName = name;

            // First check if we found that prefab already.
            bool foundAlready = prefabsByName.TryGetValue(originalName, out GameObject prefab);
            if (foundAlready && prefab != null)
                return prefab;

            // Figure out if a directory is specified.
            string nameDirectory = Path.GetDirectoryName(name);
            bool nameIncludesDirectories = !string.IsNullOrEmpty(nameDirectory);
            if (nameIncludesDirectories)
            {
                name = Path.GetFileName(name);

                // Make sure the directory starts with a /. This ensures that if you specify that something
                // must be in a Water folder, it makes sure that Assets/Models/Water qualifies and something like
                // Assets/Models/UnderWater doesn't, despite ending with Water. Note that there is always a / at the
                // end even for assets at the root, because the path includs the Assets/ folder.
                if (!nameDirectory.StartsWith(Path.AltDirectorySeparatorChar))
                    nameDirectory = Path.AltDirectorySeparatorChar + nameDirectory;
            }

            // It's not known to us. Go find it.
            List<GameObject> candidates = new List<GameObject>();
            
            // We used to search for t:gameObject because that includes both model files and prefabs. How about we first
            // search for prefabs, give those priority, and if we don't find one THEN we look for models? This way if
            // there's a matching model file, but you've made a variant of that, it will use that one instead. That
            // seems like more desirable behaviour because if you made a variant you were probably meaning to override
            // some of the values from the model.
            FindPrefabCandidates(ref candidates, "prefab", name);
            
            if (candidates.Count == 0)
                FindPrefabCandidates(ref candidates, "model", name);

            if (candidates.Count > 0)
            {
                if (candidates.Count > 1)
                {
                    Debug.LogWarning(
                        $"Found several results for query <b>t:gameObject {originalName}</b>. Consider giving the asset " +
                        $"a unique name or specify some or all of the folders preceding it.");
                }

                prefab = candidates[0];
            }

            // If a valid prefab was found, store it in the dictionary for easy loading later.
            if (prefab != null)
            {
                prefabsByName.Add(originalName, prefab);
                return prefab;
            }

            // We only want an error once per prefab name.
            if (!prefabsThatCouldntBeFound.Contains(originalName))
            {
                Debug.LogWarning($"Couldn't find prefab by the name of '{originalName}'");
                prefabsThatCouldntBeFound.Add(originalName);
            }

            return null;
        }
    }
}
