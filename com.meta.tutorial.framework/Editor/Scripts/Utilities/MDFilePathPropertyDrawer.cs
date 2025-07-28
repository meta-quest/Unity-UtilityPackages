// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.IO;
using UnityEditor;
using UnityEngine;

namespace Meta.Tutorial.Framework.Hub.Utilities
{
    [CustomPropertyDrawer(typeof(MDFilePathAttribute))]
    public class MDFilePathPropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType == SerializedPropertyType.String)
            {
                var textPos = position;
                textPos.width -= 30;
                property.stringValue = EditorGUI.TextField(textPos, label.text, property.stringValue);
                var buttonPos = position;
                buttonPos.x += textPos.width + 5;
                buttonPos.width = 20;

                if (GUI.Button(buttonPos, "..."))
                {
                    var projectRoot = Directory.GetParent(Application.dataPath)?.ToString().Replace("\\", "/");
                    var dirName = projectRoot;
                    if (!string.IsNullOrEmpty(property.stringValue))
                    {
                        var fileInfo = new FileInfo(property.stringValue);
                        dirName = fileInfo.DirectoryName;
                    }

                    var path = EditorUtility.OpenFilePanel("Select .md file", dirName, "md");
                    if (!string.IsNullOrEmpty(path))
                    {
                        if (path.StartsWith(projectRoot))
                        {
                            path = path.Replace(projectRoot, ".");
                            property.stringValue = path;
                            _ = property.serializedObject.ApplyModifiedProperties();
                        }
                        else
                        {
                            Debug.LogError($"The path {path} is not part of the project root {projectRoot}");
                        }
                    }
                    GUIUtility.ExitGUI();
                }
            }
            else
            {
                EditorGUI.LabelField(position, label.text, "Use MDFilePath with string.");
            }
        }
    }
}