// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Bindings;
using UnityEngine.Scripting;
using UnityEditor.Modules;

namespace UnityEditor.Build.Profile
{
    /// <summary>
    /// Provides a set of configuration settings you can use to build your application on a particular platform.
    /// </summary>
    [RequiredByNativeCode(GenerateProxy = true)]
    [StructLayout(LayoutKind.Sequential)]
    [ExcludeFromObjectFactory]
    [ExcludeFromPreset]
    public sealed partial class BuildProfile : ScriptableObject
    {
        /// <summary>
        /// Build Target used to fetch module and build profile extension.
        /// </summary>
        [SerializeField] BuildTarget m_BuildTarget = BuildTarget.NoTarget;
        [VisibleToOtherModules]
        internal BuildTarget buildTarget
        {
            get => m_BuildTarget;
            set => m_BuildTarget = value;
        }

        /// <summary>
        /// Subtarget, Default for all non-Standalone platforms.
        /// </summary>
        [SerializeField] StandaloneBuildSubtarget m_Subtarget;
        [VisibleToOtherModules]
        internal StandaloneBuildSubtarget subtarget
        {
            get => m_Subtarget;
            set => m_Subtarget = value;
        }

        /// <summary>
        /// Module name used to fetch build profiles.
        /// </summary>
        string m_ModuleName;
        [VisibleToOtherModules]
        internal string moduleName
        {
            get => m_ModuleName;
            set => m_ModuleName = value;
        }

        /// <summary>
        /// Platform ID of the build profile.
        /// Correspond to platform GUID in <see cref="BuildTargetDiscovery"/>
        /// </summary>
        [SerializeField] string m_PlatformId;
        [VisibleToOtherModules]
        internal string platformId
        {
            get => m_PlatformId;
            set => m_PlatformId = value;
        }

        /// <summary>
        /// Platform module specific build settings; e.g. AndroidBuildSettings.
        /// </summary>
        [SerializeReference] BuildProfilePlatformSettingsBase m_PlatformBuildProfile;
        [VisibleToOtherModules]
        internal BuildProfilePlatformSettingsBase platformBuildProfile
        {
            get => m_PlatformBuildProfile;
            set => m_PlatformBuildProfile = value;
        }

        /// <summary>
        /// List of scenes specified in the build profile.
        /// </summary>
        [SerializeField] private EditorBuildSettingsScene[] m_Scenes = Array.Empty<EditorBuildSettingsScene>();
        public EditorBuildSettingsScene[] scenes
        {
            get
            {
                CheckSceneListConsistency();
                return m_Scenes;
            }
            set
            {
                if (m_Scenes == value)
                    return;

                m_Scenes = value;
                CheckSceneListConsistency();

                if (this == BuildProfileContext.instance.activeProfile)
                    EditorBuildSettings.SceneListChanged();
            }
        }

        /// <summary>
        /// Scripting Compilation Defines used during player and editor builds.
        /// </summary>
        /// <remarks>
        /// <see cref="EditorUserBuildSettings.GetActiveProfileYamlScriptingDefines"/> fetches active profile
        /// define be deserializing the YAML file and assumes defines will be found under "m_ScriptingDefines" node.
        /// </remarks>
        [SerializeField] private string[] m_ScriptingDefines = Array.Empty<string>();
        public string[] scriptingDefines
        {
            get => m_ScriptingDefines;
            set => m_ScriptingDefines = value;
        }

        [SerializeField]
        PlayerSettingsYaml m_PlayerSettingsYaml = new();

        PlayerSettings m_PlayerSettings;
        [VisibleToOtherModules]
        internal PlayerSettings playerSettings
        {
            get { return m_PlayerSettings; }

            set { m_PlayerSettings = value; }
        }

        // TODO: Return server IBuildTargets for server build profiles. (https://jira.unity3d.com/browse/PLAT-6612)
        /// <summary>
        /// Get the IBuildTarget of the build profile.
        /// </summary>
        internal IBuildTarget GetIBuildTarget() => ModuleManager.GetIBuildTarget(buildTarget);

        /// <summary>
        /// Returns true if the given <see cref="BuildProfile"/> is the active profile or a classic
        /// profile for the EditorUserBuildSettings active build target.
        /// </summary>
        [VisibleToOtherModules]
        internal bool IsActiveBuildProfileOrPlatform()
        {
            if (BuildProfileContext.instance.activeProfile == this)
                return true;

            if (BuildProfileContext.instance.activeProfile is not null
                || !BuildProfileContext.IsClassicPlatformProfile(this))
                return false;

            var activePlatformId = BuildProfileModuleUtil.GetPlatformId(
                EditorUserBuildSettings.activeBuildTarget, EditorUserBuildSettings.standaloneBuildSubtarget);
            return platformId == activePlatformId;
        }

        [VisibleToOtherModules]
        internal bool CanBuildLocally()
        {
            // Note: A platform build profile may have a non-null value even if its module is not installed.
            // This scenario is true for server platform profiles, which are the same type as the standalone one.
            return platformBuildProfile != null && BuildProfileModuleUtil.IsModuleInstalled(platformId);
        }

        internal string GetLastRunnableBuildPathKey()
        {
            if (platformBuildProfile == null)
                return string.Empty;

            var key = platformBuildProfile.GetLastRunnableBuildPathKey();
            if (string.IsNullOrEmpty(key) || BuildProfileContext.IsClassicPlatformProfile(this))
                return key;

            string assetPath = AssetDatabase.GetAssetPath(this);
            return BuildProfileModuleUtil.GetLastRunnableBuildKeyFromAssetPath(assetPath, key);
        }

        void OnEnable()
        {
            ValidateDataConsistency();

            moduleName = BuildProfileModuleUtil.GetModuleName(platformId);

            // Check if the platform support module has been installed,
            // and try to set an uninitialized platform settings.
            if (platformBuildProfile == null)
                TryCreatePlatformSettings();

            onBuildProfileEnable?.Invoke(this);
            LoadPlayerSettings();

            if (!EditorUserBuildSettings.isBuildProfileAvailable
                || BuildProfileContext.instance.activeProfile != this)
                return;

            // On disk changes invoke OnEnable,
            // Check against the last observed editor defines.
            string[] lastCompiledDefines = BuildProfileContext.instance.cachedEditorScriptingDefines;
            if (ArrayUtility.ArrayEquals(m_ScriptingDefines, lastCompiledDefines))
            {
                return;
            }
            BuildProfileContext.instance.cachedEditorScriptingDefines = m_ScriptingDefines;
            BuildProfileModuleUtil.RequestScriptCompilation(this);
        }

        void OnDisable()
        {
            RemovePlayerSettings();

            // Active profile YAML may be read from disk during startup or
            // Asset Database refresh, flush pending changes to disk.
            if (BuildProfileContext.instance.activeProfile != this)
                return;

            AssetDatabase.SaveAssetIfDirty(this);
        }

        void ValidateDataConsistency()
        {
            // TODO: Remove migration code (https://jira.unity3d.com/browse/PLAT-8909)
            // Set platform ID for build profiles created before it is introduced.
            if (string.IsNullOrEmpty(platformId))
            {
                platformId = BuildProfileContext.IsSharedProfile(buildTarget) ?
                    new GUID(string.Empty).ToString() : BuildProfileModuleUtil.GetPlatformId(buildTarget, subtarget);
                EditorUtility.SetDirty(this);
            }
            else
            {
                var (curBuildTarget, curSubtarget) = BuildProfileModuleUtil.GetBuildTargetAndSubtarget(platformId);
                if (buildTarget != curBuildTarget || subtarget != curSubtarget)
                {
                    buildTarget = curBuildTarget;
                    subtarget = curSubtarget;
                    EditorUtility.SetDirty(this);
                }
            }

            CheckSceneListConsistency();
        }

        /// <summary>
        /// EditorBuildSettingsScene stores both path and GUID. Path can become
        /// invalid when scenes are moved or renamed and must be recalculated.
        /// </summary>
        /// <see cref="EditorBuildSettings"/> native function, EnsureScenesAreValid.
        void CheckSceneListConsistency()
        {
            int length = m_Scenes.Length;
            for (int i = length - 1; i >= 0; i--)
            {
                var scene = m_Scenes[i];

                // EditorBuildSettingScene entry may be null.
                if (scene == null)
                {
                    RemoveAt(i);
                    continue;
                }

                bool isGuidValid = !scene.guid.Empty();
                if (isGuidValid)
                {
                    // Scene may have been moved or renamed.
                    scene.path = AssetDatabase.GUIDToAssetPath(scene.guid);
                }

                if (string.IsNullOrEmpty(scene.path))
                {
                    // Scene Object may have been deleted from disk.
                    RemoveAt(i);
                    continue;
                }

                if (!isGuidValid)
                    scene.guid = AssetDatabase.GUIDFromAssetPath(scene.path);

                // Asset may have been deleted. AssetDatabase may cache GUID to/from
                // path mapping.
                if (AssetDatabase.GetMainAssetTypeFromGUID(scene.guid) is null)
                    RemoveAt(i);
            }

            if (length == m_Scenes.Length)
                return;

            var result = new EditorBuildSettingsScene[length];
            Array.Copy(m_Scenes, result, length);
            m_Scenes = result;
            EditorUtility.SetDirty(this);
            return;

            void RemoveAt(int index)
            {
                length--;
                Array.Copy(m_Scenes, index + 1, m_Scenes, index, length - index);
            }
        }
    }
}
