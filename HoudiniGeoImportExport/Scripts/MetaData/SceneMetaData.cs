using System;
using Houdini.GeoImportExport.Settings;
using RoyTheunissen.Scaffolding.Utilities;
using UnityEngine;
using UnityEngine.Serialization;
using Object = UnityEngine.Object;

namespace Houdini.GeoImportExport.MetaData
{
    /// <summary>
    /// Helps manage the flow of a specific scene's metadata between Unity and Houdini.
    /// </summary>
    public sealed class SceneMetaData : MonoBehaviour, IMetaDataContainer
    {
        public const string ContainerName = "MetaData Import";
        
        [SerializeField, HideInInspector] private bool supportExporting = true;
        public bool SupportExporting => supportExporting;

        [SerializeField] private PathReference levelPath = new PathReference("World-", HoudiniSettings.HoudiniGeometryPath);

        [FormerlySerializedAs("metaDataPath")]
        [SerializeField] private PathReference metaDataExportPath = new PathReference("MetaData");
        public PathReference MetaDataExportPath => metaDataExportPath;
        
        [SerializeField, HideInInspector] private bool supportImporting = true;
        public bool SupportImporting => supportImporting;
        [SerializeField] private HoudiniGeo metaDataImport;
        public HoudiniGeo MetaDataImport => metaDataImport;
        public bool CanImport => SupportImporting && metaDataImport != null;

        public bool CanExport => SupportExporting;

        [NonSerialized] private Transform cachedContainer;
        [NonSerialized] private bool didCacheContainer;
        public Transform Container
        {
            get
            {
                if (!didCacheContainer)
                {
                    didCacheContainer = true;
                    cachedContainer = transform.Find(ContainerName);
                }
                return cachedContainer;
            }
        }

        GameObject IMetaDataContainer.GameObject => gameObject;

        public string Name => name;

        public delegate void MetaDataImportedHandler(SceneMetaData sceneMetaData);
        public static event MetaDataImportedHandler MetaDataImportedEvent;

        public SceneMetaData()
        {
            metaDataExportPath.RelativeTo = levelPath;
        }

        public PointCollection<PointType> GetPoints<PointType>(bool translateCoordinateSystems = true)
            where PointType : PointData
        {
            return metaDataImport.GetPoints<PointType>(translateCoordinateSystems);
        }

        public void DispatchMetaDataImportedEvent()
        {
            MetaDataImportedEvent?.Invoke(this);
        }
    }
}
