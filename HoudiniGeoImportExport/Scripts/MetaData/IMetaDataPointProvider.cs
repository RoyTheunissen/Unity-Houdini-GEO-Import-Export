using System.Collections.Generic;
using Houdini.GeoImportExport;
using UnityEngine;

namespace Houdini.GeoImportExport.MetaData
{
    /// <summary>
    /// Implement this if you want it to contribute point data to the scene metadata.
    /// </summary>
    public interface IMetaDataPointProvider
    {
        Transform Transform { get; }
        void GetPoints(SceneMetaData sceneMetaData, ref List<PointData> points);
    }
}
