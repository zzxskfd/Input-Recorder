// File: Assets/Scripts/InputRecorder.cs
// Description: Runtime component that records player inputs for both the old Input system and the new Input System.
// - Supports StartRecording(), EndRecording(), ExportCsv(path), ExportHeatmaps(folderPath).
// - Keeps runtime-accessible stats via GetStats().
// Note: This file uses conditional compilation for the new Input System. If you use the package, enable ENABLE_INPUT_SYSTEM.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Amaz1ngGames.InputRecorder
{
    public class InputRecorder : Singleton<InputRecorder>
    {
        public enum InputBackend
        {
            OldInput,
            NewInputSystem
        }

        [Header("Settings")]
        public InputBackend backend = InputBackend.OldInput;

        // For New Input System: you can assign an InputActionAsset in inspector.
        #if ENABLE_INPUT_SYSTEM
        public InputActionAsset inputActionAsset;
        #endif

        // Recording state
        public bool IsRecording {get; protected set;}
        public double StartTime {get; protected set;}
        public double EndTime {get; protected set;}

        // Old Input System data
        protected Dictionary<KeyCode, int> oldKeyCounts = new();
        protected Dictionary<int, int> oldMouseButtonCounts = new();
        protected List<Vector2> oldMousePositions = new();

        // New Input System data
        protected Dictionary<string, int> newActionCounts = new(); // action name -> count
        protected Dictionary<string, List<Vector2>> newVector2Positions = new();

        public KeyCode[] keyCodesToRecord;

        #if ENABLE_INPUT_SYSTEM
        public List<InputAction> actionsToRecord = new();
        #endif

        // Heatmap cache
        protected readonly Dictionary<(string, int), (int, float[,])> heatmapCache = new();

        public void SetDefault()
        {
            #if ENABLE_INPUT_SYSTEM
            if (inputActionAsset == null)
                inputActionAsset = InputSystem.actions; // Default to current actions if not set
            SetupNewInputSystemRecording();
            #endif

            if (keyCodesToRecord == null || keyCodesToRecord.Count() == 0)
                keyCodesToRecord = (KeyCode[])Enum.GetValues(typeof(KeyCode));
        }

        protected override void Awake()
        {
            base.Awake();
            IsRecording = false;
            SetDefault();
        }

        protected void Update()
        {
            if (!IsRecording) return;

            if (backend == InputBackend.OldInput)
            {
                RecordOldInput();
            }
            else
            {
                // New input system callbacks drive recording; nothing needed every frame unless you want polling
                #if ENABLE_INPUT_SYSTEM
                // Optionally sample anything per-frame here if desired.
                #endif
            }
        }

        #region Old Input System Recording
        protected void RecordOldInput()
        {
            // Record keyboard key downs
            for (int i = 0; i < keyCodesToRecord.Length; i++)
            {
                var kc = keyCodesToRecord[i];
                if (Input.GetKeyDown(kc))
                {
                    if (!oldKeyCounts.ContainsKey(kc))
                        oldKeyCounts[kc] = 0;
                    oldKeyCounts[kc]++;
                }
            }

            // Mouse buttons (0..2 typical)
            for (int button = 0; button <= 2; button++)
            {
                if (Input.GetMouseButtonDown(button))
                {
                    if (!oldMouseButtonCounts.ContainsKey(button))
                        oldMouseButtonCounts[button] = 0;
                    oldMouseButtonCounts[button]++;

                    // record position for heatmap
                    oldMousePositions.Add(Input.mousePosition);
                }
            }
        }
        #endregion

        #region New Input System Recording
        #if ENABLE_INPUT_SYSTEM
        // Called when we enable recording for new Input System
        protected void SetupNewInputSystemRecording()
        {
            ClearNewInputSystemSubscriptions();

            if (inputActionAsset == null)
                return;

            foreach (var map in inputActionAsset.actionMaps)
            {
                foreach (var action in map.actions)
                {
                    // Determine whether this action is Button-like or Vector2-like
                    bool isButton = action.type == InputActionType.Button;
                    bool isVector = action.expectedControlType.ToLower() == "vector2";
                    if (!isButton && !isVector)
                        continue; // Skip non-button and non-vector2 actions

                    // We'll subscribe to performed for both; we'll parse value type at callback time
                    action.performed += OnNewActionPerformed;
                    actionsToRecord.Add(action);

                    // initialize counters/containers
                    if (!newActionCounts.ContainsKey(action.name))
                        newActionCounts[action.name] = 0;
                    if (isVector && !newVector2Positions.ContainsKey(action.name))
                        newVector2Positions[action.name] = new List<Vector2>();
                }
            }
        }

        protected void ClearNewInputSystemSubscriptions()
        {
            if (actionsToRecord == null)
                actionsToRecord = new List<InputAction>();
            foreach (var a in actionsToRecord)
            {
                try
                {
                    a.performed -= OnNewActionPerformed;
                    a.Disable();
                }
                catch { }
            }
            actionsToRecord.Clear();
        }

        protected void OnNewActionPerformed(InputAction.CallbackContext ctx)
        {
            if (!IsRecording) return;

            InputAction action = ctx.action;
            if (action == null) return;

            if (!newActionCounts.ContainsKey(action.name))
                newActionCounts[action.name] = 0;
            newActionCounts[action.name]++;

            // Attempt to read a Vector2 value
            if (ctx.control != null && ctx.control.displayName != null)
            {
                TryRecordVector2FromContext(action, ctx);
            }
        }

        protected void TryRecordVector2FromContext(InputAction action, InputAction.CallbackContext ctx)
        {
            // Safe attempt to read Vector2
            if (action.phase == InputActionPhase.Performed)
            {
                // Try to read as Vector2
                try
                {
                    Vector2 v = ctx.ReadValue<Vector2>();
                    // If v is nonzero or control type is Vector2, record
                    if (v != Vector2.zero || action.expectedControlType != null && action.expectedControlType.ToLower() == "vector2")
                    {
                        if (!newVector2Positions.ContainsKey(action.name))
                            newVector2Positions[action.name] = new List<Vector2>();
                        newVector2Positions[action.name].Add(v);
                    }
                }
                catch
                {
                    // not a Vector2 - ignore
                }
            }
        }
        #endif
        #endregion

        #region Public API
        public void StartRecording()
        {
            if (IsRecording) return;
            IsRecording = true;
            StartTime = Time.realtimeSinceStartupAsDouble;
            EndTime = 0;

            // Clear previous data
            oldKeyCounts.Clear();
            oldMouseButtonCounts.Clear();
            oldMousePositions.Clear();
            newActionCounts.Clear();
            newVector2Positions.Clear();

            #if ENABLE_INPUT_SYSTEM
            if (backend == InputBackend.NewInputSystem)
            {
                // If user assigned an asset, set up callbacks
                SetupNewInputSystemRecording();
            }
            #endif
        }

        public void EndRecording()
        {
            if (!IsRecording) return;
            IsRecording = false;
            EndTime = Time.realtimeSinceStartupAsDouble;

            #if ENABLE_INPUT_SYSTEM
            if (backend == InputBackend.NewInputSystem)
                ClearNewInputSystemSubscriptions();
            #endif
        }

        /// <summary>
        /// Generate CSV string summarizing current recorded counts.
        /// </summary>
        public string GenerateCsv()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Backend,StartTime,EndTime,RecordedDurationSeconds");
            double duration = IsRecording ? (Time.realtimeSinceStartupAsDouble - StartTime) : (EndTime - StartTime);
            sb.AppendLine($"{backend},{StartTime},{(IsRecording ? 0 : EndTime)},{duration:F3}");
            sb.AppendLine();

            if (backend == InputBackend.OldInput)
            {
                sb.AppendLine("Type,KeyOrButton,Count");
                foreach (var kv in oldKeyCounts)
                    sb.AppendLine($"Key,{kv.Key},{kv.Value}");
                foreach (var kv in oldMouseButtonCounts)
                    sb.AppendLine($"MouseButton,{kv.Key},{kv.Value}");
            }
            else
            {
                sb.AppendLine("Action,Count");
                foreach (var kv in newActionCounts)
                    sb.AppendLine($"{kv.Key},{kv.Value}");
            }

            if (backend == InputBackend.OldInput)
            {
                sb.AppendLine();
                sb.AppendLine("MousePositionsX,MousePositionsY");
                List<Vector2> positions = oldMousePositions;
                if (positions != null)
                {
                    foreach (var p in positions)
                        sb.AppendLine($"{p.x},{p.y}");
                }
            }

            if (backend == InputBackend.NewInputSystem)
            {
                // For new input, include all Vector2 positions labeled by action in another section
                foreach (var kv in newVector2Positions)
                {
                    sb.AppendLine();
                    sb.AppendLine($"ActionPositions_{kv.Key}_X,ActionPositions_{kv.Key}_Y");
                    foreach (var p in kv.Value)
                        sb.AppendLine($"{p.x},{p.y}");
                }
            }

            var csv = sb.ToString();
            return csv;
        }

        /// <summary>
        /// Export CSV file summarizing current recorded counts. Returns saved file path.
        /// </summary>
        public void ExportCsv(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogWarning("ExportCsv called with empty path.");
                return;
            }

            var csv = GenerateCsv();
            try
            {
                File.WriteAllText(path, csv);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to write CSV to {path}: {ex.Message}");
            }
        }

        /// <summary>
        /// Generate heatmaps and yields pairs of (KeyName, HeatmapTexture).
        /// </summary>
        public IEnumerable<KeyValuePair<string, Texture2D>> GenerateHeatmaps(int resolution)
        {
            if (backend == InputBackend.OldInput)
            {
                if (oldMousePositions.Count == 0)
                    yield break;
                var tex = BuildHeatmapTextureFromPositions(oldMousePositions, resolution, "MouseClicks");
                yield return new KeyValuePair<string, Texture2D>("MouseClicks", tex);
            }
            else
            {
                #if ENABLE_INPUT_SYSTEM
                foreach (var kv in newVector2Positions)
                {
                    var list = kv.Value;
                    if (list == null || list.Count == 0)
                        continue;
                    var tex = BuildHeatmapTextureFromPositions(list, resolution, kv.Key);
                    yield return new KeyValuePair<string, Texture2D>(kv.Key, tex);
                }
                #endif
            }
        }

        /// <summary>
        /// Export heatmaps to PNG files into folderPath.
        /// For Old Input System: exports one mouse click heatmap named "MouseClicks.png".
        /// For New Input System: exports one PNG per Vector2 action named "{ActionName}.png".
        /// Returns list of saved file paths.
        /// </summary>
        public List<string> ExportHeatmaps(string folderPath, int resolution)
        {
            var files = new List<string>();
            if (string.IsNullOrEmpty(folderPath))
                return files;

            try
            {
                Directory.CreateDirectory(folderPath);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to create folder {folderPath}: {ex.Message}");
                return files;
            }

            var textures = GenerateHeatmaps(resolution);
            foreach (var kv in textures)
            {
                var fileName = $"{SanitizeFileName(kv.Key)}.png";
                var tex = kv.Value;
                var png = tex.EncodeToPNG();
                var path = Path.Combine(folderPath, fileName);
                File.WriteAllBytes(path, png);
                files.Add(path);
                DestroyImmediate(tex);
            }

            if (files.Count == 0)
                Debug.LogWarning("No heatmaps generated to export.");

            return files;
        }

        #endregion

        #region Helpers: heatmap texture builders
        protected Texture2D BuildHeatmapTextureFromPositions(List<Vector2> positions, int resolution, PositionRangeType positionRangeType, Vector2 positionRange, string name=null)
        {
            // We will map screen positions onto a texture of given resolution.
            var tex = new Texture2D(resolution, resolution, TextureFormat.ARGB32, false);

            float rx = positionRange.x;
            float ry = positionRange.y;
            Debug.Assert(rx > 0 & ry > 0);

            int startInd = 0;
            float[,] map;
            // Use cache if name is not null
            if (name != null)
            {
                if (!heatmapCache.ContainsKey((name, resolution)))
                    heatmapCache[(name, resolution)] = (0, new float[resolution, resolution]);
                startInd = heatmapCache[(name, resolution)].Item1;
                map = heatmapCache[(name, resolution)].Item2;
            }
            else
            {
                map = new float[resolution, resolution];
            }

            // Accumulate counts
            if (positionRangeType == PositionRangeType.Center)
            {
                for (int i=startInd; i<positions.Count(); i++)
                {
                    int x = Mathf.RoundToInt((positions[i].x + rx) / rx / 2f * (resolution - 1));
                    int y = Mathf.RoundToInt((positions[i].y + ry) / ry / 2f * (resolution - 1));
                    if (x < 0 || y < 0 || x >= resolution || y >= resolution)
                        continue;       // Remove out of bounds positions
                    map[x, y] += 1f;
                }
            }
            else if (positionRangeType == PositionRangeType.Positive)
            {
                for (int i=startInd; i<positions.Count(); i++)
                {
                    int x = Mathf.RoundToInt(positions[i].x / rx * (resolution - 1));
                    int y = Mathf.RoundToInt(positions[i].y / ry * (resolution - 1));
                    if (x < 0 || y < 0 || x >= resolution || y >= resolution)
                        continue;       // Remove out of bounds positions
                    map[x, y] += 1f;
                }
            }
            else
            {
                Debug.LogError("Unimplemented position range type: " + positionRangeType);
            }

            // Cache map
            if (name != null)
            {
                heatmapCache[(name, resolution)] = (positions.Count(), map);
            }

            // Find max for normalization
            float max = 0f;
            for (int x = 0; x < resolution; x++)
                for (int y = 0; y < resolution; y++)
                    if (map[x, y] > max) max = map[x, y];
            if (max <= 0) max = 1f;

            // Write pixels using a simple grayscale->heat mapping (red for high)
            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float v = map[x, y] / max; // 0..1
                    Color c = HeatColor(v);
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            return tex;
        }



        // Automatically choose position ranges and builds heatmap texture. Auto caching map.
        protected Texture2D BuildHeatmapTextureFromPositions(List<Vector2> positions, int resolution, string name)
        {
            var heatmapParamsDict = new Dictionary<string, (PositionRangeType, Vector2)>
            {
                {"MouseClicks", (PositionRangeType.Positive, new(Camera.main.pixelWidth, Camera.main.pixelHeight))},
                {"Point", (PositionRangeType.Positive, new(Camera.main.pixelWidth, Camera.main.pixelHeight))},
                {"Look", (PositionRangeType.Center, new Vector2(5, 5))},
                {"ScrollWheel", (PositionRangeType.Center, new Vector2(1, 5))},
                {"", (PositionRangeType.Center, new Vector2(1, 1))},    // Default for other actions including "Move"
            };
            if (!heatmapParamsDict.ContainsKey(name))
                name = "";
            return BuildHeatmapTextureFromPositions(positions, resolution, heatmapParamsDict[name].Item1, heatmapParamsDict[name].Item2, name);
        }


        public enum PositionRangeType
        {
            Center,         // [-x, x] * [-y, y]
            Positive,       // [0, x] * [0, y]
        }

        protected Color HeatColor(float value)
        {
            // Simple heat map: black->blue->cyan->yellow->red
            value = Mathf.Clamp01(value);
            if (value <= 0.0f) return new Color(0f, 0f, 0f, 0.2f);
            if (value < 0.25f) return Color.Lerp(Color.black, Color.blue, value / 0.25f);
            if (value < 0.5f) return Color.Lerp(Color.blue, Color.cyan, (value - 0.25f) / 0.25f);
            if (value < 0.75f) return Color.Lerp(Color.cyan, Color.yellow, (value - 0.5f) / 0.25f);
            return Color.Lerp(Color.yellow, Color.red, (value - 0.75f) / 0.25f);
        }

        protected string SanitizeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }
        #endregion

        #region Query runtime stats (for scripts to call)
        /// <summary>
        /// Returns a snapshot of current statistics. Caller should not modify returned collections.
        /// </summary>
        public InputStats GetStatsSnapshot()
        {
            var s = new InputStats
            {
                backend = backend,
                isRecording = IsRecording,
                startTime = StartTime,
                currentTime = Time.realtimeSinceStartupAsDouble,
                oldKeyCounts = new Dictionary<KeyCode, int>(oldKeyCounts),
                oldMouseButtonCounts = new Dictionary<int, int>(oldMouseButtonCounts),
                oldMousePositions = new List<Vector2>(oldMousePositions),
                newActionCounts = new Dictionary<string, int>(newActionCounts),
                newVector2Positions = new Dictionary<string, List<Vector2>>()
            };
            foreach (var kv in newVector2Positions)
                s.newVector2Positions[kv.Key] = new List<Vector2>(kv.Value);
            return s;
        }

        [Serializable]
        public class InputStats
        {
            public InputBackend backend;
            public bool isRecording;
            public double startTime;
            public double currentTime;

            public Dictionary<KeyCode, int> oldKeyCounts;
            public Dictionary<int, int> oldMouseButtonCounts;
            public List<Vector2> oldMousePositions;

            public Dictionary<string, int> newActionCounts;
            public Dictionary<string, List<Vector2>> newVector2Positions;
        }
        #endregion

        protected void OnDisable()
        {
            // Cleanup subscriptions for new input system
            #if ENABLE_INPUT_SYSTEM
            ClearNewInputSystemSubscriptions();
            #endif
        }
    }

}