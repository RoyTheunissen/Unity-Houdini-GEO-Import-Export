using System.Collections.Generic;
using Houdini.GeoImportExport;
using UnityEngine;

namespace Houdini.GeoImportExport.MetaData
{
    /// <summary>
    /// Implement this if you want it to contribute spline data to the scene metadata.
    /// </summary>
    public interface IMetaDataSplineProvider
    {
        Transform Transform { get; }
        void GetSplines(SceneMetaData sceneMetaData, ref List<SplineDataBase> splines);
    }
}
