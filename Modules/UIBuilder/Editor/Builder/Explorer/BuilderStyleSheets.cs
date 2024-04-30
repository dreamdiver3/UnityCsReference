// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using System;
using UnityEngine.UIElements;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;

namespace Unity.UI.Builder
{
    internal class BuilderStyleSheets : BuilderExplorer
    {
        static readonly string kToolbarPath = BuilderConstants.UIBuilderPackagePath + "/Explorer/BuilderStyleSheetsNewSelectorControls.uxml";
        static readonly string kHelpTooltipPath = BuilderConstants.UIBuilderPackagePath + "/Explorer/BuilderStyleSheetsNewSelectorHelpTips.uxml";
        private static readonly string kMessageLinkClassName = "unity-builder-message-link";

        ToolbarMenu m_AddUSSMenu;
        BuilderNewSelectorField m_NewSelectorField;
        TextField m_NewSelectorTextField;
        VisualElement m_NewSelectorTextInputField;
        ToolbarMenu m_PseudoStatesMenu;
        BuilderTooltipPreview m_TooltipPreview;
        Label m_MessageLink;
        BuilderStyleSheetsDragger m_StyleSheetsDragger;
        Label m_EmptyStyleSheetsPaneLabel;

        enum FieldFocusStep
        {
            Idle,
            FocusedFromStandby,
            NeedsSelectionOverride
        }

        FieldFocusStep m_FieldFocusStep;
        bool m_ShouldRefocusSelectorFieldOnBlur;

        BuilderDocument document => m_PaneWindow?.document;
        public BuilderNewSelectorField newSelectorField => m_NewSelectorField;

        public BuilderStyleSheets(
            BuilderPaneWindow paneWindow,
            BuilderViewport viewport,
            BuilderSelection selection,
            BuilderClassDragger classDragger,
            BuilderStyleSheetsDragger styleSheetsDragger,
            HighlightOverlayPainter highlightOverlayPainter,
            BuilderTooltipPreview tooltipPreview)
            : base(
                paneWindow,
                viewport,
                selection,
                classDragger,
                styleSheetsDragger,
                new BuilderStyleSheetsContextMenu(paneWindow, selection),
                viewport.styleSelectorElementContainer,
                false,
                highlightOverlayPainter,
                kToolbarPath,
                "StyleSheets")
        {
            m_TooltipPreview = tooltipPreview;
            if (m_TooltipPreview != null)
            {
                var helpTooltipTemplate = BuilderPackageUtilities.LoadAssetAtPath<VisualTreeAsset>(kHelpTooltipPath);
                var helpTooltipContainer = helpTooltipTemplate.CloneTree();
                m_TooltipPreview.Add(helpTooltipContainer); // We are the only ones using it so just add the contents and be done.
                m_MessageLink = m_TooltipPreview.Q<Label>(null, kMessageLinkClassName);
                m_MessageLink.focusable = true;
            }

            viewDataKey = "builder-style-sheets";
            AddToClassList(BuilderConstants.ExplorerStyleSheetsPaneClassName);

            var parent = this.Q("new-selector-item");

            // Init text field.
            m_NewSelectorField = parent.Q<BuilderNewSelectorField>("new-selector-field");
            m_NewSelectorTextField = m_NewSelectorField.textField;
            m_NewSelectorTextField.SetValueWithoutNotify(BuilderConstants.ExplorerInExplorerNewClassSelectorInfoMessage);
            m_NewSelectorTextInputField = m_NewSelectorTextField.Q("unity-text-input");
            m_NewSelectorTextInputField.RegisterCallback<KeyDownEvent>(OnEnter, TrickleDown.TrickleDown);
            UpdateNewSelectorFieldEnabledStateFromDocument();

            m_NewSelectorTextInputField.RegisterCallback<FocusEvent>((evt) =>
            {
                var input = evt.elementTarget;
                var field = GetTextFieldParent(input);
                m_FieldFocusStep = FieldFocusStep.FocusedFromStandby;
                if (field.text == BuilderConstants.ExplorerInExplorerNewClassSelectorInfoMessage || m_ShouldRefocusSelectorFieldOnBlur)
                {
                    m_ShouldRefocusSelectorFieldOnBlur = false;
                    field.value = BuilderConstants.UssSelectorClassNameSymbol;
                    field.textSelection.selectAllOnMouseUp = false;
                }

                ShowTooltip();
            }, TrickleDown.TrickleDown);

            m_NewSelectorTextField.RegisterCallback<ChangeEvent<string>>((evt) =>
            {
                if (m_FieldFocusStep != FieldFocusStep.FocusedFromStandby)
                    return;

                m_FieldFocusStep = m_NewSelectorTextField.value == BuilderConstants.UssSelectorClassNameSymbol ? FieldFocusStep.NeedsSelectionOverride : FieldFocusStep.Idle;

                // We don't want the '.' we just inserted in the FocusEvent to be highlighted,
                // which is the default behavior. Same goes for when we add pseudo states with options menu.
                m_NewSelectorTextField.textSelection.SelectRange(m_NewSelectorTextField.value.Length, m_NewSelectorTextField.value.Length);
            });

            // Since MouseDown captures the mouse, we need to RegisterCallback directly on the target in order to intercept the event.
            // This could be replaced by setting selectAllOnMouseUp to false.
            m_NewSelectorTextInputField.Q<TextElement>().RegisterCallback<MouseUpEvent>((evt) =>
            {
                // We want to prevent the default action on mouse up in KeyboardTextEditor, but only when
                // the field selection behaviour was changed by us.
                if (m_FieldFocusStep != FieldFocusStep.NeedsSelectionOverride)
                    return;

                m_FieldFocusStep = FieldFocusStep.Idle;

                // Reselect on the next execution, after the KeyboardTextEditor selects all.
                m_NewSelectorTextInputField.schedule.Execute(() =>
                {
                    m_NewSelectorTextField.textSelection.SelectRange(m_NewSelectorTextField.value.Length, m_NewSelectorTextField.value.Length);
                });

            }, TrickleDown.TrickleDown);

            m_NewSelectorTextInputField.RegisterCallback<BlurEvent>((evt) =>
            {
                var input = evt.elementTarget;
                var field = GetTextFieldParent(input);
                // Delay tooltip to allow users to click on link in preview if needed
                schedule.Execute(() =>
                {
                    field.textSelection.selectAllOnMouseUp = true;
                    if (m_NewSelectorTextInputField.focusController.focusedElement == m_MessageLink)
                        return;

                    HideTooltip();

                    if (m_ShouldRefocusSelectorFieldOnBlur)
                    {
                        PostEnterRefocus();
                    }
                    else if (string.IsNullOrEmpty(field.text) || field.text == BuilderConstants.UssSelectorClassNameSymbol)
                    {
                        field.SetValueWithoutNotify(BuilderConstants.ExplorerInExplorerNewClassSelectorInfoMessage);
                        m_PseudoStatesMenu.SetEnabled(false);
                    }

                });
            }, TrickleDown.TrickleDown);

            m_MessageLink?.RegisterCallback<BlurEvent>(evt =>
            {
                HideTooltip();
                if (m_ShouldRefocusSelectorFieldOnBlur)
                {
                    m_NewSelectorTextInputField.schedule.Execute(PostEnterRefocus);
                }

            });
            m_MessageLink?.RegisterCallback<ClickEvent>(evt =>
            {
                HideTooltip();
                if (m_ShouldRefocusSelectorFieldOnBlur)
                {
                    m_NewSelectorTextInputField.schedule.Execute(PostEnterRefocus);
                }
            });

            // Setup New USS Menu.
            m_AddUSSMenu = parent.Q<ToolbarMenu>("add-uss-menu");
            SetUpAddUSSMenu();

            // Setup pseudo states menu.
            m_PseudoStatesMenu = m_NewSelectorField.pseudoStatesMenu;

            // Update sub title.
            UpdateSubtitleFromActiveUSS();

            // Init drag stylesheet root
            classDragger.builderStylesheetRoot = container;
            styleSheetsDragger.builderStylesheetRoot = container;
            m_StyleSheetsDragger = styleSheetsDragger;

            RegisterCallback<GeometryChangedEvent>(e => AdjustPosition());

            // Create the empty state label here because this file shares a UXML with BuilderHierarchy
            m_EmptyStyleSheetsPaneLabel = new Label("Click the + icon to create a new StyleSheet.");
            m_EmptyStyleSheetsPaneLabel.AddToClassList(BuilderConstants.ExplorerDayZeroStateLabelClassName);
            m_EmptyStyleSheetsPaneLabel.style.display = DisplayStyle.None;
        }

        protected override void InitEllipsisMenu()
        {
            base.InitEllipsisMenu();

            if (pane == null)
            {
                return;
            }

            pane.AppendActionToEllipsisMenu(L10n.Tr("Full selector text"),
                a => ChangeVisibilityState(BuilderElementInfoVisibilityState.FullSelectorText),
            a => m_ElementHierarchyView.elementInfoVisibilityState
                .HasFlag(BuilderElementInfoVisibilityState.FullSelectorText)
                ? DropdownMenuAction.Status.Checked
                : DropdownMenuAction.Status.Normal);
        }

        TextField GetTextFieldParent(VisualElement ve)
        {
            return ve.GetFirstAncestorOfType<TextField>();
        }

        protected override bool IsSelectedItemValid(VisualElement element)
        {
            var isCS = element.GetStyleComplexSelector() != null;
            var isSS = element.GetStyleSheet() != null;

            return isCS || isSS;
        }

        void PostEnterRefocus()
        {
            m_NewSelectorTextInputField.Focus();
        }

        void OnEnter(KeyDownEvent evt)
        {
            if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter)
                return;

            CreateNewSelector(document.activeStyleSheet);

            evt.StopImmediatePropagation();
        }

        void CreateNewSelector(StyleSheet styleSheet)
        {
            var newValue = m_NewSelectorTextField.text;
            if (string.IsNullOrEmpty(newValue) || newValue == BuilderConstants.ExplorerInExplorerNewClassSelectorInfoMessage)
                return;

            if (styleSheet == null)
            {
                if (BuilderStyleSheetsUtilities.CreateNewUSSAsset(m_PaneWindow))
                {
                    styleSheet = m_PaneWindow.document.firstStyleSheet;

                    // The EditorWindow will no longer have Focus after we show the
                    // Save Dialog so even though the New Selector field will appear
                    // focused, typing won't do anything. As such, it's better, in
                    // this one case to remove focus from this field so users know
                    // to re-focus it themselves before they can add more selectors.
                    m_NewSelectorTextField.value = string.Empty;
                    m_NewSelectorTextField.Blur();
                }
                else
                {
                    return;
                }
            }
            else
            {
                m_ShouldRefocusSelectorFieldOnBlur = true;
            }

            var newSelectorStr = newValue.Trim();
            var selectorTypeSymbol = (newSelectorStr[0]) switch
            {
                '.' => BuilderConstants.UssSelectorClassNameSymbol,
                '#' => BuilderConstants.UssSelectorNameSymbol,
                ':' => BuilderConstants.UssSelectorPseudoStateSymbol,
                _ => ""
            };
            if (!string.IsNullOrEmpty(selectorTypeSymbol))
            {
                newSelectorStr = selectorTypeSymbol + newSelectorStr.Trim(selectorTypeSymbol[0]).Trim();
            }

            if (string.IsNullOrEmpty(newSelectorStr))
                return;

            if (newSelectorStr.Length == 1 && (
                newSelectorStr.StartsWith(BuilderConstants.UssSelectorClassNameSymbol)
                || newSelectorStr.StartsWith("-")
                || newSelectorStr.StartsWith("_")))
                return;

            if (!BuilderNameUtilities.styleSelectorRegex.IsMatch(newSelectorStr))
            {
                Builder.ShowWarning(BuilderConstants.StyleSelectorValidationSpacialCharacters);
                m_NewSelectorTextField.schedule.Execute(() =>
                {
                    m_NewSelectorTextField.SetValueWithoutNotify(newValue);
                    m_NewSelectorTextField.textSelection.SelectAll();
                });
                return;
            }

            var selectorContainerElement = m_Viewport.styleSelectorElementContainer;
            var newComplexSelector = BuilderSharedStyles.CreateNewSelector(selectorContainerElement, styleSheet, newSelectorStr);

            m_Selection.NotifyOfHierarchyChange();
            m_Selection.NotifyOfStylingChange();

            // Try to selected newly created selector.
            var newSelectorElement =
                m_Viewport.styleSelectorElementContainer.FindElement(
                    (e) => e.GetStyleComplexSelector() == newComplexSelector);
            if (newSelectorElement != null)
                m_Selection.Select(null, newSelectorElement);

            schedule.Execute(() =>
            {
                m_NewSelectorTextField.Blur();
                m_NewSelectorTextField.SetValueWithoutNotify(BuilderConstants.ExplorerInExplorerNewClassSelectorInfoMessage);
            });
        }

        void SetUpAddUSSMenu()
        {
            if (m_AddUSSMenu == null)
                return;

            m_AddUSSMenu.menu.MenuItems().Clear();

            {
                m_AddUSSMenu.menu.AppendAction(
                    BuilderConstants.ExplorerStyleSheetsPaneCreateNewUSSMenu,
                    action =>
                    {
                        BuilderStyleSheetsUtilities.CreateNewUSSAsset(m_PaneWindow);
                    });
                m_AddUSSMenu.menu.AppendAction(
                    BuilderConstants.ExplorerStyleSheetsPaneAddExistingUSSMenu,
                    action =>
                    {
                        BuilderStyleSheetsUtilities.AddExistingUSSToAsset(m_PaneWindow);
                    });
            }
        }

        void ShowTooltip()
        {
            if (m_TooltipPreview == null)
                return;

            if (m_TooltipPreview.isShowing)
                return;

            m_TooltipPreview.Show();

            AdjustPosition();
        }

        void AdjustPosition()
        {
            m_TooltipPreview.style.left = Mathf.Max(0, this.pane.resolvedStyle.width + BuilderConstants.TooltipPreviewYOffset);
            m_TooltipPreview.style.top = m_Viewport.viewportWrapper.worldBound.y;
        }

        void HideTooltip()
        {
            if (m_TooltipPreview == null)
                return;

            m_TooltipPreview.Hide();
        }

        void UpdateNewSelectorFieldEnabledStateFromDocument()
        {
            m_NewSelectorTextField.SetEnabled(true);
            SetUpAddUSSMenu();
        }

        void UpdateSubtitleFromActiveUSS()
        {
            if (pane == null)
                return;

            if (document == null || document.activeStyleSheet == null)
            {
                pane.subTitle = string.Empty;
                return;
            }

            pane.subTitle = document.activeStyleSheet.name + BuilderConstants.UssExtension;
        }

        protected override void ElementSelectionChanged(List<VisualElement> elements)
        {
            base.ElementSelectionChanged(elements);

            UpdateSubtitleFromActiveUSS();
        }

        public override void HierarchyChanged(VisualElement element, BuilderHierarchyChangeType changeType)
        {
            base.HierarchyChanged(element, changeType);
            m_ElementHierarchyView.hasUssChanges = true;
            UpdateNewSelectorFieldEnabledStateFromDocument();
            UpdateSubtitleFromActiveUSS();

            // Show empty state if no stylesheet loaded
            if (document.activeStyleSheet == null)
            {
                m_ElementHierarchyView.container.style.justifyContent = Justify.Center;
                m_ElementHierarchyView.treeView.style.flexGrow = document.activeOpenUXMLFile.isChildSubDocument ? 1 : 0;
                m_EmptyStyleSheetsPaneLabel.style.display = DisplayStyle.Flex;
                m_ElementHierarchyView.container.Add(m_EmptyStyleSheetsPaneLabel);
                m_EmptyStyleSheetsPaneLabel.SendToBack();
            }
            else
            {
                if (m_EmptyStyleSheetsPaneLabel.parent != m_ElementHierarchyView.container)
                    return;

                // Revert inline style changes to default
                m_ElementHierarchyView.container.style.justifyContent = Justify.FlexStart;
                elementHierarchyView.treeView.style.flexGrow = 1;
                m_EmptyStyleSheetsPaneLabel.style.display = DisplayStyle.None;
                m_EmptyStyleSheetsPaneLabel.RemoveFromHierarchy();
            }
        }

        // Used by unit tests to reset state after stylesheets drag
        internal void ResetStyleSheetsDragger()
        {
            m_StyleSheetsDragger.Reset();
        }
    }
}
