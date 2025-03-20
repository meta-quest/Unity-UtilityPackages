// Copyright (c) Meta Platforms, Inc. and affiliates.
using UnityEditor;
using UnityEngine;

namespace Meta.Utilities.Environment
{
    /// <summary>
    /// Custom editor for WindController, displays a handle in the scene to help with visualisation
    /// </summary>
    [CustomEditor(typeof(WindController))]
    public class WindControllerEditor : UnityEditor.Editor
    {
        private WindController m_windController;
        private WindData m_windData;
        private SerializedObject m_serializedWindData;
        private bool m_showGeneral = true;
        private bool m_showPrimary = true;
        private bool m_showSecondary = true;
        private bool m_showVerticleLeaf = true;
        private bool m_showTrunk = true;

        private GUIStyle m_headerStyle;
        private readonly Color m_activeHandleColor = new(0.2f, 0.6f, 1f, 1f);

        private void OnEnable()
        {
            m_windController = (WindController)target;
            UpdateWindDataReference();

            // Initialize header style
            m_headerStyle = new GUIStyle
            {
                fontStyle = FontStyle.Bold,
                fontSize = 12
            };
            m_headerStyle.normal.textColor = EditorGUIUtility.isProSkin ? Color.white : Color.black;
        }

        private void UpdateWindDataReference()
        {
            var windDataProp = serializedObject.FindProperty("windData");
            if (windDataProp != null)
            {
                m_windData = windDataProp.objectReferenceValue as WindData;
                if (m_windData != null)
                {
                    m_serializedWindData = new SerializedObject(m_windData);
                }
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUI.BeginChangeCheck();

            // Draw WindData field with object picker
            EditorGUI.BeginChangeCheck();
            _ = EditorGUILayout.PropertyField(serializedObject.FindProperty("windData"));
            if (EditorGUI.EndChangeCheck())
            {
                _ = serializedObject.ApplyModifiedProperties();
                UpdateWindDataReference();
            }

            if (m_windData == null)
            {
                EditorGUILayout.Space(10);
                EditorGUILayout.HelpBox("Please assign a Wind Data asset.", MessageType.Warning);
                return;
            }

            m_serializedWindData.Update();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Wind Parameters", m_headerStyle);
            EditorGUILayout.Space(5);

            // Draw the foldout sections
            DrawSection("General Settings", ref m_showGeneral, () =>
            {
                DrawProperty("_Random_Offset");
                DrawProperty("_Wind_Direction");
                DrawProperty("_Wind_Intensity");
                DrawProperty("_Lead_Amount");
            });

            DrawSection("Primary Wind", ref m_showPrimary, () =>
            {
                DrawProperty("_Primary_Wind_Speed");
                DrawProperty("_Primary_Frequency");
                DrawProperty("_Primary_Amplitude");
            });

            DrawSection("Secondary Wind", ref m_showSecondary, () =>
            {
                DrawProperty("_Secondary_Wind_Speed");
                DrawProperty("_Secondary_Frequency");
                DrawProperty("_Secondary_Amplitude");
            });

            DrawSection("Verticle Leaf", ref m_showVerticleLeaf, () =>
            {
                DrawProperty("_Verticle_Leaf_Speed");
                DrawProperty("_Verticle_Leaf_Frequency");
                DrawProperty("_Verticle_Leaf_Amplitude");
            });

            DrawSection("Trunk", ref m_showTrunk, () =>
            {
                DrawProperty("_Trunk_Wind_Speed");
                DrawProperty("_Trunk_Frequency");
                DrawProperty("_Trunk_Amplitude");
                DrawProperty("_Trunk_Wind_Intensity");
            });

            if (EditorGUI.EndChangeCheck())
            {
                _ = m_serializedWindData.ApplyModifiedProperties();
                EditorUtility.SetDirty(m_windData);
            }

            _ = serializedObject.ApplyModifiedProperties();
        }

        private void DrawSection(string title, ref bool show, System.Action drawContent)
        {
            EditorGUILayout.Space(5);
            show = EditorGUILayout.BeginFoldoutHeaderGroup(show, title);
            if (show)
            {
                EditorGUI.indentLevel++;
                drawContent?.Invoke();
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawProperty(string propertyName)
        {
            var property = m_serializedWindData.FindProperty(propertyName);
            if (property != null)
            {
                _ = EditorGUILayout.PropertyField(property);
            }
        }

        private void OnSceneGUI()
        {
            if (m_windData == null) return;

            var windDirProp = m_serializedWindData.FindProperty("_Wind_Direction");
            var position = m_windController.transform.position;
            var windDirection = windDirProp.vector3Value;
            var handleSize = HandleUtility.GetHandleSize(position) * 2;

            // Draw direction arrow
            Handles.color = m_activeHandleColor;
            Handles.ArrowHandleCap(
                0,
                position,
                Quaternion.LookRotation(windDirection),
                handleSize,
                EventType.Repaint
            );

            // Position handle for wind direction
            EditorGUI.BeginChangeCheck();
            var newPos = Handles.PositionHandle(position + windDirection * handleSize, Quaternion.identity);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(m_windData, "Change Wind Direction");
                windDirProp.vector3Value = (newPos - position).normalized;
                _ = m_serializedWindData.ApplyModifiedProperties();
                EditorUtility.SetDirty(m_windData);
            }

            // Draw info labels
            Handles.BeginGUI();
            var screenPosition = HandleUtility.WorldToGUIPoint(position + Vector3.up * handleSize);

            GUILayout.BeginArea(new Rect(screenPosition.x + 10, screenPosition.y - 40, 200, 100));
            EditorGUILayout.LabelField("Wind Parameters:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Direction: {windDirection:F2}");
            EditorGUILayout.LabelField($"Intensity: {m_windData.WindIntensity:F2}");
            GUILayout.EndArea();

            Handles.EndGUI();
        }
    }
}