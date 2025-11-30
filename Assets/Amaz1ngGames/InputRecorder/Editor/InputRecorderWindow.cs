// File: Assets/Editor/InputRecorderWindow.cs
// Description: EditorWindow to control InputRecorder in play mode, show live stats, heatmaps, and export buttons.
// - Controls: choose backend (Old / New), optionally assign InputActionAsset (new system), Start/End, Export CSV, Export Heatmaps
// - Shows scrollable counters and a simple heatmap preview for mouse clicks (old) or Vector2 actions (new).
// Note: This file uses conditional compilation for the new Input System. If you use the package, enable ENABLE_INPUT_SYSTEM.

using System.IO;
using UnityEditor;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Amaz1ngGames.InputRecorder
{
    public class InputRecorderWindow : EditorWindow
    {
        private InputRecorder recorderInstance = null;
        private Vector2 scroll;
        private bool showHeatmap = true;
        private Vector2 heatmapScroll;
        private int heatmapResolution = 17;
        private int heatmapResolutionLevel = 4;
        private string csvExportPath = "";
        private string heatmapExportFolder = "";

        [MenuItem("Window/Amaz1ng Games/Input Recorder")]
        public static void ShowWindow()
        {
            var w = GetWindow<InputRecorderWindow>("Input Recorder");
            w.minSize = new Vector2(540, 640);
        }

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        }

        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            // clear reference when exiting play mode
            if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode)
            {
                recorderInstance = null;
                Repaint();
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.Space();

            if (GUILayout.Button("Find or Create InputRecorder in Scene"))
            {
                FindOrCreateRecorderInScene();
            }

            EditorGUILayout.Space();

            if (recorderInstance == null)
            {
                EditorGUILayout.HelpBox("No InputRecorder instance found in the scene. Click 'Find or Create' to add one.", MessageType.Info);
                return;
            }

            // Show backend and asset (if new)
            EditorGUI.BeginChangeCheck();
            var backend = recorderInstance.backend;
            backend = (InputRecorder.InputBackend)EditorGUILayout.EnumPopup("Backend", backend);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(recorderInstance, "Change backend");
                recorderInstance.backend = backend;
                EditorUtility.SetDirty(recorderInstance);
            }

            #if ENABLE_INPUT_SYSTEM
            if (recorderInstance.backend == InputRecorder.InputBackend.NewInputSystem)
            {
                var asset = recorderInstance.InputActionAsset;
                asset = (InputActionAsset)EditorGUILayout.ObjectField("Input Action Asset", asset, typeof(InputActionAsset), false);
                if (asset != recorderInstance.InputActionAsset)
                {
                    Undo.RecordObject(recorderInstance, "Assign action asset");
                    recorderInstance.InputActionAsset = asset;
                    EditorUtility.SetDirty(recorderInstance);
                }
            }
            else
            {
                EditorGUILayout.LabelField("Old Input System: will record KeyCode and mouse clicks.");
            }
            #else
            EditorGUILayout.HelpBox("New Input System is not available in this project (ENABLE_INPUT_SYSTEM undefined). Using Old Input System mode only.", MessageType.Info);
            #endif

            EditorGUILayout.Space();

            // Start/End Buttons
            EditorGUILayout.BeginHorizontal();
            GUI.enabled = Application.isPlaying && !recorderInstance.IsRecording;
            #if !ENABLE_INPUT_SYSTEM
            GUI.enabled = false; // disable if new input system is not available
            #endif
            if (GUILayout.Button("Start"))
                recorderInstance.StartRecording();

            GUI.enabled = Application.isPlaying && recorderInstance.IsRecording;
            #if !ENABLE_INPUT_SYSTEM
            GUI.enabled = false; // disable if new input system is not available
            #endif
            if (GUILayout.Button("End"))
                recorderInstance.EndRecording();
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();

            // Show status
            EditorGUILayout.LabelField("Status:");
            EditorGUILayout.LabelField($"Recording: {recorderInstance.IsRecording}");
            EditorGUILayout.LabelField($"Backend: {recorderInstance.backend}");
            if (recorderInstance.EndTime > 0)
                EditorGUILayout.LabelField($"Recorded duration: {recorderInstance.EndTime - recorderInstance.StartTime:F2} s");

            EditorGUILayout.Space(16);

            EditorGUILayout.BeginHorizontal();
            // Live stats panel
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField("Live Stats", EditorStyles.boldLabel);
            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(360 + EditorGUIUtility.singleLineHeight));
            DrawStatsPanel();
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            // Heatmap preview and export
            EditorGUILayout.BeginVertical();
            // EditorGUILayout.LabelField("Heatmaps", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            showHeatmap = EditorGUILayout.Toggle("Show Heatmaps", showHeatmap);
            if (GUILayout.Button("Refresh Preview", GUILayout.Width(120)))
            {
                // just repaint
                Repaint();
            }
            EditorGUILayout.EndHorizontal();
            heatmapResolutionLevel = EditorGUILayout.IntSlider("Resolution Level", heatmapResolutionLevel, 2, 8);
            heatmapResolution = 1 << heatmapResolutionLevel + 1; // 2^level+1
            
            heatmapScroll = EditorGUILayout.BeginScrollView(heatmapScroll, GUILayout.Height(360), GUILayout.Width(position.width - 288));
            if (showHeatmap)
                DrawHeatmapPreview();
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

            // Export buttons
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Export CSV"))
            {
                ExportCsvFromRecorder();
            }
            if (GUILayout.Button("Export Heatmaps"))
            {
                ExportHeatmapsFromRecorder();
            }
            EditorGUILayout.EndHorizontal();

        }

        private void DrawStatsPanel()
        {
            if (recorderInstance == null) return;

            var stats = recorderInstance.GetStatsSnapshot();

            if (stats.backend == InputRecorder.InputBackend.OldInput)
            {
                EditorGUILayout.LabelField("Keyboard counts:");
                foreach (var kv in stats.oldKeyCounts)
                {
                    EditorGUILayout.LabelField($"  {kv.Key}: {kv.Value}");
                }

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Mouse buttons:");
                foreach (var kv in stats.oldMouseButtonCounts)
                {
                    EditorGUILayout.LabelField($"  Button {kv.Key}: {kv.Value}");
                }

                EditorGUILayout.Space();
                EditorGUILayout.LabelField($"Mouse click samples: {stats.oldMousePositions.Count}");
            }
            else
            {
                EditorGUILayout.LabelField("Action counts (New Input System):");
                foreach (var kv in stats.newActionCounts)
                {
                    EditorGUILayout.LabelField($"  {kv.Key}: {kv.Value}");
                }

                EditorGUILayout.Space();
                foreach (var kv in stats.newVector2Positions)
                {
                    EditorGUILayout.LabelField($"  Vector2 samples for {kv.Key}: {kv.Value.Count}");
                }
            }
        }

        private void DrawHeatmapPreview()
        {
            if (recorderInstance == null) return;

            var stats = recorderInstance.GetStatsSnapshot();
            if (stats.backend == InputRecorder.InputBackend.OldInput)
            {
                if (stats.oldMousePositions.Count == 0)
                {
                    EditorGUILayout.HelpBox("No mouse click samples recorded yet.", MessageType.Info);
                    return;
                }
            }
            else
            {
                // For each vector2 action, show a small preview
                if (stats.newVector2Positions == null || stats.newVector2Positions.Count == 0)
                {
                    EditorGUILayout.HelpBox("No Vector2 action samples recorded yet.", MessageType.Info);
                    return;
                }
            }
            
            foreach (var kv in recorderInstance.GenerateHeatmaps(heatmapResolution))
            {
                var tex = kv.Value;
                if (tex == null)
                    continue;
                
                Rect rect = GUILayoutUtility.GetAspectRect(1.0f);
                GUI.DrawTexture(rect, tex, ScaleMode.ScaleToFit);
                // Provide button to save preview
                if (GUILayout.Button($"Save {kv.Key} Heatmap PNG"))
                {
                    var folder = EditorUtility.SaveFolderPanel("Choose folder to save heatmap", "", "");
                    if (!string.IsNullOrEmpty(folder))
                    {
                        string fileName = $"{SanitizeFileName(kv.Key)}.png";
                        string path = Path.Combine(folder, fileName);
                        File.WriteAllBytes(path, tex.EncodeToPNG());
                    }
                }
                DestroyImmediate(tex);
            }
        }

        private void ExportCsvFromRecorder()
        {
            if (recorderInstance == null) return;
            csvExportPath = EditorUtility.SaveFilePanel("Export Recorder CSV", "", "input_record.csv", "csv");
            if (string.IsNullOrEmpty(csvExportPath)) return;
            recorderInstance.ExportCsv(csvExportPath);
            EditorUtility.RevealInFinder(csvExportPath);
            Debug.Log($"Exported CSV to {csvExportPath}");
        }

        private void ExportHeatmapsFromRecorder()
        {
            if (recorderInstance == null) return;
            heatmapExportFolder = EditorUtility.SaveFolderPanel("Export heatmaps to folder", "", "");
            if (string.IsNullOrEmpty(heatmapExportFolder)) return;
            var files = recorderInstance.ExportHeatmaps(heatmapExportFolder, heatmapResolution);
            if (files.Count > 0)
                EditorUtility.RevealInFinder(files[0]);
            Debug.Log($"Exported {files.Count} heatmap files to {heatmapExportFolder}");
        }

        private void FindOrCreateRecorderInScene()
        {
            if (InputRecorder.InstanceExists)
            {
                recorderInstance = InputRecorder.Instance;
                EditorGUIUtility.PingObject(recorderInstance.gameObject);
                Selection.activeGameObject = recorderInstance.gameObject;
            }
            else
            {
                recorderInstance = InputRecorder.Instance;
                recorderInstance.SetDefaultKeycodesAndActions();
                Selection.activeGameObject = recorderInstance.gameObject;
                if (!Application.isPlaying)
                    Undo.RegisterCreatedObjectUndo(recorderInstance.gameObject, "Create InputRecorder");
            }
        }

        private string SanitizeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }
    }

}