using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Houdini.GeoImportExport.Settings
{
    /// <summary>
    /// Responsible for the area in the Project Settings window where you can configure the Houdini settings. 
    /// </summary>
    public static class HoudiniSettingsWindow
    {
        private static readonly HashSet<string> keywords = new HashSet<string>(new[] { "Houdini" });
        
        [NonSerialized] private static List<FieldInfo> cachedSettings;
        private static List<FieldInfo> Settings
        {
            get
            {
                return cachedSettings ??
                       (cachedSettings =
                           RoyTheunissen.DevelopmentSettings.Settings.GetSettings(typeof(HoudiniSettings)));
            }
        }

        [SettingsProvider]
        public static SettingsProvider CreateHoudiniSettingsProvider()
        {
            // First parameter is the path in the Settings window.
            // Second parameter is the scope of this setting: it only appears in the Project Settings window.
            SettingsProvider provider = new SettingsProvider("Project/Houdini", SettingsScope.Project)
            {
                guiHandler = (string searchContext) =>
                {
                    GUILayout.Space(10);
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(8);
                    EditorGUILayout.BeginVertical();
                    
                    EditorGUILayout.LabelField("Settings for the procedural Houdini workflow.");
                    
                    EditorGUILayout.Space();

                    // Draw the editor preferences themselves.
                    RoyTheunissen.DevelopmentSettings.Settings.DrawSettings(Settings);
                    
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.EndHorizontal();
                },

                // Populate the search keywords to enable smart search filtering and label highlighting:
                keywords = keywords,
            };

            return provider;
        }
    }
    
    
}
