/**
 * Houdini Geo File Importer for Unity
 *
 * Copyright 2015 by Waldo Bronchart <wbronchart@gmail.com>
 * Exporter added in 2021 by Roy Theunissen <roy.theunissen@live.nl>
 * Licensed under GNU General Public License 3.0 or later. 
 * Some rights reserved. See COPYING, AUTHORS.
 */

using System;
using System.Collections.Generic;

namespace Houdini.GeoImportExport
{
    /// <summary>
    /// Non-genericized base class for Spline Data.
    /// NOTE: Not a Spline Database but a Base for Spline Data. Sorry to disappoint.
    /// </summary>
    public abstract class SplineDataBase
    {
        public abstract IList<PointData> BasePoints { get; }
    }
    
    [Serializable]
    public class SplineData<PointType> : SplineDataBase
        where PointType : PointData
    {
        public PointCollection<PointType> points = new PointCollection<PointType>();

        public bool isClosed;

        public override IList<PointData> BasePoints => new List<PointData>(points);

        public SplineData()
        {
        }
        
        public SplineData(PointCollection<PointType> points, bool isClosed = false)
        {
            this.points.AddRange(points);
        }
    }
}
