using UnityEngine;

namespace Houdini.GeoImportExport
{
    /// <summary>
    /// Point Data which has all the fields required to place a specified prefab on it. If you can't or don't want to
    /// inherit from PointDataPopulatable you can also implement IPointDataPopulatable instead.
    /// </summary>
    public class PointDataPopulatable : PointData, IPointDataPopulatable
    {
        public string name;
        public float pscale = 1.0f;
        public Vector3 scale = Vector3.one;
        public Quaternion orient = Quaternion.identity;
        
        public PointDataPopulatable()
        {
        }

        public PointDataPopulatable(Vector3 p, string name, float pscale, Vector3 scale, Quaternion orient) : base(p)
        {
            this.pscale = pscale;
            this.scale = scale;
            this.orient = orient;
            this.name = name;
        }
        
        public override string ToString()
        {
            return $"{nameof(P)}: {P}, {nameof(name)}: '{name}', {nameof(pscale)}: {pscale}, {nameof(scale)}: {scale}, {nameof(orient)}: {orient}";
        }
        
        Vector3 IPointDataPopulatable.P => P;

        float IPointDataPopulatable.pscale => pscale;

        Vector3 IPointDataPopulatable.scale => scale;

        Quaternion IPointDataPopulatable.orient => orient;

        string IPointDataPopulatable.name => name;
    }
}
