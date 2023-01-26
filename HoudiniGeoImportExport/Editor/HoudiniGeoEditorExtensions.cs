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
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Houdini.GeoImportExport
{
    public static class HoudiniGeoEditorExtensions
    {
        public static readonly string[] GroupFieldNames = { "groups", "grouping" };
        
        internal static void ImportAllMeshes(this HoudiniGeo geo)
        {
            string geoAssetPath = AssetDatabase.GetAssetPath(geo);
            if (!File.Exists(geoAssetPath))
            {
                return;
            }

            // Convert to unity mesh and store mesh as sub asset
            if (geo.polyPrimitives.Count > 0)
            {
                var mesh = AssetDatabase.LoadAllAssetsAtPath(geoAssetPath).Where(a => a is Mesh).FirstOrDefault() as Mesh;
                if (mesh == null)
                {
                    mesh = new Mesh();
                    AssetDatabase.AddObjectToAsset(mesh, geoAssetPath);
                }
                
                geo.ToUnityMesh(mesh);
                EditorUtility.SetDirty(mesh);
            }
        }
        
        public static void Export(this HoudiniGeo houdiniGeo, string path = null)
        {
            HoudiniGeoFileExporter.Export(houdiniGeo, path);
        }
        
        public static void AddPoints<PointType>(
            this HoudiniGeo houdiniGeo, IList<PointType> pointCollection,
            bool translateCoordinateSystems = true)
            where PointType : PointData
        {
            // First determine the point type. You'd think we could just use PointType, but we actually want to be able
            // to use this in a generic way and specify PointData (the base class) as the type.
            Type pointType = pointCollection[0].GetType();

            for (int i = 1; i < pointCollection.Count; i++)
            {
                if (pointCollection[i].GetType() != pointType)
                {
                    Debug.LogError($"Adding Points to Houdini GEO file but found point of type " +
                                   $"'{pointCollection[i].GetType().Name}' while it was expecting all of the points " +
                                   $"to be of type '{pointType.Name}'. We don't currently support exporting points " +
                                   $"of mixed types.");
                    return;
                }
            }

            // First create the attributes.
            Dictionary<FieldInfo, HoudiniGeoAttribute> fieldToPointAttribute =
                new Dictionary<FieldInfo, HoudiniGeoAttribute>();
            Dictionary<FieldInfo, HoudiniGeoAttribute> fieldToDetailAttribute = new Dictionary<FieldInfo, HoudiniGeoAttribute>();
            FieldInfo[] fieldCandidates =
                pointType.GetFields(
                    BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            FieldInfo groupsField = null;
            foreach (FieldInfo field in fieldCandidates)
            {
                // Ignore private fields that aren't tagged with SerializeField. 
                if (field.IsPrivate && field.GetCustomAttribute<SerializeField>() == null)
                    continue;

                if (GroupFieldNames.Contains(field.Name))
                {
                    groupsField = field;
                    continue;
                }

                HoudiniGeoAttributeOwner owner =
                    field.IsStatic ? HoudiniGeoAttributeOwner.Detail : HoudiniGeoAttributeOwner.Point;

                bool hasValidAttribute = TryGetOrCreateAttribute(
                    houdiniGeo, field, owner, out HoudiniGeoAttribute attribute);
                if (!hasValidAttribute)
                    continue;

                if (field.IsStatic)
                    fieldToDetailAttribute.Add(field, attribute);
                else
                    fieldToPointAttribute.Add(field, attribute);
            }
            
            // Now increment the point count. We must do this AFTER the attributes are created because if there are
            // already points and we create a new attribute, it will neatly populate the value collections with default
            // values for those pre-existing points. These new points will receive such attribute values down below.
            houdiniGeo.pointCount += pointCollection.Count;

            // Then populate the point attributes with values.
            foreach (KeyValuePair<FieldInfo, HoudiniGeoAttribute> kvp in fieldToPointAttribute)
            {
                foreach (PointType point in pointCollection)
                {
                    object value = kvp.Key.GetValue(point);
                    
                    HoudiniGeoAttribute attribute = kvp.Value;

                    attribute.AddValueAsTuples(value, translateCoordinateSystems);
                }
            }
            
            // Now populate the detail attributes with values.
            foreach (KeyValuePair<FieldInfo, HoudiniGeoAttribute> kvp in fieldToDetailAttribute)
            {
                object value = kvp.Key.GetValue(null);

                HoudiniGeoAttribute attribute = kvp.Value;

                attribute.AddValueAsTuples(value, translateCoordinateSystems);
            }

            // Figure out which groups this point has based on the enum type.
            if (groupsField != null)
            {
                Type groupsEnumType = groupsField.FieldType;
                if (!typeof(Enum).IsAssignableFrom(groupsEnumType))
                {
                    Debug.LogError($"Fields named 'groups' are special and are used to set groups in the .GEO file. " +
                                   $"It must be of an enum type with each flag representing its group participation.");
                    return;
                }

                // Now create a group for every flag in the enum.
                string[] enumNames = Enum.GetNames(groupsEnumType);
                Array enumValues = Enum.GetValues(groupsEnumType);
                for (int i = 0; i < enumNames.Length; i++)
                {
                    string groupName = enumNames[i];
                    int groupValue = (int)enumValues.GetValue(i);
                    
                    if (groupValue <= 0)
                        continue;

                    // Get or create the point group.
                    houdiniGeo.TryGetOrCreateGroup(groupName, HoudiniGeoGroupType.Points, out PointGroup group);
                    
                    // Populate the group with points.
                    for (int j = 0; j < pointCollection.Count; j++)
                    {
                        PointType point = pointCollection[j];
                        int pointGroupFlags = (int)groupsField.GetValue(point);

                        int pointId = houdiniGeo.pointCount - pointCollection.Count + j;
                        
                        // Check that the point has the flag for this group.
                        if ((pointGroupFlags & groupValue) == groupValue)
                            group.ids.Add(pointId);
                    }

                    // Now add it to the geometry.
                    houdiniGeo.pointGroups.Add(group);
                }
            }
        }

        public static void AddSplines<SplineType>(
            this HoudiniGeo houdiniGeo, IList<SplineType> splines,
            bool translateCoordinateSystems = true)
            where SplineType : SplineDataBase
        {
            foreach (SplineType spline in splines)
            {
                IList<PointData> points = spline.BasePoints;
                
                // Firstly we can just add all of the points. There is nothing special about these points, it's like
                // any ordinary point collection.
                houdiniGeo.AddPoints(points, translateCoordinateSystems);

                // Primitives are comprised of vertices, not points. So we need to add a vertex for every point.
                // Why do we do this in reverse? The splines I exported from Maya do it in reverse, so I'm doing it too
                // for consistency. You'll get the spline served to you the way it would be if it came from Maya instead.
                for (int i = points.Count - 1; i >= 0; i--)
                {
                    // Knowing that we just added points, we can grab them from the end of the list.
                    int pointIndex = houdiniGeo.pointCount - points.Count + i;
                    houdiniGeo.pointRefs.Add(pointIndex);
                    houdiniGeo.vertexCount++;
                }
                
                NURBCurvePrimitive nurbCurvePrimitive = new NURBCurvePrimitive();
                for (int i = 0; i < points.Count; i++)
                {
                    // Knowing that we just added a certain number of vertices, we can calculate which ones they were.
                    int vertexIndex = houdiniGeo.vertexCount - points.Count + i;
                    nurbCurvePrimitive.indices.Add(vertexIndex);
                }

                // NOTE: This does not support EVERY kind of spline that GEO files can handle, but it supports
                // bezier curves which is the kind that's most useful to export from Unity.
                nurbCurvePrimitive.order = 4;
                nurbCurvePrimitive.endInterpolation = true;
                
                // I'm not so well-versed in NURBS so I'm winging it a little bit here based on a wikipedia article
                // and some reference splines that I cooked up in Maya. If I made a mistake feel free to fix it.
                // Here's my sources:
                // https://en.wikipedia.org/wiki/Non-uniform_rational_B-spline#:~:text=The%20knot%20vector%20is%20a,control%20points%20plus%20curve%20order).
                // Also go watch this, it's really good: https://www.youtube.com/watch?v=jvPPXbo87ds
                int vertexCount = points.Count;
                int knotCount = 2 + (vertexCount - nurbCurvePrimitive.order) / (nurbCurvePrimitive.order - 1);
                for (int i = 0; i < knotCount; i++)
                {
                    int multiplicity;
                    if (i == 0 || i == knotCount - 1)
                        multiplicity = nurbCurvePrimitive.order;
                    else
                        multiplicity = nurbCurvePrimitive.order - 1;
                    
                    for (int j = 0; j < multiplicity; j++)
                    {
                        nurbCurvePrimitive.knots.Add(i);
                    }
                }
                
                houdiniGeo.nurbCurvePrimitives.Add(nurbCurvePrimitive);
                houdiniGeo.primCount++;
            }
        }
        
        private static bool GetAttributeTypeAndSize(Type valueType, out HoudiniGeoAttributeType type, out int tupleSize)
        {
            type = HoudiniGeoAttributeType.Invalid;
            tupleSize = 0;
            
            if (valueType == typeof(bool))
            {
                type = HoudiniGeoAttributeType.Integer;
                tupleSize = 1;
            }
            else if (valueType == typeof(float))
            {
                type = HoudiniGeoAttributeType.Float;
                tupleSize = 1;
            }
            else if (valueType == typeof(int))
            {
                type = HoudiniGeoAttributeType.Integer;
                tupleSize = 1;
            }
            else if (valueType == typeof(string))
            {
                type = HoudiniGeoAttributeType.String;
                tupleSize = 1;
            }
            if (valueType == typeof(Vector2))
            {
                type = HoudiniGeoAttributeType.Float;
                tupleSize = 2;
            }
            else if (valueType == typeof(Vector3))
            {
                type = HoudiniGeoAttributeType.Float;
                tupleSize = 3;
            }
            else if (valueType == typeof(Vector4))
            {
                type = HoudiniGeoAttributeType.Float;
                tupleSize = 4;
            }
            else if (valueType == typeof(Vector2Int))
            {
                type = HoudiniGeoAttributeType.Integer;
                tupleSize = 2;
            }
            else if (valueType == typeof(Vector3Int))
            {
                type = HoudiniGeoAttributeType.Integer;
                tupleSize = 3;
            }
            else if (valueType == typeof(Quaternion))
            {
                type = HoudiniGeoAttributeType.Float;
                tupleSize = 4;
            }
            else if (valueType == typeof(Color))
            {
                type = HoudiniGeoAttributeType.Float;
                tupleSize = 3;
            }

            return type != HoudiniGeoAttributeType.Invalid;
        }
        
        private static bool TryCreateAttribute(
            this HoudiniGeo houdiniGeo, string name, HoudiniGeoAttributeType type, int tupleSize,
            HoudiniGeoAttributeOwner owner, out HoudiniGeoAttribute attribute)
        {
            attribute = new HoudiniGeoAttribute {type = type, tupleSize = tupleSize};

            if (attribute == null)
                return false;

            attribute.name = name;
            attribute.owner = owner;
            
            houdiniGeo.attributes.Add(attribute);
            
            // If we are adding an attribute of an element type that already has elements present, we need to make sure 
            // that they have default values.
            if (owner == HoudiniGeoAttributeOwner.Vertex)
                attribute.AddDefaultValues(houdiniGeo.vertexCount, type, tupleSize);
            else if (owner == HoudiniGeoAttributeOwner.Point)
                attribute.AddDefaultValues(houdiniGeo.pointCount, type, tupleSize);
            else if (owner == HoudiniGeoAttributeOwner.Primitive)
                attribute.AddDefaultValues(houdiniGeo.primCount, type, tupleSize);

            return true;
        }

        private static bool TryCreateAttribute(
            this HoudiniGeo houdiniGeo, FieldInfo fieldInfo, HoudiniGeoAttributeOwner owner,
            out HoudiniGeoAttribute attribute)
        {
            attribute = null;

            Type valueType = fieldInfo.FieldType;

            bool isValid = GetAttributeTypeAndSize(valueType, out HoudiniGeoAttributeType type, out int tupleSize);
            if (!isValid)
                return false;

            return TryCreateAttribute(houdiniGeo, fieldInfo.Name, type, tupleSize, owner, out attribute);
        }

        private static bool TryGetOrCreateAttribute(this HoudiniGeo houdiniGeo,
            FieldInfo fieldInfo, HoudiniGeoAttributeOwner owner, out HoudiniGeoAttribute attribute)
        {
            bool isValid = GetAttributeTypeAndSize(fieldInfo.FieldType, out HoudiniGeoAttributeType type, out int _);
            if (!isValid)
            {
                attribute = null;
                return false;
            }

            bool existedAlready = houdiniGeo.TryGetAttribute(fieldInfo.Name, type, owner, out attribute);
            if (existedAlready)
                return true;

            return TryCreateAttribute(houdiniGeo, fieldInfo, owner, out attribute);
        }
    }
}
