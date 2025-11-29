# Input Recorder Documentation

## Overview

The **Input Recorder** is a Unity package designed to record and analyze user input during gameplay. This tool is useful for debugging, testing, and creating adaptive AI or systems.

## Features

- Record user input during gameplay.
- Select specific inputs to record (keycodes/mouse clicks for the old input system and actions for the new input system).
- Start or stop recording via the EditorWindow or by calling functions in scripts.
- Display real-time statistics and heatmaps of recorded input during gameplay.
- Export statistics and heatmaps to files through the EditorWindow or by calling functions in scripts.
- Easily integrate into existing Unity projects.

## Requirements

- Unity Editor 2022.3 or later.
- The new input system functionality requires the Input System package.

## Installation

Copy the `Assets/Amaz1ngGames/InputRecorder` folder into your Unity project's `Assets` directory.

## Folder Structure

### `Scripts/`
Contains the core scripts for the Input Recorder functionality:
- **Singleton.cs**: Base class for InputRecorder, ensuring only one instance exists and creating one if it doesn't exist when called.
- **InputRecorder.cs**: Manages the recording and saving of user input.
- **RPSGameController.cs**: Handles the Rock-Paper-Scissors (RPS) game in the sample scene, demonstrating an adaptive AI utilizing InputRecorder.

### `Editor/`
Contains editor scripts to enhance the Unity Editor experience:
- **InputRecorderWindow.cs**: Provides a controller and inspector for the Input Recorder component.

### `Scenes/`
Contains an example scene to demonstrate usage:
- **RPSGameScene.unity**: A sample scene showcasing how to use InputRecorder to build an adaptive AI for the classic Rock-Paper-Scissors (RPS) game, which predicts player input based on past input statistics.

## Usage

### GUI
1. Open your scene and navigate to "Window > Amaz1ng Games > Input Recorder".
2. Click the "Find or Create InputRecorder in Scene" button to create an InputRecorder instance.
3. In the inspector of the created InputRecorder instance, configure the backend and keycodes/actions you want to record.
4. Click the Play button in your scene.
5. Click the "Start" button in the Input Recorder inspector.
6. Interact with your game and observe the statistics and heatmaps.

### Script
1. Use `Amaz1ngGames.InputRecorder.InputRecorder.Instance` to find or create an InputRecorder instance.
2. Configure `backend`, `keyCodesToRecord`, and `actionsToRecord`.
3. Call `StartRecording()` to begin recording.
4. Use `GetStatsSnapshot()` to retrieve the current input statistics.
5. Call `GenerateCsv()`, `ExportCsv(Path)`, `GenerateHeatmaps(resolution)`, or `ExportHeatmaps(folderPath, resolution)` to convert statistics into CSV files or heatmaps.
6. Call `EndRecording()` to stop recording.

## Example

```csharp
// Start recording
InputRecorder inputRecorder = InputRecorder.Instance;
inputRecorder.backend = InputRecorder.InputBackend.OldInput;
inputRecorder.StartRecording();

// Get current stats
var stats = inputRecorder.GetStatsSnapshot();

// Export stats as a CSV file
inputRecorder.ExportCsv(FilePath);

// Export heatmaps as PNG files
inputRecorder.ExportHeatmaps(FolderPath);

// End recording
inputRecorder.EndRecording();
```

## Limitations

1. For the new input system, only actions of `Button` types or `Vector2` control values are recorded.
2. Not all input details, such as timestamps, are recorded.

## Support

For issues or feature requests, visit the [GitHub page](https://github.com/zzxskfd/Input-Recorder) or contact us via [email](mailto:Amaz1ngGames@hotmail.com).
