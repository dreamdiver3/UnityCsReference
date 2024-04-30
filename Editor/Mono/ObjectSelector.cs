// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using UnityEditor.AnimatedValues;
using UnityEditor.IMGUI.Controls;
using UnityEditor.SceneManagement;
using UnityEditor.SearchService;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Audio;
using UnityObject = UnityEngine.Object;
using Scene = UnityEngine.SceneManagement.Scene;

namespace UnityEditor
{
    internal abstract class ObjectSelectorReceiver : ScriptableObject
    {
        public abstract void OnSelectionChanged(UnityObject selection);
        public abstract void OnSelectionClosed(UnityObject selection);
    }

    internal class ObjectSelector : EditorWindow
    {
        // Styles used in the object selector
        static class Styles
        {
            public static GUIStyle smallStatus = "ObjectPickerSmallStatus";
            public static GUIStyle largeStatus = "ObjectPickerLargeStatus";
            public static GUIStyle tab = "ObjectPickerTab";
            public static GUIStyle bottomResize = "WindowBottomResize";
            public static GUIStyle previewBackground = "PopupCurveSwatchBackground"; // TODO: Make dedicated style
            public static GUIStyle previewTextureBackground = "ObjectPickerPreviewBackground"; // TODO: Make dedicated style

            public static GUIContent assetsTabLabel = EditorGUIUtility.TrTextContent("Assets");
            public static GUIContent sceneTabLabel = EditorGUIUtility.TrTextContent("Scene");

            public static readonly GUIContent packagesVisibilityContent = EditorGUIUtility.TrIconContent("SceneViewVisibility", "Number of hidden packages, click to toggle packages visibility");
        }

        public const string ObjectSelectorClosedCommand = "ObjectSelectorClosed";
        public const string ObjectSelectorUpdatedCommand = "ObjectSelectorUpdated";
        public const string ObjectSelectorCanceledCommand = "ObjectSelectorCanceled";
        public const string ObjectSelectorSelectionDoneCommand = "ObjectSelectorSelectionDone";

        // Filters
        string[]        m_RequiredTypes;
        string          m_SearchFilter;

        // Display state
        bool            m_FocusSearchFilter;
        bool            m_AllowSceneObjects;
        bool            m_IsShowingAssets;
        bool            m_SkipHiddenPackages;
        SavedInt        m_StartGridSize = new SavedInt("ObjectSelector.GridSize", 64);

        // Misc
        internal int    objectSelectorID = 0;
        ObjectSelectorReceiver m_ObjectSelectorReceiver;
        int             m_ModalUndoGroup = -1;
        UnityObject     m_OriginalSelection;
        EditorCache     m_EditorCache;
        GUIView         m_DelegateView;
        PreviewResizer  m_PreviewResizer = new PreviewResizer();
        List<int> m_AllowedIDs;

        // Callbacks
        Action<UnityObject> m_OnObjectSelectorClosed;
        Action<UnityObject> m_OnObjectSelectorUpdated;

        ObjectListAreaState m_ListAreaState;
        ObjectListArea  m_ListArea;
        ObjectTreeForSelector m_ObjectTreeWithSearch = new ObjectTreeForSelector();
        UnityObject m_ObjectBeingEdited;
        SerializedProperty m_EditedProperty;
        bool m_ShowNoneItem;

        bool m_SelectionCancelled;
        int m_LastSelectedInstanceId = 0;
        readonly SearchService.ObjectSelectorSearchSessionHandler m_SearchSessionHandler = new SearchService.ObjectSelectorSearchSessionHandler();
        readonly SearchSessionOptions m_LegacySearchSessionOptions = new SearchSessionOptions { legacyOnly = true };

        // Layout
        const float kMinTopSize = 250;
        const float kMinWidth = 200;
        const float kPreviewMargin = 5;
        const float kPreviewExpandedAreaHeight = 75;
        static float kToolbarHeight => EditorGUI.kWindowToolbarHeight;
        static float kTopAreaHeight => kToolbarHeight * 2;
        const float kResizerHeight = 20f;

        float           m_PreviewSize = 0;
        float           m_TopSize = 0;
        AnimBool m_ShowWidePreview = new AnimBool();
        AnimBool m_ShowOverlapPreview = new AnimBool();

        static HashSet<Event> s_GridAreaPriorityKeyboardEvents;

        // Delayer for debouncing search inputs
        private Delayer m_Debounce;

        Rect listPosition
        {
            get
            {
                return new Rect(0, kTopAreaHeight, position.width, Mathf.Max(0f, m_TopSize - kTopAreaHeight));
            }
        }

        public List<int> allowedInstanceIDs
        {
            get { return m_AllowedIDs; }
        }

        public UnityObject objectBeingEdited
        {
            get { return m_ObjectBeingEdited; }
        }

        // get an existing ObjectSelector or create one
        static ObjectSelector s_SharedObjectSelector = null;
        public static ObjectSelector get
        {
            get
            {
                if (s_SharedObjectSelector == null)
                {
                    UnityObject[] objs = Resources.FindObjectsOfTypeAll(typeof(ObjectSelector));
                    if (objs != null && objs.Length > 0)
                        s_SharedObjectSelector = (ObjectSelector)objs[0];
                    if (s_SharedObjectSelector == null)
                        s_SharedObjectSelector = ScriptableObject.CreateInstance<ObjectSelector>();
                }
                return s_SharedObjectSelector;
            }
        }

        public static bool isVisible
        {
            get
            {
                return s_SharedObjectSelector != null;
            }
        }

        bool IsUsingTreeView()
        {
            return m_ObjectTreeWithSearch.IsInitialized();
        }

        // Internal for test purposes only
        internal int GetInternalSelectedInstanceID()
        {
            if (m_ListArea == null)
                InitIfNeeded();
            int[] selection = IsUsingTreeView() ? m_ObjectTreeWithSearch.GetSelection() : m_ListArea.GetSelection();
            if (selection.Length >= 1)
                return selection[0];
            return 0;
        }

        int GetSelectedInstanceID()
        {
            return m_LastSelectedInstanceId;
        }

        [UsedImplicitly]
        void OnEnable()
        {
            hideFlags = HideFlags.DontSave;
            m_ShowOverlapPreview.valueChanged.AddListener(Repaint);
            m_ShowOverlapPreview.speed = 1.5f;
            m_ShowWidePreview.valueChanged.AddListener(Repaint);
            m_ShowWidePreview.speed = 1.5f;

            m_PreviewResizer.Init("ObjectPickerPreview");
            m_PreviewSize = m_PreviewResizer.GetPreviewSize(); // Init size

            if (s_GridAreaPriorityKeyboardEvents == null)
            {
                s_GridAreaPriorityKeyboardEvents = new HashSet<Event>
                {
                    Event.KeyboardEvent("up"),
                    Event.KeyboardEvent("down"),
                };
            }

            AssetPreview.ClearTemporaryAssetPreviews();

            SetupPreview();

            m_Debounce = Delayer.Debounce(_ =>
            {
                FilterSettingsChanged();
                Repaint();
            });
        }

        [UsedImplicitly]
        void OnDisable()
        {
            NotifySelectorClosed(false);
            if (m_ListArea != null)
                m_StartGridSize.value = m_ListArea.gridSize;

            if (s_SharedObjectSelector == this)
                s_SharedObjectSelector = null;
            if (m_EditorCache != null)
                m_EditorCache.Dispose();

            AssetPreview.ClearTemporaryAssetPreviews();
            HierarchyProperty.ClearSceneObjectsFilter();
            m_Debounce?.Dispose();
            m_Debounce = null;
        }

        public void SetupPreview()
        {
            bool open = PreviewIsOpen();
            bool wide = PreviewIsWide();
            m_ShowOverlapPreview.target = m_ShowOverlapPreview.value = (open && !wide);
            m_ShowWidePreview.target = m_ShowWidePreview.value = (open && wide);
        }

        void ListAreaItemSelectedCallback(bool doubleClicked)
        {
            m_LastSelectedInstanceId = GetInternalSelectedInstanceID();
            if (doubleClicked)
            {
                ItemWasDoubleClicked();
            }
            else
            {
                m_FocusSearchFilter = false;
                NotifySelectionChanged(true);
            }
        }

        internal string searchFilter
        {
            get { return m_SearchFilter; }
            set
            {
                if (ObjectSelectorSearch.HasEngineOverride())
                {
                    m_SearchSessionHandler.SetSearchFilter(value);
                    return;
                }
                m_SearchFilter = value;
                m_Debounce?.Execute();
            }
        }

        public ObjectSelectorReceiver objectSelectorReceiver
        {
            get { return m_ObjectSelectorReceiver; }
            set { m_ObjectSelectorReceiver = value; }
        }

        Scene GetSceneFromObject(UnityObject obj)
        {
            var go = obj as GameObject;
            if (go != null)
                return go.scene;

            var component = obj as Component;
            if (component != null)
                return component.gameObject.scene;

            return new Scene();
        }

        // Used by tests
        internal void Internal_TriggerFilterSettingsChanged()
        {
            FilterSettingsChanged();
        }

        void FilterSettingsChanged()
        {
            var filter = GetSearchFilter();
            var hierarchyType = m_IsShowingAssets ? HierarchyType.Assets : HierarchyType.GameObjects;

            bool hasObject = false;
            var requiredTypes = new List<Type>();
            var objectTypes = TypeCache.GetTypesDerivedFrom<UnityEngine.Object>();
            foreach (var type in m_RequiredTypes)
            {
                foreach (var objectType in objectTypes)
                {
                    if (objectType.Name == type)
                        requiredTypes.Add(objectType);
                    else if (!hasObject)
                    {
                        requiredTypes.Add(typeof(UnityObject));
                        hasObject = true;
                    }
                }
            }
            m_ListArea.InitForSearch(listPosition, hierarchyType, filter, true, s =>
            {
                foreach (var type in requiredTypes)
                {
                    var asset = AssetDatabase.LoadAssetAtPath(s, type);
                    if (asset != null && asset.GetInstanceID() != 0)
                        return asset.GetInstanceID();
                }
                return 0;
            }, m_LegacySearchSessionOptions);
        }

        SearchFilter GetSearchFilter()
        {
            var filter = new SearchFilter();
            if (m_IsShowingAssets)
                filter.searchArea = SearchFilter.SearchArea.AllAssets;

            filter.SearchFieldStringToFilter(m_SearchFilter);
            if (filter.classNames.Length == 0 && m_RequiredTypes.All(type => !string.IsNullOrEmpty(type)))
                filter.classNames = m_RequiredTypes;

            var hierarchyType = m_IsShowingAssets ? HierarchyType.Assets : HierarchyType.GameObjects;

            if (hierarchyType == HierarchyType.GameObjects)
            {
                if (m_ObjectBeingEdited != null)
                {
                    var scene = GetSceneFromObject(m_ObjectBeingEdited);
                    if (scene.IsValid())
                    {
                        // We do not support cross scene references so ensure we only show game objects
                        // from the same scene as the object being edited is part of.
                        // Also don't allow references to other scenes if object being edited
                        // is in a preview scene.
                        if (EditorSceneManager.IsPreviewScene(scene) || EditorSceneManager.preventCrossSceneReferences)
                            filter.sceneHandles = new[] { scene.handle };
                    }
                }
                else
                {
                    // If we don't know which object is being edited, assume it's one in current stage.
                    PreviewSceneStage previewSceneStage = StageUtility.GetCurrentStage() as PreviewSceneStage;
                    if (previewSceneStage != null)
                    {
                        filter.sceneHandles = new[] { previewSceneStage.scene.handle };
                    }
                }
            }

            if (hierarchyType == HierarchyType.Assets)
            {
                // When AssemblyDefinitionAsset is the required type, don't skip hidden packages
                foreach (var type in m_RequiredTypes)
                {
                    if (!string.IsNullOrEmpty(type) && type == typeof(AssemblyDefinitionAsset).Name)
                    {
                        m_SkipHiddenPackages = false;
                        break;
                    }
                }
                filter.skipHidden = m_SkipHiddenPackages;
            }

            return filter;
        }

        static bool ShouldTreeViewBeUsed(String typeStr)
        {
            return (String.Equals(typeof(AudioMixerGroup).Name, typeStr));
        }

        private readonly Regex s_MatchPPtrTypeName = new Regex(@"PPtr\<(\w+)\>");

        internal void Show(Type requiredType, SerializedProperty property, bool allowSceneObjects, List<int> allowedInstanceIDs = null, Action<UnityObject> onObjectSelectorClosed = null, Action<UnityObject> onObjectSelectedUpdated = null)
        {
            if (property == null)
                throw new ArgumentNullException(nameof(property));

            if (requiredType == null)
            {
                ScriptAttributeUtility.GetFieldInfoFromProperty(property, out requiredType);
                // case 951876: built-in types do not actually have reflectable fields, so their object types must be extracted from the type string
                // this works because built-in types will only ever have serialized references to other built-in types, which this window's filter expects as unqualified names
                if (requiredType == null)
                    m_RequiredTypes = new string[] { s_MatchPPtrTypeName.Match(property.type).Groups[1].Value };
            }

            // Don't select anything on multi selection
            UnityObject obj = property.hasMultipleDifferentValues ? null : property.objectReferenceValue;

            UnityObject objectBeingEdited = property.serializedObject.targetObject;
            m_EditedProperty = property;

            Show(obj, new Type[] { requiredType }, objectBeingEdited, allowSceneObjects, allowedInstanceIDs, onObjectSelectorClosed, onObjectSelectedUpdated);
        }

        internal void Show(Type[] requiredTypes, SerializedProperty property, bool allowSceneObjects, List<int> allowedInstanceIDs = null, Action<UnityObject> onObjectSelectorClosed = null, Action<UnityObject> onObjectSelectedUpdated = null)
        {
            if (requiredTypes == null)
            {
                Show((Type)null, property, allowSceneObjects, allowedInstanceIDs, onObjectSelectorClosed, onObjectSelectedUpdated);
                return;
            }

            if (property == null)
                throw new ArgumentNullException(nameof(property));

            m_RequiredTypes = new string[requiredTypes.Length];
            for (int i = 0; i < requiredTypes.Length; i++)
            {
                var requiredType = requiredTypes[i];
                if (requiredType == null)
                {
                    ScriptAttributeUtility.GetFieldInfoFromProperty(property, out requiredType);
                    // case 951876: built-in types do not actually have reflectable fields, so their object types must be extracted from the type string
                    // this works because built-in types will only ever have serialized references to other built-in types, which this window's filter expects as unqualified names
                    if (requiredType == null)
                        m_RequiredTypes[i] = s_MatchPPtrTypeName.Match(property.type).Groups[1].Value;
                    else
                        requiredTypes[i] = requiredType;
                }
            }

            // Don't select anything on multi selection
            UnityObject obj = property.hasMultipleDifferentValues ? null : property.objectReferenceValue;

            UnityObject objectBeingEdited = property.serializedObject.targetObject;
            m_EditedProperty = property;

            Show(obj, requiredTypes, objectBeingEdited, allowSceneObjects, allowedInstanceIDs, onObjectSelectorClosed, onObjectSelectedUpdated);
        }

        internal void Show(UnityObject obj, Type requiredType, UnityObject objectBeingEdited, bool allowSceneObjects, List<int> allowedInstanceIDs = null, Action<UnityObject> onObjectSelectorClosed = null, Action<UnityObject> onObjectSelectedUpdated = null, bool showNoneItem = true)
        {
            Show(obj, new Type[] { requiredType }, objectBeingEdited, allowSceneObjects, allowedInstanceIDs, onObjectSelectorClosed, onObjectSelectedUpdated, showNoneItem);
        }

        internal void Show(UnityObject obj, Type[] requiredTypes, UnityObject objectBeingEdited, bool allowSceneObjects, List<int> allowedInstanceIDs = null, Action<UnityObject> onObjectSelectorClosed = null, Action<UnityObject> onObjectSelectedUpdated = null, bool showNoneItem = true)
        {
            // We can't rely on the fact that the window will always be closed when we call Show. For example,
            // if a user clicks on multiple object fields without closing the window first, there is no guarantee
            // that the auxiliary window will close before the click event is processed. And since closing the window
            // cleans up the undo state, we have to force close the window if it wasn't already closed.
            CloseOpenedWindow();
            m_ObjectSelectorReceiver = null;
            m_AllowSceneObjects = allowSceneObjects;
            m_IsShowingAssets = true;
            m_SkipHiddenPackages = true;
            m_AllowedIDs = allowedInstanceIDs;
            m_ObjectBeingEdited = objectBeingEdited;
            m_LastSelectedInstanceId = obj?.GetInstanceID() ?? 0;
            m_SelectionCancelled = false;
            m_ShowNoneItem = showNoneItem;

            m_OnObjectSelectorClosed = onObjectSelectorClosed;
            m_OnObjectSelectorUpdated = onObjectSelectedUpdated;

            // Do not allow to show scene objects if the object being edited is persistent
            if (m_ObjectBeingEdited != null && EditorUtility.IsPersistent(m_ObjectBeingEdited))
                m_AllowSceneObjects = false;

            // Set which tab should be visible at startup
            if (m_AllowSceneObjects)
            {
                if (obj != null)
                {
                    if (typeof(Component).IsAssignableFrom(obj.GetType()))
                    {
                        obj = ((Component)obj).gameObject;
                    }
                    // Set the right tab visible (so we can see our selection)
                    m_IsShowingAssets = EditorUtility.IsPersistent(obj);
                }
                else
                {
                    foreach (var requiredType in requiredTypes)
                        m_IsShowingAssets &= (requiredType != typeof(GameObject) && !typeof(Component).IsAssignableFrom(requiredType));
                }
            }
            else
            {
                m_IsShowingAssets = true;
            }

            // Set member variables
            m_DelegateView = GUIView.current;
            // type filter requires unqualified names for built-in types, but will prioritize them over user types, so ensure user types are namespace-qualified
            if (m_RequiredTypes == null || m_RequiredTypes.Length != requiredTypes.Length)
                m_RequiredTypes = new string[requiredTypes.Length];
            for (int i = 0; i < requiredTypes.Length; i++)
            {
                if (requiredTypes[i] != null)
                    m_RequiredTypes[i] = typeof(ScriptableObject).IsAssignableFrom(requiredTypes[i]) || typeof(MonoBehaviour).IsAssignableFrom(requiredTypes[i]) ? requiredTypes[i].FullName : requiredTypes[i].Name;
            }
            m_SearchFilter = "";
            m_OriginalSelection = obj;
            m_ModalUndoGroup = Undo.GetCurrentGroup();

            // Show custom selector if available
            if (ObjectSelectorSearch.HasEngineOverride())
            {
                m_SearchSessionHandler.BeginSession(() =>
                {
                    return new SearchService.ObjectSelectorSearchContext
                    {
                        currentObject = obj,
                        editedObjects = m_EditedProperty != null ? m_EditedProperty.serializedObject.targetObjects : new[] { objectBeingEdited },
                        requiredTypes = requiredTypes,
                        requiredTypeNames = m_RequiredTypes,
                        allowedInstanceIds = allowedInstanceIDs,
                        visibleObjects = allowSceneObjects ? SearchService.VisibleObjects.All : SearchService.VisibleObjects.Assets,
                        searchFilter = GetSearchFilter()
                    };
                });

                Action<UnityObject> onSelectionChanged = selectedObj =>
                {
                    m_LastSelectedInstanceId = selectedObj == null ? 0 : selectedObj.GetInstanceID();
                    NotifySelectionChanged(false);
                };
                Action<UnityObject, bool> onSelectorClosed = (selectedObj, canceled) =>
                {
                    m_SearchSessionHandler.EndSession();
                    if (canceled)
                    {
                        // Undo changes we have done in the ObjectSelector
                        Undo.RevertAllDownToGroup(m_ModalUndoGroup);
                        m_LastSelectedInstanceId = 0;
                        m_SelectionCancelled = true;
                    }
                    else
                    {
                        m_LastSelectedInstanceId = selectedObj == null ? 0 : selectedObj.GetInstanceID();
                        NotifySelectionChanged(false);
                    }

                    m_EditedProperty = null;
                    NotifySelectorClosed(false);
                };

                if (m_SearchSessionHandler.SelectObject(onSelectorClosed, onSelectionChanged))
                    return;
                else
                    m_SearchSessionHandler.EndSession();
            }

            // Freeze to prevent flicker on OSX.
            // Screen will be updated again when calling
            // SetFreezeDisplay(false) further down.
            ContainerWindow.SetFreezeDisplay(true);

            var shouldRepositionWindow = m_Parent != null;
            ShowWithMode(ShowMode.AuxWindow);

            titleContent = EditorGUIUtility.TrTextContent(GenerateTitleContent(requiredTypes, m_RequiredTypes));

            // Deal with window size
            if (shouldRepositionWindow)
            {
                m_Parent.window.LoadInCurrentMousePosition();
                m_Parent.window.FitWindowToScreen(true);
            }
            Rect p = m_Parent == null ? new Rect(0, 0, 1, 1) : m_Parent.window.position;
            p.width = EditorPrefs.GetFloat("ObjectSelectorWidth", 200);
            p.height = EditorPrefs.GetFloat("ObjectSelectorHeight", 390);
            position = p;
            minSize = new Vector2(kMinWidth, kMinTopSize + kPreviewExpandedAreaHeight + 2 * kPreviewMargin);
            maxSize = new Vector2(10000, 10000);
            SetupPreview();

            // Focus
            Focus();
            ContainerWindow.SetFreezeDisplay(false);

            m_FocusSearchFilter = true;

            // Add after unfreezing display because AuxWindowManager.cpp assumes that aux windows are added after we get 'got/lost'- focus calls.
            if (m_Parent != null)
                m_Parent.AddToAuxWindowList();

            // Initial selection
            int initialSelection = obj != null ? obj.GetInstanceID() : 0;

            if (initialSelection != 0)
            {
                var assetPath = AssetDatabase.GetAssetPath(initialSelection);
                if (m_SkipHiddenPackages && !PackageManagerUtilityInternal.IsPathInVisiblePackage(assetPath))
                    m_SkipHiddenPackages = false;
            }

            if (m_RequiredTypes.All(t => ShouldTreeViewBeUsed(t)))
            {
                m_ObjectTreeWithSearch.Init(position, this, CreateAndSetTreeView, TreeViewSelection, ItemWasDoubleClicked, initialSelection, 0);
            }
            else
            {
                // To frame the selected item we need to wait to initialize the search until our window has been setup
                InitIfNeeded();
                m_ListArea.InitSelection(new[] { initialSelection });
                if (initialSelection != 0)
                    m_ListArea.Frame(initialSelection, true, false);
            }
        }

        void CloseOpenedWindow()
        {
            // We check m_ModalUndoGroup as it is the only value that will be reliably set when the window is open
            // and unset when the window is closed. Checking m_OnObjectSelectorClosed or m_ObjectSelectorReceiver is not enough
            // as they are not always set.
            if (m_ModalUndoGroup >= 0)
            {
                if (ObjectSelectorSearch.HasEngineOverride())
                {
                    m_SearchSessionHandler.CloseSelector();
                }
                else
                {
                    NotifySelectorClosed(false);
                }
            }
        }

        internal static string GenerateTitleContent(Type[] requiredTypes, string[] requiredTypeStrings)
        {
            var typeName = requiredTypes[0] == null ? requiredTypeStrings[0] : requiredTypes[0].Name;
            var text = "Select " + ObjectNames.NicifyVariableName(typeName);

            for (int i = 1; i < requiredTypes.Length; i++)
            {
                typeName = requiredTypes[i] == null ? requiredTypeStrings[i] : requiredTypes[i].Name;
                text += (i == requiredTypes.Length - 1 ? " or " : ", ") + ObjectNames.NicifyVariableName(typeName);
            }

            return text;
        }

        void ItemWasDoubleClicked()
        {
            SendEvent(ObjectSelectorSelectionDoneCommand, false);

            Close();
            GUIUtility.ExitGUI();
        }

        // TreeView Section

        void CreateAndSetTreeView(ObjectTreeForSelector.TreeSelectorData data)
        {
            TreeViewForAudioMixerGroup.CreateAndSetTreeView(data);
        }

        void TreeViewSelection(TreeViewItem item)
        {
            m_LastSelectedInstanceId = GetInternalSelectedInstanceID();
            NotifySelectionChanged(true);
        }

        // Grid Section

        void InitIfNeeded()
        {
            if (m_ListAreaState == null)
                m_ListAreaState = new ObjectListAreaState(); // is serialized

            if (m_ListArea == null)
            {
                m_ListArea = new ObjectListArea(m_ListAreaState, this, m_ShowNoneItem);
                m_ListArea.allowDeselection = false;
                m_ListArea.allowDragging = false;
                m_ListArea.allowFocusRendering = false;
                m_ListArea.allowMultiSelect = false;
                m_ListArea.allowRenaming = false;
                m_ListArea.allowBuiltinResources = true;
                m_ListArea.repaintCallback += Repaint;
                m_ListArea.itemSelectedCallback += ListAreaItemSelectedCallback;
                m_ListArea.gridSize = m_StartGridSize.value;

                FilterSettingsChanged();
            }
        }

        public static bool SelectionCanceled()
        {
            return ObjectSelector.get.m_SelectionCancelled;
        }

        public static UnityObject GetCurrentObject()
        {
            return EditorUtility.InstanceIDToObject(ObjectSelector.get.GetSelectedInstanceID());
        }

        // This is the public Object that the inspector might revert to
        public static UnityObject GetInitialObject()
        {
            return ObjectSelector.get.m_OriginalSelection;
        }

        // This is our search field
        void SearchArea()
        {
            GUI.Label(new Rect(0, 0, position.width, kToolbarHeight), GUIContent.none, EditorStyles.toolbar);

            // ESC clears search field and removes it's focus. But if we get an esc event we only want to clear search field.
            // So we need special handling afterwards.
            bool wasEscape = Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape;

            GUI.SetNextControlName("SearchFilter");
            string searchFilter = EditorGUI.ToolbarSearchField(new Rect(2, 2, position.width - 2, 16), m_SearchFilter, false);

            if (wasEscape && Event.current.type == EventType.Used)
            {
                // If we hit esc and the string WAS empty, it's an actual cancel event.
                if (m_SearchFilter == "")
                    Cancel();

                // Otherwise the string has been cleared and focus has been lost. We don't have anything else to recieve focus, so we want to refocus the search field.
                m_FocusSearchFilter = true;
            }

            if (searchFilter != m_SearchFilter || m_FocusSearchFilter)
            {
                m_SearchFilter = searchFilter;
                m_Debounce.Execute();
            }

            if (m_FocusSearchFilter)
            {
                EditorGUI.FocusTextInControl("SearchFilter");
                m_FocusSearchFilter = false;
            }

            GUI.changed = false;

            GUI.Label(new Rect(0, kToolbarHeight, position.width, kToolbarHeight), GUIContent.none, EditorStyles.toolbar);

            // TAB BAR
            GUILayout.BeginArea(new Rect(4, kToolbarHeight, position.width - 4, kToolbarHeight));
            GUILayout.BeginHorizontal();

            // Asset Tab
            bool showAssets = GUILayout.Toggle(m_IsShowingAssets, Styles.assetsTabLabel, Styles.tab);
            if (!m_IsShowingAssets && showAssets)
                m_IsShowingAssets = true;


            // The Scene Tab
            if (!m_AllowSceneObjects)
            {
                GUI.enabled = false;
                GUI.color = new Color(1, 1, 1, 0);
            }

            bool showingSceneTab = !m_IsShowingAssets;
            showingSceneTab = GUILayout.Toggle(showingSceneTab, Styles.sceneTabLabel, Styles.tab);
            if (m_IsShowingAssets && showingSceneTab)
                m_IsShowingAssets = false;


            if (!m_AllowSceneObjects)
            {
                GUI.color = new Color(1, 1, 1, 1);
                GUI.enabled = true;
            }

            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            if (GUI.changed)
                m_Debounce.Execute();

            var size = new Vector2(0, 0);
            if (m_IsShowingAssets)
            {
                Styles.packagesVisibilityContent.text = PackageManagerUtilityInternal.HiddenPackagesCount.ToString();
                size = EditorStyles.toolbarButton.CalcSize(Styles.packagesVisibilityContent);
            }

            if (m_ListArea.CanShowThumbnails())
            {
                EditorGUI.BeginChangeCheck();
                var newGridSize = (int)GUI.HorizontalSlider(new Rect(position.width - (60 + size.x), kToolbarHeight + GUI.skin.horizontalSlider.margin.top, 55, EditorGUI.kSingleLineHeight), m_ListArea.gridSize, m_ListArea.minGridSize, m_ListArea.maxGridSize);
                if (EditorGUI.EndChangeCheck())
                {
                    m_ListArea.gridSize = newGridSize;
                }
            }

            if (m_IsShowingAssets)
            {
                EditorGUI.BeginChangeCheck();
                var skipHiddenPackages = GUI.Toggle(new Rect(position.width - size.x, kToolbarHeight, size.x, EditorStyles.toolbarButton.fixedHeight), m_SkipHiddenPackages, Styles.packagesVisibilityContent, EditorStyles.toolbarButtonRight);
                if (EditorGUI.EndChangeCheck())
                {
                    m_SkipHiddenPackages = skipHiddenPackages;
                    FilterSettingsChanged();
                }
            }
        }

        [UsedImplicitly]
        void OnInspectorUpdate()
        {
            if (m_ListArea != null && AssetPreview.HasAnyNewPreviewTexturesAvailable(m_ListArea.GetAssetPreviewManagerID()))
                Repaint();
        }

        // This is the preview area at the bottom of the screen
        void PreviewArea()
        {
            GUI.Box(new Rect(0, m_TopSize, position.width, m_PreviewSize), "", Styles.previewBackground);

            if (m_ListArea.GetSelection().Length == 0)
                return;

            EditorWrapper p = null;
            UnityObject selectedObject = GetCurrentObject();
            if (m_PreviewSize < kPreviewExpandedAreaHeight)
            {
                // Get info string
                string s;
                if (selectedObject != null)
                {
                    p = m_EditorCache[selectedObject];
                    string typeName = ObjectNames.NicifyVariableName(selectedObject.GetType().Name);
                    if (p != null)
                        s = p.name + " (" + typeName + ")";
                    else
                        s = selectedObject.name + " (" + typeName + ")";

                    s += "      " + AssetDatabase.GetAssetPath(selectedObject);
                }
                else
                    s = "None";

                LinePreview(s, selectedObject, p);
            }
            else
            {
                if (m_EditorCache == null)
                    m_EditorCache = new EditorCache(EditorFeatures.PreviewGUI);

                // Get info string
                string s;
                if (selectedObject != null)
                {
                    p = m_EditorCache[selectedObject];
                    string typeName = ObjectNames.NicifyVariableName(selectedObject.GetType().Name);
                    if (p != null)
                    {
                        s = p.GetInfoString();
                        if (s != "")
                            s = p.name + "\n" + typeName + "\n" + s;
                        else
                            s = p.name + "\n" + typeName;
                    }
                    else
                    {
                        s = selectedObject.name + "\n" + typeName;
                    }

                    s += "\n" + AssetDatabase.GetAssetPath(selectedObject);
                }
                else
                    s = "None";

                // Make previews
                if (m_ShowWidePreview.faded != 0.0f)
                {
                    GUI.color = new Color(1, 1, 1, m_ShowWidePreview.faded);
                    WidePreview(m_PreviewSize, s, selectedObject, p);
                }
                if (m_ShowOverlapPreview.faded != 0.0f)
                {
                    GUI.color = new Color(1, 1, 1, m_ShowOverlapPreview.faded);
                    OverlapPreview(m_PreviewSize, s, selectedObject, p);
                }
                GUI.color = Color.white;
                m_EditorCache.CleanupUntouchedEditors();
            }
        }

        void WidePreview(float actualSize, string s, UnityObject o, EditorWrapper p)
        {
            float margin = kPreviewMargin;
            Rect previewRect = new Rect(margin, m_TopSize + margin, actualSize - margin * 2, actualSize - margin * 2);

            Rect labelRect = new Rect(m_PreviewSize + 3, m_TopSize + (m_PreviewSize - kPreviewExpandedAreaHeight) * 0.5f, m_Parent.window.position.width - m_PreviewSize - 3 - margin, kPreviewExpandedAreaHeight);

            if (p != null && p.HasPreviewGUI())
                p.OnPreviewGUI(previewRect, Styles.previewTextureBackground);
            else if (o != null)
                DrawObjectIcon(previewRect, m_ListArea.m_SelectedObjectIcon);

            var prevClipping = Styles.smallStatus.clipping;
            Styles.smallStatus.clipping = TextClipping.Overflow;
            if (EditorGUIUtility.isProSkin)
                EditorGUI.DropShadowLabel(labelRect, s, Styles.smallStatus);
            else
                GUI.Label(labelRect, s, Styles.smallStatus);
            Styles.smallStatus.clipping = prevClipping;
        }

        void OverlapPreview(float actualSize, string s, UnityObject o, EditorWrapper p)
        {
            float margin = kPreviewMargin;
            Rect previewRect = new Rect(margin, m_TopSize + margin, position.width - margin * 2, actualSize - margin * 2);

            if (p != null && p.HasPreviewGUI())
                p.OnPreviewGUI(previewRect, Styles.previewTextureBackground);
            else if (o != null)
                DrawObjectIcon(previewRect, m_ListArea.m_SelectedObjectIcon);

            if (EditorGUIUtility.isProSkin)
                EditorGUI.DropShadowLabel(previewRect, s, Styles.largeStatus);
            else
                EditorGUI.DoDropShadowLabel(previewRect, EditorGUIUtility.TempContent(s), Styles.largeStatus, .3f);
        }

        void LinePreview(string s, UnityObject o, EditorWrapper p)
        {
            if (m_ListArea.m_SelectedObjectIcon != null)
                GUI.DrawTexture(new Rect(2, (int)(m_TopSize + 2), 16, 16), m_ListArea.m_SelectedObjectIcon, ScaleMode.StretchToFill);
            Rect labelRect = new Rect(20, m_TopSize + 1, position.width - 22, 18);
            if (EditorGUIUtility.isProSkin)
                EditorGUI.DropShadowLabel(labelRect, s, Styles.smallStatus);
            else
                GUI.Label(labelRect, s, Styles.smallStatus);
        }

        void DrawObjectIcon(Rect position, Texture icon)
        {
            if (icon == null)
                return;
            int size = Mathf.Min((int)position.width, (int)position.height);
            if (size >= icon.width * 2)
                size = icon.width * 2;

            FilterMode temp = icon.filterMode;
            icon.filterMode = FilterMode.Point;
            GUI.DrawTexture(new Rect(position.x + ((int)position.width - size) / 2, position.y + ((int)position.height - size) / 2, size, size), icon, ScaleMode.ScaleToFit);
            icon.filterMode = temp;
        }

        // Resize the preview area
        void ResizeBottomPartOfWindow()
        {
            GUI.changed = false;

            // Handle preview size
            m_PreviewSize = m_PreviewResizer.ResizeHandle(position, kPreviewExpandedAreaHeight + kPreviewMargin * 2 - kResizerHeight, kMinTopSize + kResizerHeight, kResizerHeight) + kResizerHeight;
            m_TopSize = position.height - m_PreviewSize;

            bool open = PreviewIsOpen();
            bool wide = PreviewIsWide();
            m_ShowOverlapPreview.target = open && !wide;
            m_ShowWidePreview.target = open && wide;
        }

        bool PreviewIsOpen()
        {
            return m_PreviewSize >= 32 + kPreviewMargin;
        }

        bool PreviewIsWide()
        {
            return position.width - m_PreviewSize - kPreviewMargin > Mathf.Min(m_PreviewSize * 2 - 20, 256);
        }

        // send an event to the delegate view (the view that called us)
        void SendEvent(string eventName, bool exitGUI)
        {
            if (m_DelegateView)
            {
                Event e = EditorGUIUtility.CommandEvent(eventName);

                try
                {
                    m_DelegateView.SendEvent(e);
                }
                finally
                {
                }
                if (exitGUI)
                    GUIUtility.ExitGUI();
            }
        }

        void HandleKeyboard()
        {
            // Handle events on the object selector window
            if (Event.current.type != EventType.KeyDown)
                return;

            switch (Event.current.keyCode)
            {
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    Close();
                    GUI.changed = true;
                    GUIUtility.ExitGUI();
                    break;
                default:
                    //Debug.Log ("Unhandled " + Event.current.keyCode);
                    return;
            }
            Event.current.Use();
            GUI.changed = true;
        }

        internal void Cancel()
        {
            // Undo changes we have done in the ObjectSelector
            Undo.RevertAllDownToGroup(m_ModalUndoGroup);

            // Clear selection so that object field doesn't grab it
            m_ListArea?.InitSelection(new int[0]);
            m_ObjectTreeWithSearch.Clear();
            m_LastSelectedInstanceId = 0;
            m_SelectionCancelled = true;
            m_EditedProperty = null;

            SendEvent(ObjectSelectorCanceledCommand, false);

            Close();
            GUI.changed = true;
            GUIUtility.ExitGUI();
        }

        [UsedImplicitly]
        void OnDestroy()
        {
            if (m_ListArea != null)
                m_ListArea.OnDestroy();

            m_ObjectTreeWithSearch.Clear();
        }

        [UsedImplicitly]
        void OnGUI()
        {
            HandleKeyboard();

            if (m_ObjectTreeWithSearch.IsInitialized())
                OnObjectTreeGUI();
            else
                OnObjectGridGUI();

            // Must be after gui so search field can use the Escape event if it has focus
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
                Cancel();
            else if (Event.current.commandName == EventCommandNames.UndoRedoPerformed && Selection.activeObject == null)
            {
                Close();
                GUI.changed = true;
                GUIUtility.ExitGUI();
            }
        }

        void OnObjectTreeGUI()
        {
            m_ObjectTreeWithSearch.OnGUI(new Rect(0, 0, position.width, position.height));
        }

        void OnObjectGridGUI()
        {
            InitIfNeeded();

            if (m_EditorCache == null)
                m_EditorCache = new EditorCache(EditorFeatures.PreviewGUI);

            // Handle window/preview stuff
            ResizeBottomPartOfWindow();

            Rect p = position;
            EditorPrefs.SetFloat("ObjectSelectorWidth", p.width);
            EditorPrefs.SetFloat("ObjectSelectorHeight", p.height);

            GUI.BeginGroup(new Rect(0, 0, position.width, position.height), GUIContent.none);

            // Let grid/list area take priority over search area on up/down arrow keys
            if (s_GridAreaPriorityKeyboardEvents.Contains(Event.current))
                m_ListArea.HandleKeyboard(false);

            SearchArea();

            // Let grid/list area handle any keyboard events not used by search area
            m_ListArea.HandleKeyboard(false);

            GridListArea();
            PreviewArea();

            GUI.EndGroup();

            // overlay preview resize widget
            GUI.Label(new Rect(position.width * .5f - 16, position.height - m_PreviewSize + 2, 32, Styles.bottomResize.fixedHeight), GUIContent.none, Styles.bottomResize);
        }

        void GridListArea()
        {
            int listKeyboardControlID = GUIUtility.GetControlID(FocusType.Keyboard);
            m_ListArea.OnGUI(listPosition, listKeyboardControlID);
        }

        void NotifySelectionChanged(bool exitGUI)
        {
            var currentObject = GetCurrentObject();
            Internal_NotifySelectionChanged(currentObject, exitGUI);
        }

        // Used by tests
        internal void Internal_NotifySelectionChanged(UnityObject selectedObject, bool exitGUI)
        {
            if (m_ObjectSelectorReceiver != null)
            {
                m_ObjectSelectorReceiver.OnSelectionChanged(selectedObject);
            }

            m_OnObjectSelectorUpdated?.Invoke(selectedObject);

            SendEvent(ObjectSelectorUpdatedCommand, exitGUI);
        }

        void NotifySelectorClosed(bool exitGUI)
        {
            var currentObject = GetCurrentObject();
            Internal_NotifySelectorClosed(currentObject, exitGUI);
        }

        // Used by tests
        internal void Internal_NotifySelectorClosed(UnityObject selectedObject, bool exitGUI)
        {
            if (m_ObjectSelectorReceiver != null)
            {
                m_ObjectSelectorReceiver.OnSelectionClosed(selectedObject);
                m_ObjectSelectorReceiver = null;
            }

            m_OnObjectSelectorClosed?.Invoke(selectedObject);
            m_OnObjectSelectorClosed = null;
            m_OnObjectSelectorUpdated = null;

            SendEvent(ObjectSelectorClosedCommand, exitGUI);
            Undo.CollapseUndoOperations(m_ModalUndoGroup);
            m_ModalUndoGroup = -1;
        }
    }
}
