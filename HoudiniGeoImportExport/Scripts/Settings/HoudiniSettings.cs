using RoyTheunissen.DevelopmentSettings;

namespace Houdini.GeoImportExport.Settings
{
    /// <summary>
    /// General Houdini settings like file paths.
    /// </summary>
    public static class HoudiniSettings
    {
        private const string PathPrefix = "Houdini/";

        public static readonly EditorPreferencePath HoudiniRootPath = new EditorPreferencePath(PathPrefix + "Root Path");
        public static readonly EditorPreferencePath HoudiniGeometryPath = new EditorPreferencePath(PathPrefix + "Geometry Path", "geo/", HoudiniRootPath);
        public static readonly EditorPreferencePath HoudiniSplinesGeometryPath = new EditorPreferencePath(PathPrefix + "Geometry Splines Path", "Splines/", HoudiniGeometryPath);
    }
}
