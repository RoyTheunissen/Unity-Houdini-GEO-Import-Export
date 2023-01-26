using System.Collections.Generic;
using Houdini.GeoImportExport;
using UnityEditor;
using UnityEngine;

namespace Houdini.GeoImportExport.MetaData
{
    /// <summary>
    /// Draws buttons for importing scene meta data from Houdini or exporting scene meta data to Houdini.
    /// </summary>
    [CustomEditor(typeof(SceneMetaData))]
    public class SceneMetaDataEditor : Editor
    {
        private SceneMetaData sceneMetaData;
        
        private SerializedProperty supportExportingProperty;
        private SerializedProperty levelPathProperty;
        private SerializedProperty metaDataExportPathProperty;
        private SerializedProperty metaDataImportProperty;
        private SerializedProperty supportImportingProperty;

        private void OnEnable()
        {
            supportExportingProperty = serializedObject.FindProperty("supportExporting");
            levelPathProperty = serializedObject.FindProperty("levelPath");
            metaDataExportPathProperty = serializedObject.FindProperty("metaDataExportPath");
            
            supportImportingProperty = serializedObject.FindProperty("supportImporting");
            metaDataImportProperty = serializedObject.FindProperty("metaDataImport");
        }

        public override void OnInspectorGUI()
        {
            sceneMetaData = (SceneMetaData)target;
            
            serializedObject.Update();

            // Exporting metadata.
            supportExportingProperty.boolValue = EditorGUILayout.BeginToggleGroup(
                "Exporting", supportExportingProperty.boolValue);
            EditorGUILayout.EndToggleGroup();
            
            if (supportExportingProperty.boolValue)
            {
                EditorGUILayout.PropertyField(levelPathProperty);
                EditorGUILayout.Space();
                EditorGUILayout.PropertyField(metaDataExportPathProperty);
                
                bool shouldExport = GUILayout.Button("Export Meta Data");
                if (shouldExport)
                    Export(sceneMetaData);
            }

            // Importing metadata.
            EditorGUILayout.Space();
            supportImportingProperty.boolValue = EditorGUILayout.BeginToggleGroup(
                "Importing", supportImportingProperty.boolValue);
            EditorGUILayout.EndToggleGroup();

            if (supportImportingProperty.boolValue)
            {
                EditorGUILayout.PropertyField(metaDataImportProperty);

                GUI.enabled = sceneMetaData.CanImport;
                bool shouldImport = GUILayout.Button("Import Meta Data");
                if (shouldImport)
                    Import(sceneMetaData);
                GUI.enabled = true;
            }

            serializedObject.ApplyModifiedProperties();
        }

        public static void Export(SceneMetaData sceneMetaData)
        {
            if (!sceneMetaData.CanExport)
                return;
            
            string path = sceneMetaData.MetaDataExportPath.GetWithExtension(HoudiniGeo.EXTENSION);
            
            HoudiniGeo houdiniGeo = HoudiniGeo.Create();
            
            // Points can be added to the metadata by any children implementing IMetaDataPointProvider
            IMetaDataPointProvider[] pointProviders = sceneMetaData.GetComponentsInChildren<IMetaDataPointProvider>();
            if (pointProviders.Length > 0)
            {
                List<PointData> points = new List<PointData>();
                foreach (IMetaDataPointProvider pointProvider in pointProviders)
                {
                    // Ignore point providers that belong to a nested scene meta data. That one will handle them.
                    SceneMetaData parentMetaData = pointProvider.Transform.GetComponentInParent<SceneMetaData>();
                    if (parentMetaData != sceneMetaData)
                        continue;
                    
                    // Give the point provider an opportunity to add as many points as it wants.
                    pointProvider.GetPoints(sceneMetaData, ref points);
                }
                
                houdiniGeo.AddPoints(points);
            }
            
            // Added splines as actual spline geometry.
            IMetaDataSplineProvider[] splineProviders =
                sceneMetaData.GetComponentsInChildren<IMetaDataSplineProvider>();
            if (splineProviders.Length > 0)
            {
                List<SplineDataBase> splines = new List<SplineDataBase>();
                foreach (IMetaDataSplineProvider splineProvider in splineProviders)
                {
                    // Ignore spline providers that belong to a nested level meta data. That one will handle them.
                    SceneMetaData parentMetaData = splineProvider.Transform.GetComponentInParent<SceneMetaData>();
                    if (parentMetaData != sceneMetaData)
                        continue;
                    
                    // Give the spline provider an opportunity to add as many splines as it wants.
                    splineProvider.GetSplines(sceneMetaData, ref splines);
                }

                houdiniGeo.AddSplines(splines);
            }
            
            houdiniGeo.Export(path);
        }

        public static void Import(SceneMetaData sceneMetaData)
        {
            sceneMetaData.DispatchMetaDataImportedEvent();
            SceneMetaDataPopulator.PlacePrefabsFromMetaData(sceneMetaData);
        }
    }
}
