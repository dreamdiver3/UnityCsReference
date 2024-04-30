// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UIElements;

namespace UnityEditor.UIElements
{
    class DefaultWindowBackend : IWindowBackend
    {
        protected Panel m_Panel;
        protected IWindowModel m_Model;

        protected IMGUIContainer imguiContainer;

        public object visualTree => m_Panel.visualTree;

        internal Panel panel => m_Panel;

        public virtual void OnCreate(IWindowModel model)
        {
            m_Model = model;
            m_Panel = EditorPanel.FindOrCreate(model as ScriptableObject);

            m_Panel.visualTree.SetSize(m_Model.size);
            m_Panel.IMGUIEventInterests = m_Model.eventInterests;

            imguiContainer = new IMGUIContainer(m_Model.onGUIHandler) { useOwnerObjectGUIState = true };
            imguiContainer.StretchToParentSize();
            imguiContainer.viewDataKey = "Dockarea";
            imguiContainer.name = VisualElementUtils.GetUniqueName("Dockarea");
            imguiContainer.tabIndex = -1;
            imguiContainer.focusOnlyIfHasFocusableControls = false;

            m_Panel.visualTree.Insert(0, imguiContainer);
            Assert.IsNull(m_Panel.rootIMGUIContainer);
            m_Panel.rootIMGUIContainer = imguiContainer;
        }

        public virtual void SizeChanged()
        {
            // The window backend isn't aware of the panel scaling, so the size only considers the native
            // pixels-per-point value. So for example, if a panel scaling of 2 is used, we must have twice
            // less points displayed, hence the division by 2.

            OnBackingScaleFactorChanged();
            m_Panel.visualTree.SetSize(m_Model.size / m_Panel.scale);
        }

        void IWindowBackend.EventInterestsChanged()
        {
            m_Panel.IMGUIEventInterests = m_Model.eventInterests;
        }

        void IWindowBackend.ResetPanelRenderingOnAssetChangeChanged()
        {
            m_Panel.resetPanelRenderingOnAssetChange = m_Model.resetPanelRenderingOnAssetChange;
        }

        public virtual void OnDestroy(IWindowModel model)
        {
            if (imguiContainer != null)
            {
                if (imguiContainer.HasMouseCapture())
                    imguiContainer.ReleaseMouse();
                imguiContainer.RemoveFromHierarchy();
                Assert.AreEqual(imguiContainer, m_Panel.rootIMGUIContainer);
                m_Panel.rootIMGUIContainer = null;
                imguiContainer = null;
            }

            // Here we assume m_Model == model. We should probably make the ignored OnDestroy argument obsolete.
            m_Model = null;
            m_Panel.Dispose();
        }

        public bool GetTooltip(Vector2 windowMouseCoordinates, out string tooltip, out Rect screenRectPosition)
        {
            tooltip = string.Empty;
            screenRectPosition = Rect.zero;

            VisualElement target = m_Panel.Pick(windowMouseCoordinates);
            if (target != null)
            {
                using (var tooltipEvent = TooltipEvent.GetPooled())
                {
                    tooltipEvent.elementTarget = target;
                    tooltipEvent.tooltip = null;
                    tooltipEvent.rect = Rect.zero;
                    target.SendEvent(tooltipEvent);

                    // StopPropagation will prevent further elements from modifying the tooltip but will not to prevent
                    // the tooltip from showing.
                    // To achieve that result, set tooltip = string.Empty and then StopImmediatePropagation().
                    if (!string.IsNullOrEmpty(tooltipEvent.tooltip))
                    {
                        tooltip = tooltipEvent.tooltip;
                        screenRectPosition = tooltipEvent.rect;
                        return true;
                    }
                }
            }
            return false;
        }

        public virtual void OnBackingScaleFactorChanged()
        {
            if (panel.UpdateScalingFromEditorWindow)
                panel.pixelsPerPoint = (panel as EditorPanel).GetBackingScaleFactor() ?? 1;
        }
    }
}
