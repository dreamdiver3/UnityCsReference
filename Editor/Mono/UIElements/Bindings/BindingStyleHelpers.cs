// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using System;
using System.Collections.Generic;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Bindings;
using UnityEngine.UIElements;

namespace UnityEditor.UIElements
{
    [VisibleToOtherModules("UnityEditor.UIBuilderModule")]
    internal static class BindingsStyleHelpers
    {
        internal static event Action<VisualElement, SerializedProperty> updateBindingStateStyle;

        static EventCallback<PointerUpEvent> s_RightClickMenuCallback;
        static Action<VisualElement, SerializedProperty> s_UpdateElementStyleFromProperty;
        static Action<VisualElement, SerializedProperty> s_UpdatePrefabStateStyleFromProperty;

        // Lets us bypass the default right click menu to show our own.
        [VisibleToOtherModules("UnityEditor.UIBuilderModule")]
        internal static Func<VisualElement, bool> HandleRightClickMenu;

        static BindingsStyleHelpers()
        {
            s_RightClickMenuCallback = RightClickFieldMenuEvent;
            s_UpdateElementStyleFromProperty = UpdateElementStyleFromProperty;
            s_UpdatePrefabStateStyleFromProperty = UpdatePrefabStateStyleFromProperty;
        }

        private enum BarType
        {
            PrefabOverride,
            LiveProperty
        }

        private static void UpdateElementRecursively(VisualElement element, SerializedProperty prop, Action<VisualElement, SerializedProperty> updateCallback)
        {
            VisualElement elementToUpdate = element;

            if (element is Foldout foldout)
            {
                // We only want to apply override styles onto the Foldout header, not the entire contents.
                elementToUpdate = foldout.toggle;
            }
            else if (element.ClassListContains(BaseCompositeField<int, IntegerField, int>.ussClassName)
                     || element is BoundsField || element is BoundsIntField)
            {
                // The problem with compound fields is that they are bound at the parent level using
                // their parent value data type. For example, a Vector3Field is bound to the parent
                // SerializedProperty which uses the Vector3 data type. However, animation overrides
                // are not stored on the parent SerializedProperty but on the component child
                // SerializedProperties. So even though we're bound to the parent property, we still
                // have to dive inside and example the child SerializedProperties (ie. x, y, z, height)
                // and override the animation styles individually.

                var compositeField = element;

                // The element we style in the main pass is going to be just the label.
                if (element is IPrefixLabel prefixLabel)
                    elementToUpdate = prefixLabel.labelElement;
                else
                    elementToUpdate = element.Q(className: BaseField<int>.labelUssClassName);

                // Go through the inputs and find any that match the names of the child PropertyFields.
                var propCopy = prop.Copy();
                var endProperty = propCopy.GetEndProperty();
                propCopy.NextVisible(true);     // Expand the first child.
                do
                {
                    if (SerializedProperty.EqualContents(propCopy, endProperty))
                        break;

                    var subInputName = "unity-" + propCopy.name + "-input";
                    var subInput = compositeField.Q(subInputName);
                    if (subInput == null)
                        continue;
                    UpdateElementRecursively(subInput, propCopy, updateCallback);
                }
                while (propCopy.NextVisible(false));     // Never expand children.
            }

            if (elementToUpdate != null)
            {
                updateCallback(elementToUpdate, prop);
            }
        }

        internal static void UpdateElementStyle(VisualElement element, SerializedProperty prop)
        {
            if (element == null)
                return;

            UpdateElementRecursively(element, prop, s_UpdateElementStyleFromProperty);
        }

        private static void UpdateElementStyleFromProperty(VisualElement element, SerializedProperty prop)
        {
            if (element is IMixedValueSupport mixedValuePropertyField)
                mixedValuePropertyField.showMixedValue = prop.hasMultipleDifferentValues;

            // It's possible for there to be no label in a compound field, for example. So, nothing to style.
            if (element == null)
                return;

            // Handle prefab state.
            UpdatePrefabStateStyleFromProperty(element, prop);

            // Handle live property state.
            UpdateLivePropertyStyleFromProperty(element, prop);

            // Handle dynamic states
            updateBindingStateStyle?.Invoke(element, prop);

            // Handle animated state.

            // Since we handle compound fields above, the element here will always be a single field
            // (or not a field at all). This means we can perform a faster query and search for
            // a single element.
            var inputElement = element.Q(className: BaseField<int>.inputUssClassName);
            if (inputElement == null)
            {
                return;
            }

            bool animated = AnimationMode.IsPropertyAnimated(prop.serializedObject.targetObject, prop.propertyPath);
            bool candidate = AnimationMode.IsPropertyCandidate(prop.serializedObject.targetObject, prop.propertyPath);
            bool recording = AnimationMode.InAnimationRecording();

            inputElement.EnableInClassList(BindingExtensions.animationRecordedUssClassName, animated && recording);
            inputElement.EnableInClassList(BindingExtensions.animationCandidateUssClassName, animated && !recording && candidate);
            inputElement.EnableInClassList(BindingExtensions.animationAnimatedUssClassName, animated && !recording && !candidate);
        }

        internal static void UpdateLivePropertyStyleFromProperty(VisualElement element, SerializedProperty prop)
        {
            bool handleLivePropertyState = false;

            if (EditorApplication.isPlaying && SerializedObject.GetLivePropertyFeatureGlobalState())
            {
                try
                {
                    var component = prop.serializedObject.targetObject as Component;
                    handleLivePropertyState = (component != null && !component.gameObject.scene.isSubScene) || prop.isLiveModified;
                }
                catch (Exception)
                {
                    return;
                }
            }

            if (handleLivePropertyState)
            {
                if (!element.ClassListContains(BindingExtensions.livePropertyUssClassName))
                {
                    var container = FindPrefabOverrideOrLivePropertyBarCompatibleParent(element);
                    var barContainer = container?.livePropertyYellowBarsContainer;

                    element.AddToClassList(BindingExtensions.livePropertyUssClassName);

                    if (container != null && barContainer != null)
                    {
                        var livePropertyBar = new VisualElement();
                        livePropertyBar.name = BindingExtensions.livePropertyBarName;
                        livePropertyBar.userData = element;
                        livePropertyBar.AddToClassList(BindingExtensions.livePropertyBarUssClassName);
                        barContainer.Add(livePropertyBar);

                        element.SetProperty(BindingExtensions.livePropertyBarName, livePropertyBar);

                        // We need to try and set the bar style right away, even if the container
                        // didn't compute its layout yet. This is for when the override is done after
                        // everything has been layed out.
                        UpdatePrefabOverrideOrLivePropertyBarStyle(livePropertyBar);

                        // We intentionally re-register this event on the container per element and
                        // never unregister.
                        container.RegisterCallback<GeometryChangedEvent, BarType>(UpdatePrefabOverrideOrLivePropertyBarStyleEvent, BarType.LiveProperty);
                        element.RegisterCallback<DetachFromPanelEvent>(_ =>
                        {
                            element.RemoveFromClassList(BindingExtensions.livePropertyUssClassName);
                            livePropertyBar.RemoveFromHierarchy();
                        });
                    }
                }
            }
            else if (element.ClassListContains(BindingExtensions.livePropertyUssClassName))
            {
                element.RemoveFromClassList(BindingExtensions.livePropertyUssClassName);

                var container = FindPrefabOverrideOrLivePropertyBarCompatibleParent(element);
                var barContainer = container?.livePropertyYellowBarsContainer;

                if (container != null && barContainer != null)
                {
                    var livePropertyBar = element.GetProperty(BindingExtensions.livePropertyBarName) as VisualElement;
                    if (livePropertyBar != null)
                        livePropertyBar.RemoveFromHierarchy();
                }
            }
        }

        internal static void UpdatePrefabStateStyle(VisualElement element, SerializedProperty prop)
        {
            if (element == null)
                return;

            UpdateElementRecursively(element, prop, s_UpdatePrefabStateStyleFromProperty);
        }

        internal static void UpdateLivePropertyStateStyle(VisualElement element, SerializedProperty prop)
        {
            if (element == null)
                return;

            UpdateElementRecursively(element, prop, UpdateLivePropertyStyleFromProperty);
        }

        static bool ComponentIsPrefabOverride(Component comp)
        {
            return comp != null &&
                PrefabUtility.GetCorrespondingConnectedObjectFromSource(comp.gameObject) != null &&
                PrefabUtility.GetCorrespondingObjectFromSource(comp) == null;
        }

        private static void UpdatePrefabStateStyleFromProperty(VisualElement element, SerializedProperty prop)
        {
            bool handlePrefabState = false;

            try
            {
                // This can throw if the serialized object changes type under our feet
                handlePrefabState = prop.serializedObject.targetObjects.Length == 1 &&
                    prop.isInstantiatedPrefab &&
                    (prop.prefabOverride || ComponentIsPrefabOverride(prop.serializedObject.targetObject as Component));
            }
            catch
            {
                return;
            }

            // Handle prefab state.
            if (handlePrefabState)
            {
                if (!element.ClassListContains(BindingExtensions.prefabOverrideUssClassName))
                {
                    var container = FindPrefabOverrideOrLivePropertyBarCompatibleParent(element);
                    var barContainer = container?.prefabOverrideBlueBarsContainer;

                    element.AddToClassList(BindingExtensions.prefabOverrideUssClassName);

                    if (container != null && barContainer != null)
                    {
                        // Ideally, this blue bar would be a child of the field and just move
                        // outside the field in absolute offsets to hug the side of the field's
                        // container. However, right now we need to have overflow:hidden on
                        // fields because of case 1105567 (the inputs can grow beyond the field).
                        // Therefore, we have to add the blue bars as children of the container
                        // and move them down beside their respective field.

                        var prefabOverrideBar = new VisualElement();
                        prefabOverrideBar.name = BindingExtensions.prefabOverrideBarName;
                        prefabOverrideBar.userData = element;
                        string ussClass = PrefabUtility.CanPropertyBeAppliedToSource(prop) ? BindingExtensions.prefabOverrideBarUssClassName : BindingExtensions.prefabOverrideBarNotApplicableUssClassName;
                        prefabOverrideBar.AddToClassList(ussClass);
                        barContainer.Add(prefabOverrideBar);

                        element.SetProperty(BindingExtensions.prefabOverrideBarName, prefabOverrideBar);

                        // We need to try and set the bar style right away, even if the container
                        // didn't compute its layout yet. This is for when the override is done after
                        // everything has been layed out.
                        UpdatePrefabOverrideOrLivePropertyBarStyle(prefabOverrideBar);

                        // We intentionally re-register this event on the container per element and
                        // never unregister.
                        container.RegisterCallback<GeometryChangedEvent, BarType>(UpdatePrefabOverrideOrLivePropertyBarStyleEvent, BarType.PrefabOverride);
                        element.RegisterCallback<DetachFromPanelEvent>(_ =>
                        {
                            element.RemoveFromClassList(BindingExtensions.prefabOverrideUssClassName);
                            prefabOverrideBar.RemoveFromHierarchy();
                        });
                    }
                }
            }
            else if (element.ClassListContains(BindingExtensions.prefabOverrideUssClassName))
            {
                element.RemoveFromClassList(BindingExtensions.prefabOverrideUssClassName);

                var container = FindPrefabOverrideOrLivePropertyBarCompatibleParent(element);
                var barContainer = container?.prefabOverrideBlueBarsContainer;

                if (container != null && barContainer != null)
                {
                    var prefabOverrideBar = element.GetProperty(BindingExtensions.prefabOverrideBarName) as VisualElement;
                    if (prefabOverrideBar != null)
                        prefabOverrideBar.RemoveFromHierarchy();
                }
            }

            if (PrefabUtility.IsPropertyBeingDrivenByPrefabStage(prop))
            {
                element.SetEnabled(false);
                element.tooltip = PrefabStage.s_PrefabInContextPreviewValuesTooltip;
            }
        }

        private static InspectorElement FindPrefabOverrideOrLivePropertyBarCompatibleParent(VisualElement field)
        {
            // For now we only support these blue prefab override bars and yellow live property bars within an InspectorElement.
            return field.GetFirstAncestorOfType<InspectorElement>();
        }

        private static void UpdatePrefabOverrideOrLivePropertyBarStyle(VisualElement bar)
        {
            var element = bar.userData as VisualElement;

            var container = FindPrefabOverrideOrLivePropertyBarCompatibleParent(element);
            if (container == null)
                return;

            // Move the bar to where the control is in the container.
            var top = element.worldBound.y - container.worldBound.y;
            if (float.IsNaN(top))     // If this is run before the container has been layed out.
                return;

            var elementHeight = element.resolvedStyle.height;
            if (float.IsNaN(elementHeight))
            {
                // This event is added due to an issue where container is fully resolved, but this element is not.
                // This happens each time when entering play mode.
                element.RegisterCallback<GeometryChangedEvent, VisualElement>(ReUpdateLivePropertyBarStyleEvent, bar);
                return;
            }

            // This is needed so if you have 2 overridden fields their blue
            // bars touch (and it looks like one long bar). They normally wouldn't
            // because most fields have a small margin.
            var bottomOffset = element.resolvedStyle.marginBottom;
            var topOffset = element.resolvedStyle.marginTop;

            bar.style.top = top - topOffset;
            bar.style.height = elementHeight + bottomOffset + topOffset;
            bar.style.left = 0.0f;
        }

        private static void UpdatePrefabOverrideOrLivePropertyBarStyleEvent(GeometryChangedEvent evt, BarType barType)
        {
            var container = evt.target as InspectorElement;
            if (container == null)
                return;

            var barContainer = barType switch
            {
                BarType.PrefabOverride => container.Q(BindingExtensions.prefabOverrideBarContainerName),
                BarType.LiveProperty => container.Q(BindingExtensions.livePropertyBarContainerName),
                _ => throw new ArgumentOutOfRangeException(nameof(barType), barType, null)
            };

            if (barContainer == null)
                return;

            // If the userData parent has been removed then we should remove all bars as the component has been removed.
            for (var i = 0; i < barContainer.childCount; i++)
            {
                var element = barContainer[i].userData as VisualElement;
                if (element == null || FindPrefabOverrideOrLivePropertyBarCompatibleParent(element) != null) continue;

                switch (barType)
                {
                    case BarType.PrefabOverride:
                    {
                        element.RemoveFromClassList(BindingExtensions.prefabOverrideUssClassName);
                        var prefabOverridePropertyBar = element.GetProperty(BindingExtensions.prefabOverrideBarName) as VisualElement;
                        prefabOverridePropertyBar?.RemoveFromHierarchy();
                        break;
                    }
                    case BarType.LiveProperty:
                    {
                        element.RemoveFromClassList(BindingExtensions.livePropertyUssClassName);
                        var livePropertyBar = element.GetProperty(BindingExtensions.livePropertyBarName) as VisualElement;
                        livePropertyBar?.RemoveFromHierarchy();
                        break;
                    }
                    default:
                        throw new ArgumentOutOfRangeException(nameof(barType), barType, null);
                }
                return;
            }

            for (var i = 0; i < barContainer.childCount; i++)
                UpdatePrefabOverrideOrLivePropertyBarStyle(barContainer[i]);
        }

        private static void ReUpdateLivePropertyBarStyleEvent(GeometryChangedEvent evt, VisualElement bar)
        {
            var element = evt.elementTarget;
            if (element == null)
                return;

            element.UnregisterCallback<GeometryChangedEvent, VisualElement>(ReUpdateLivePropertyBarStyleEvent);

            UpdatePrefabOverrideOrLivePropertyBarStyle(bar);
        }

        // Stop ContextClickEvent because the context menu in the UITk inspector is shown on PointerUpEvent and not on ContextualMenuPopulateEvent (UUM-11643).
        static void StopContextClickEvent(ContextClickEvent e)
        {
            e.StopImmediatePropagation();
        }

        internal static void RegisterRightClickMenu(Label field, SerializedProperty property)
        {
            field.userData = property.Copy();
            field.RegisterCallback(s_RightClickMenuCallback, InvokePolicy.IncludeDisabled, TrickleDown.TrickleDown);
            field.RegisterCallback<ContextClickEvent>(StopContextClickEvent, TrickleDown.TrickleDown);
        }

        internal static void RegisterRightClickMenu<TValue>(BaseField<TValue> field, SerializedProperty property)
        {
            field.userData = property.Copy();
            field.RegisterCallback(s_RightClickMenuCallback, InvokePolicy.IncludeDisabled, TrickleDown.TrickleDown);
            field.RegisterCallback<ContextClickEvent>(StopContextClickEvent, TrickleDown.TrickleDown);
        }

        internal static void RegisterRightClickMenu(Foldout field, SerializedProperty property)
        {
            var toggle = field.Q<Toggle>(className: Foldout.toggleUssClassName);
            if (toggle != null)
            {
                toggle.userData = property.Copy();
                toggle.RegisterCallback(s_RightClickMenuCallback, InvokePolicy.IncludeDisabled, TrickleDown.TrickleDown);
                toggle.RegisterCallback<ContextClickEvent>(StopContextClickEvent, TrickleDown.TrickleDown);
            }
        }

        internal static void UnregisterRightClickMenu<TValue>(BaseField<TValue> field)
        {
           field.userData = null;
           field.UnregisterCallback(s_RightClickMenuCallback);
           field.UnregisterCallback<ContextClickEvent>(StopContextClickEvent);
        }

        internal static void UnregisterRightClickMenu(Foldout field)
        {
            var toggle = field.Q<Toggle>(className: Foldout.toggleUssClassName);
            toggle?.UnregisterCallback(s_RightClickMenuCallback);
            toggle?.UnregisterCallback<ContextClickEvent>(StopContextClickEvent);
        }

        internal static void RightClickFieldMenuEvent(PointerUpEvent evt)
        {
            if (evt.button != (int)MouseButton.RightMouse)
                return;

            var element = evt.currentTarget as VisualElement;

            bool handledExternally = HandleRightClickMenu?.Invoke(element) == true;

            var property = element?.userData as SerializedProperty;
            if (property == null || handledExternally)
                return;

            var wasEnabled = GUI.enabled;
            if (!element.enabledInHierarchy)
                GUI.enabled = false;

            try
            {
                Event.ignoreGuiDepth = true;
                Event.current = evt.imguiEvent;
                var menu = EditorGUI.FillPropertyContextMenu(property, null, null, element);
                GUI.enabled = wasEnabled;

                if (menu == null)
                    return;

                var dropdownMenu = ConvertGenericMenuToDropdownMenu(menu);
                element.panel.contextualMenuManager.DisplayMenu(evt, element, dropdownMenu);
                evt.StopPropagation();
            }
            finally
            {
                Event.ignoreGuiDepth = false;
            }
        }

        static DropdownMenu ConvertGenericMenuToDropdownMenu(GenericMenu menu)
        {
            var dropdownMenu = new DropdownMenu();

            foreach (var menuItem in menu.menuItems)
            {
                if (menuItem.separator)
                {
                    dropdownMenu.AppendSeparator(menuItem.content.text);
                }
                else
                {
                    var statusCallback = MenuItemToActionStatusCallback(menuItem);

                    if (menuItem.func != null)
                    {
                        dropdownMenu.AppendAction(menuItem.content.text, _ => menuItem.func(), statusCallback);
                    }
                    else if (menuItem.func2 != null)
                    {
                        dropdownMenu.AppendAction(menuItem.content.text, action => menuItem.func2(action.userData), statusCallback, menuItem.userData);
                    }
                    else
                    {
                        dropdownMenu.AppendAction(menuItem.content.text, null, statusCallback);
                    }
                }
            }

            return dropdownMenu;
        }

        static readonly Dictionary<DropdownMenuAction.Status, Func<DropdownMenuAction, DropdownMenuAction.Status>> s_StatusCallbacks = new()
        {
            { DropdownMenuAction.Status.Normal, DropdownMenuAction.AlwaysEnabled },
            { DropdownMenuAction.Status.Disabled, DropdownMenuAction.AlwaysDisabled },
        };

        static Func<DropdownMenuAction, DropdownMenuAction.Status> MenuItemToActionStatusCallback(GenericMenu.MenuItem menuItem)
        {
            var status = DropdownMenuAction.Status.None;

            if (menuItem.func != null || menuItem.func2 != null)
            {
                status |= DropdownMenuAction.Status.Normal;
            }
            else
            {
                status |= DropdownMenuAction.Status.Disabled;
            }

            if (menuItem.on)
                status |= DropdownMenuAction.Status.Checked;

            // Cached callbacks
            if (!s_StatusCallbacks.TryGetValue(status, out var callback))
            {
                callback = action => status;
                s_StatusCallbacks[status] = callback;
            }

            return callback;
        }
    }
}
