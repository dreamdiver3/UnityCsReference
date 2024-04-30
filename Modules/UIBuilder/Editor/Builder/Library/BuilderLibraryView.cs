// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using TreeViewItem = UnityEngine.UIElements.TreeViewItemData<Unity.UI.Builder.BuilderLibraryTreeItem>;

namespace Unity.UI.Builder
{
    abstract class BuilderLibraryView : VisualElement
    {
        VisualElement m_DocumentRootElement;
        BuilderSelection m_Selection;
        BuilderTooltipPreview m_TooltipPreview;
        BuilderPaneContent m_BuilderPaneContent;

        protected BuilderPaneWindow m_PaneWindow;
        protected BuilderLibraryDragger m_Dragger;

        protected IList<TreeViewItem> m_Items;
        protected IList<TreeViewItem> m_VisibleItems;

        internal IList<TreeViewItem> visibleItems => m_VisibleItems;

        public abstract VisualElement primaryFocusable { get; }

        public virtual void SetupView(BuilderLibraryDragger dragger, BuilderTooltipPreview tooltipPreview,
            BuilderPaneContent builderPaneContent, BuilderPaneWindow builderPaneWindow,
            VisualElement documentElement, BuilderSelection selection)
        {
            m_Dragger = dragger;
            m_TooltipPreview = tooltipPreview;
            m_BuilderPaneContent = builderPaneContent;
            m_PaneWindow = builderPaneWindow;
            m_DocumentRootElement = documentElement;
            m_Selection = selection;
        }

        public abstract void Refresh();
        public abstract void FilterView(string value);

        protected void RegisterControlContainer(VisualElement element)
        {
            m_Dragger?.RegisterCallbacksOnTarget(element);

            if (m_TooltipPreview != null)
            {
                element.RegisterCallback<MouseEnterEvent>(OnItemMouseEnter);
                element.RegisterCallback<MouseLeaveEvent>(OnItemMouseLeave);
            }
        }

        protected void LinkToTreeViewItem(VisualElement element, BuilderLibraryTreeItem libraryTreeItem)
        {
            element.userData = libraryTreeItem;
            element.SetProperty(BuilderConstants.LibraryItemLinkedManipulatorVEPropertyName, libraryTreeItem);
        }

        protected BuilderLibraryTreeItem GetLibraryTreeItem(VisualElement element)
        {
            return (BuilderLibraryTreeItem)element.GetProperty(BuilderConstants.LibraryItemLinkedManipulatorVEPropertyName);
        }

        internal void AddItemToTheDocument(BuilderLibraryTreeItem item)
        {
            // If this is the uxml file entry of the currently open file, don't allow
            // the user to instantiate it (infinite recursion) or re-open it.
            var listOfOpenDocuments = m_PaneWindow.document.openUXMLFiles;
            bool isCurrentDocumentOpen = listOfOpenDocuments.Any(doc => doc.uxmlFileName == item.name);

            if (isCurrentDocumentOpen)
                return;

            if (m_PaneWindow.document.WillCauseCircularDependency(item.sourceAsset))
            {
                BuilderDialogsUtility.DisplayDialog(BuilderConstants.InvalidWouldCauseCircularDependencyMessage,
                    BuilderConstants.InvalidWouldCauseCircularDependencyMessageDescription, BuilderConstants.DialogOkOption);
                return;
            }

            var newElement = item.makeVisualElementCallback?.Invoke();
            if (newElement == null)
                return;

            if (item.makeElementAssetCallback != null && newElement is TemplateContainer tempContainer)
            {
                if (!BuilderAssetUtilities.ValidateAsset(item.sourceAsset, item.sourceAssetPath))
                    return;
            }

            var activeVTARootElement = m_DocumentRootElement.Query().Where(e => e.GetVisualTreeAsset() == m_PaneWindow.document.visualTreeAsset).First();
            if (activeVTARootElement == null)
            {
                Debug.LogError("UI Builder has a bug. Could not find document root element for currently active open UXML document.");
                return;
            }
            activeVTARootElement.Add(newElement);

            if (item.makeElementAssetCallback == null)
                BuilderAssetUtilities.AddElementToAsset(m_PaneWindow.document, newElement);
            else
                BuilderAssetUtilities.AddElementToAsset(
                    m_PaneWindow.document, newElement, item.makeElementAssetCallback);

            m_Selection.NotifyOfHierarchyChange();
            m_Selection.Select(null, newElement);
        }

        void OnItemMouseEnter(MouseEnterEvent evt)
        {
            var box = evt.elementTarget;
            var libraryTreeItem = box.GetProperty(BuilderConstants.LibraryItemLinkedManipulatorVEPropertyName) as BuilderLibraryTreeItem;

            if (!libraryTreeItem.hasPreview)
                return;

            var sample = libraryTreeItem.makeVisualElementCallback?.Invoke();
            if (sample == null)
                return;

            m_TooltipPreview.Add(sample);
            m_TooltipPreview.Show();

            m_TooltipPreview.style.left = m_BuilderPaneContent.pane.resolvedStyle.width + BuilderConstants.TooltipPreviewYOffset;
            m_TooltipPreview.style.top = m_BuilderPaneContent.pane.resolvedStyle.top;
        }

        void OnItemMouseLeave(MouseLeaveEvent evt)
        {
            HidePreview();
        }

        protected void HidePreview()
        {
            m_TooltipPreview.Clear();
            m_TooltipPreview.Hide();
        }

        protected static IList<TreeViewItem> FilterTreeViewItems(IEnumerable<TreeViewItem> items, string searchText)
        {
            var filteredItems = new List<TreeViewItem>();

            foreach (var item in items)
            {
                if (item.data.name.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                {
                    filteredItems.Add(item);
                }
                else if (item.children != null && item.children.GetCount() > 0)
                {
                    // Recursively filter children
                    var filteredChildren = FilterTreeViewItems(item.children, searchText);
                    if (filteredChildren.Count > 0)
                    {
                        // If any children match, add a copy of the parent item with filtered children
                        var itemCopy = new TreeViewItem(item.id, item.data);
                        itemCopy.AddChildren(filteredChildren);
                        filteredItems.Add(itemCopy);
                    }
                }
            }

            return filteredItems;
        }
    }
}
