using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Kaddumi.UnityTools.ToolManager.Editor
{
    /// <summary>
    /// Discovers every <see cref="IEditorToolModule"/> in the project, remembers each one's on/off
    /// state in <c>EditorPrefs</c>, and drives their <see cref="IEditorToolModule.OnActivated"/> /
    /// <see cref="IEditorToolModule.OnDeactivated"/> lifecycle. Enabled tools are (re)activated
    /// automatically whenever the editor loads or scripts recompile.
    ///
    /// This is the single source of truth for tool state; the manager window is just a view over it.
    /// </summary>
    [InitializeOnLoad]
    public static class EditorToolRegistry
    {
        private const string EnabledSuffix = ".Enabled";

        private static readonly List<IEditorToolModule> _modules;
        private static readonly HashSet<string> _activeIds = new HashSet<string>();

        /// <summary> All discovered tool modules, ordered by category then display name. </summary>
        public static IReadOnlyList<IEditorToolModule> Modules => _modules;

        /// <summary> Raised whenever any tool's enabled state changes, so open windows can repaint. </summary>
        public static event Action StateChanged;

        static EditorToolRegistry()
        {
            _modules = DiscoverModules();

            // Defer activation until the editor has finished loading: some modules subscribe to
            // callbacks or touch windows that are not ready inside the static constructor.
            EditorApplication.delayCall += ActivateEnabledModules;
        }

        /// <summary> Whether the given tool is currently switched on (persisted across sessions). </summary>
        public static bool IsEnabled(IEditorToolModule module)
        {
            if (module == null)
                return false;
            return EditorPrefs.GetBool(EnabledKey(module), module.DefaultEnabled);
        }

        /// <summary> Whether the given tool has actually been activated this session. </summary>
        public static bool IsActive(IEditorToolModule module)
        {
            return module != null && _activeIds.Contains(module.Id);
        }

        /// <summary> Turn a tool on or off, persisting the choice and running its lifecycle callbacks. </summary>
        public static void SetEnabled(IEditorToolModule module, bool enabled)
        {
            if (module == null)
                return;

            EditorPrefs.SetBool(EnabledKey(module), enabled);

            if (enabled)
                Activate(module);
            else
                Deactivate(module);

            StateChanged?.Invoke();
        }

        private static void ActivateEnabledModules()
        {
            foreach (IEditorToolModule module in _modules)
            {
                if (IsEnabled(module))
                    Activate(module);
            }
        }

        private static void Activate(IEditorToolModule module)
        {
            if (!_activeIds.Add(module.Id))
                return; // already active

            try
            {
                module.OnActivated();
            }
            catch (Exception e)
            {
                _activeIds.Remove(module.Id);
                Debug.LogError($"[Tool Manager] Failed to activate '{module.DisplayName}': {e}");
            }
        }

        private static void Deactivate(IEditorToolModule module)
        {
            if (!_activeIds.Remove(module.Id))
                return; // already inactive

            try
            {
                module.OnDeactivated();
            }
            catch (Exception e)
            {
                Debug.LogError($"[Tool Manager] Failed to deactivate '{module.DisplayName}': {e}");
            }
        }

        private static string EnabledKey(IEditorToolModule module) => module.Id + EnabledSuffix;

        private static List<IEditorToolModule> DiscoverModules()
        {
            var modules = new List<IEditorToolModule>();
            var seenIds = new HashSet<string>();

            foreach (Type type in TypeCache.GetTypesDerivedFrom<IEditorToolModule>())
            {
                if (type.IsAbstract || type.IsInterface || type.IsGenericTypeDefinition)
                    continue;

                if (type.GetConstructor(Type.EmptyTypes) == null)
                {
                    Debug.LogWarning(
                        $"[Tool Manager] '{type.FullName}' implements IEditorToolModule but has no public " +
                        "parameterless constructor, so it cannot be managed.");
                    continue;
                }

                IEditorToolModule module;
                try
                {
                    module = (IEditorToolModule)Activator.CreateInstance(type);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Tool Manager] Could not instantiate '{type.FullName}': {e}");
                    continue;
                }

                if (string.IsNullOrEmpty(module.Id))
                {
                    Debug.LogWarning($"[Tool Manager] '{type.FullName}' has an empty Id and was skipped.");
                    continue;
                }

                if (!seenIds.Add(module.Id))
                {
                    Debug.LogWarning(
                        $"[Tool Manager] Duplicate tool Id '{module.Id}' on '{type.FullName}' was skipped.");
                    continue;
                }

                modules.Add(module);
            }

            return modules
                .OrderBy(m => string.IsNullOrEmpty(m.Category) ? "General" : m.Category, StringComparer.OrdinalIgnoreCase)
                .ThenBy(m => m.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
