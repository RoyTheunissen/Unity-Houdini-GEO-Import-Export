using UnityEngine;

namespace Houdini.GeoImportExport.MetaData
{
    /// <summary>
    /// Useful interface that allows your game to also have custom metadata containing scripts that can be found
    /// together with general scene metadata.
    /// </summary>
    public interface IMetaDataContainer
    {
        bool CanImport { get; }
        bool CanExport { get; }
        string Name { get; }
        GameObject GameObject { get; }
    }
}
