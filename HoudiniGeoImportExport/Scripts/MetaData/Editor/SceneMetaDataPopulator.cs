using Houdini.GeoImportExport;
using UnityEditor;
using UnityEngine;

namespace Houdini.GeoImportExport.MetaData
{
    [InitializeOnLoad]
    public static class SceneMetaDataPopulator
    {
        static SceneMetaDataPopulator()
        {
            HoudiniGeo.GeoFileImportedEvent -= HandleGeoFileImportedEvent;
            HoudiniGeo.GeoFileImportedEvent += HandleGeoFileImportedEvent;
        }

        private static void HandleGeoFileImportedEvent(HoudiniGeo houdiniGeo)
        {
            SceneMetaData[] sceneMetaDatas = Object.FindObjectsOfType<SceneMetaData>();
            foreach (SceneMetaData sceneMetaData in sceneMetaDatas)
            {
                if (!sceneMetaData.CanImport || sceneMetaData.MetaDataImport != houdiniGeo)
                    continue;
                
                SceneMetaDataEditor.Import(sceneMetaData);
            }
        }

        public static void PlacePrefabsFromMetaData(SceneMetaData sceneMetaData)
        {
            if (sceneMetaData.MetaDataImport == null)
                return;
            
            Debug.Log($"Importing scene meta data '{sceneMetaData.MetaDataImport.name}'...");
            PointCollection<PointDataPopulatable> points =
                sceneMetaData.MetaDataImport.GetPoints<PointDataPopulatable>();

            Transform container = sceneMetaData.transform.FindOrCreateChild(SceneMetaData.ContainerName);
            
            points.PopulateWithPrefabs(container);
        }
    }
}
