using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Kaddumi.UnityTools.FolderGeneration.Editor
{
    // ----------------------------------------------------------------------------
    // Service Layer: Handles the business logic (I/O and File Generation)
    // Adheres to Single Responsibility Principle (SRP)
    // ----------------------------------------------------------------------------
    public static class FolderGenerationService
    {
        private const string GitKeepFileName = ".gitkeep";

        /// <summary>
        /// Generates the folder structure based on the provided paths.
        /// </summary>
        /// <param name="baseAssetsPath">The absolute path to the Assets folder.</param>
        /// <param name="relativePaths">List of relative paths (e.g., "Scripts/Core").</param>
        /// <param name="createGitKeep">If true, creates a .gitkeep file in every folder.</param>
        /// <returns>A report of the operation.</returns>
        public static GenerationReport GenerateFolders(string baseAssetsPath, IEnumerable<string> relativePaths, bool createGitKeep)
        {
            var report = new GenerationReport();

            if (string.IsNullOrEmpty(baseAssetsPath) || relativePaths == null)
            {
                report.AddError("Invalid arguments provided to FolderGenerationService.");
                return report;
            }

            foreach (string path in relativePaths)
            {
                if (string.IsNullOrWhiteSpace(path)) continue;

                // Normalize path to prevent OS specific issues
                string normalizedPath = path.Trim().Replace('\\', '/');

                // Remove 'Assets/' if the user typed it, as we append it to Application.dataPath manually
                if (normalizedPath.StartsWith("Assets/"))
                {
                    normalizedPath = normalizedPath.Substring("Assets/".Length);
                }

                string fullPath = Path.Combine(baseAssetsPath, normalizedPath);

                try
                {
                    CreateDirectoryAndGitKeep(fullPath, createGitKeep, report);
                }
                catch (Exception ex)
                {
                    report.AddError($"Failed to process path '{normalizedPath}': {ex.Message}");
                }
            }

            return report;
        }

        private static void CreateDirectoryAndGitKeep(string fullPath, bool createGitKeep, GenerationReport report)
        {
            bool directoryCreated = false;

            // 1. Create Directory
            if (!Directory.Exists(fullPath))
            {
                Directory.CreateDirectory(fullPath);
                directoryCreated = true;
                report.FoldersCreated++;
            }

            // 2. Create .gitkeep if requested
            if (createGitKeep)
            {
                string gitKeepPath = Path.Combine(fullPath, GitKeepFileName);
                if (!File.Exists(gitKeepPath))
                {
                    File.WriteAllText(gitKeepPath, string.Empty);
                    report.GitKeepsCreated++;
                }
            }
        }

        public class GenerationReport
        {
            public int FoldersCreated = 0;
            public int GitKeepsCreated = 0;
            public List<string> Errors = new List<string>();

            public void AddError(string error) => Errors.Add(error);
            public bool HasErrors => Errors.Count > 0;
        }
    }

    // ----------------------------------------------------------------------------
    // Presentation Layer: Editor Window
    // Handles User Input and rendering
    // ----------------------------------------------------------------------------
    public class FolderStructureWindow : EditorWindow
    {
        // Default template following standard Unity architecture
        private const string DefaultStructure =
            "Assets\n" +
            "Assets/Assets\n" +
            "Assets/Assets/Art\n" +
            "Assets/Assets/Art/Animations\n" +
            "Assets/Assets/Art/Materials\n" +
            "Assets/Assets/Art/Fonts\n" +
            "Assets/Assets/Art/Models\n" +
            "Assets/Assets/Art/Textures\n" +
            "Assets/Assets/Art/Videos\n" +
            "Assets/Assets/Art/UI\n" +
            "Assets/Assets/Audio\n" +
            "Assets/Assets/Audio/MixGroups\n" +
            "Assets/Assets/Audio/SFX\n" +
            "Assets/Assets/Audio/Music\n" +
            "Assets/Assets/Level\n" +
            "Assets/Assets/Level/Prefabs\n" +
            "Assets/Assets/Level/Scenes\n" +
            "Assets/Assets/Level/Scenes/Development\n" +
            "Assets/Assets/Level/ScriptableObjects\n" +
            "Assets/Assets/Scripts\n" +
            "Assets/Assets/Settings";

        private string folderStructureInput = DefaultStructure;
        private bool generateGitKeep = true;
        private Vector2 scrollPosition;

        [MenuItem("Tools/Project Setup/Folder Generator")]
        public static void ShowWindow()
        {
            var window = GetWindow<FolderStructureWindow>("Folder Generator");
            window.minSize = new Vector2(400, 500);
        }

        private void OnGUI()
        {
            DrawHeader();
            DrawConfiguration();
            DrawActionArea();
        }

        private void DrawHeader()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Folder Structure Generator", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Define your folder structure below. Each line represents a folder path relative to the 'Assets' folder.", MessageType.Info);
            EditorGUILayout.Space(5);
        }

        private void DrawConfiguration()
        {
            EditorGUILayout.LabelField("Structure Definition", EditorStyles.label);

            // Scroll view for the text area
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(300));
            {
                // Clean input style
                GUIStyle textAreaStyle = new GUIStyle(EditorStyles.textArea);
                textAreaStyle.wordWrap = false;

                folderStructureInput = EditorGUILayout.TextArea(folderStructureInput, textAreaStyle, GUILayout.ExpandHeight(true));
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(10);

            // Options
            generateGitKeep = EditorGUILayout.ToggleLeft(" Generate .gitkeep files (Git Support)", generateGitKeep);
            EditorGUILayout.HelpBox("Enabling this will add an empty .gitkeep file to every generated folder, ensuring empty folders are committed to Version Control.", MessageType.None);
        }

        private void DrawActionArea()
        {
            EditorGUILayout.Space(15);

            if (GUILayout.Button("Generate Folders", GUILayout.Height(40)))
            {
                ExecuteGeneration();
            }
        }

        private void ExecuteGeneration()
        {
            if (string.IsNullOrWhiteSpace(folderStructureInput))
            {
                EditorUtility.DisplayDialog("Error", "Please define a folder structure.", "OK");
                return;
            }

            // Prepare Data
            string[] paths = folderStructureInput.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            string assetsPath = Application.dataPath;

            // Execute Service
            EditorUtility.DisplayProgressBar("Generating Folders", "Creating directory structure...", 0.5f);

            var report = FolderGenerationService.GenerateFolders(assetsPath, paths, generateGitKeep);

            // Cleanup
            EditorUtility.ClearProgressBar();
            AssetDatabase.Refresh();

            // Report Results
            ShowResultDialog(report);
        }

        private void ShowResultDialog(FolderGenerationService.GenerationReport report)
        {
            StringBuilder message = new StringBuilder();
            message.AppendLine("Generation Complete!");
            message.AppendLine($"Folders Created: {report.FoldersCreated}");
            message.AppendLine($".gitkeep Files Created: {report.GitKeepsCreated}");

            if (report.HasErrors)
            {
                message.AppendLine("\nErrors encountered:");
                foreach (var error in report.Errors)
                {
                    message.AppendLine($"- {error}");
                }
                EditorUtility.DisplayDialog("Completed with Warnings", message.ToString(), "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Success", message.ToString(), "OK");
            }
        }
    }
}