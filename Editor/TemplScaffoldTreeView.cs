/*
 * Copyright (c) 2022 Willy Alberto Kuster
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace Willykc.Templ.Editor
{
    internal class TemplScaffoldTreeView : TreeView
    {
        private const string RootName = "Root";
        private const string GUIFieldName = "m_GUI";
        private const string UseHorizontalScrollFieldName = "m_UseHorizontalScroll";
        private const string GenericDragID = "GenericDragColumnDragging";
        private const string MultipleDragTitle = "< Multiple >";

        private readonly TemplSettings settings;
        private readonly Texture2D scaffoldIcon;
        private readonly Texture2D folderIcon;
        private readonly Texture2D fileIcon;
        private readonly List<TreeViewItem> rows = new List<TreeViewItem>(100);
        private readonly Dictionary<TemplScaffoldNode, int> nodeIDs =
            new Dictionary<TemplScaffoldNode, int>();

        internal event Action BeforeDrop;
        internal event Action AfterDrop;

        internal TemplScaffoldTreeView(TreeViewState treeViewState,
            TemplSettings settings,
            Texture2D scaffoldIcon,
            Texture2D folderIcon,
            Texture2D fileIcon)
            : base(treeViewState)
        {
            this.settings = settings
                ? settings
                : throw new ArgumentNullException(nameof(settings));
            this.scaffoldIcon = scaffoldIcon;
            this.folderIcon = folderIcon;
            this.fileIcon = fileIcon;
            settings.ScaffoldChange += OnScaffoldChange;
            settings.FullReset += Reload;
            showAlternatingRowBackgrounds = true;
            UseHorizontalScroll = true;
        }

        private bool UseHorizontalScroll
        {
            set
            {
                var guiFieldInfo = typeof(TreeView)
                    .GetField(GUIFieldName,
                    BindingFlags.Instance | BindingFlags.NonPublic);
                object gui = guiFieldInfo?.GetValue(this);
                var useHorizontalScrollFieldInfo = gui?.GetType()
                    .GetField(UseHorizontalScrollFieldName,
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (useHorizontalScrollFieldInfo == null)
                {
                    throw new InvalidOperationException("TreeView internals changed");
                }
                useHorizontalScrollFieldInfo.SetValue(gui, value);
            }
        }

        internal TemplScaffoldNode[] GetNodeSelection() => rows
            .Where(r => GetSelection()
            .Contains(r.id))
            .Cast<TemplScaffoldTreeViewItem>()
            .Select(r => r.Node)
            .ToArray();

        protected override void RowGUI(RowGUIArgs args)
        {
            base.RowGUI(args);
        }

        protected override IList<int> GetAncestors(int id)
        {
            var node = nodeIDs.First(kvp => kvp.Value == id).Key;
            var parentID = GetId(node);
            var parentIDs = new List<int>() { parentID };
            while (node.parent != null)
            {
                node = node.parent;
                parentID = GetId(node);
                parentIDs.Add(parentID);
            }
            return parentIDs;
        }

        protected override TreeViewItem BuildRoot() =>
            new TreeViewItem() { id = 0, depth = -1, displayName = RootName };

        protected override IList<TreeViewItem> BuildRows(TreeViewItem root)
        {
            rows.Clear();
            AddChildrenRecursive(settings.Scaffolds, 0, rows);
            SetupParentsAndChildrenFromDepths(root, rows);
            return rows;
        }

        protected override bool CanStartDrag(CanStartDragArgs args) => true;

        protected override void SetupDragAndDrop(SetupDragAndDropArgs args)
        {
            if (hasSearch)
            {
                return;
            }

            DragAndDrop.PrepareStartDrag();
            var draggedRows = GetRows()
                .Where(item => args.draggedItemIDs.Contains(item.id))
                .ToList();
            DragAndDrop.SetGenericData(GenericDragID, draggedRows);
            DragAndDrop.objectReferences = new UnityEngine.Object[] { };
            string title = draggedRows.Count == 1
                ? draggedRows[0].displayName
                : MultipleDragTitle;
            DragAndDrop.StartDrag(title);
        }

        protected override DragAndDropVisualMode HandleDragAndDrop(DragAndDropArgs args)
        {
            var genericData = DragAndDrop.GetGenericData(GenericDragID);
            if (!(genericData is List<TreeViewItem> draggedItems))
            {
                return DragAndDropVisualMode.None;
            }

            switch (args.dragAndDropPosition)
            {
                case DragAndDropPosition.UponItem:
                case DragAndDropPosition.BetweenItems:
                    {
                        var parentItem = args.parentItem as TemplScaffoldTreeViewItem;
                        var validDrag = IsValidDrag(parentItem, draggedItems);
                        var insertIndex = Mathf.Clamp(args.insertAtIndex, 0, args.insertAtIndex);
                        if (args.performDrop && validDrag)
                        {
                            OnDropItemsAtIndex(draggedItems, parentItem.Node, insertIndex);
                        }
                        return validDrag
                            ? DragAndDropVisualMode.Move
                            : DragAndDropVisualMode.None;
                    }
                default:
                    return DragAndDropVisualMode.None;
            }
        }

        private void OnDropItemsAtIndex(
            List<TreeViewItem> draggedItems,
            TemplScaffoldNode parent,
            int insertIndex)
        {
            BeforeDrop?.Invoke();

            var draggedNodes = draggedItems
                .Cast<TemplScaffoldTreeViewItem>()
                .Select(i => i.Node)
                .ToArray();

            settings.MoveScaffoldNodes(parent, insertIndex, draggedNodes);

            AfterDrop?.Invoke();
        }


        private bool IsValidDrag(TemplScaffoldTreeViewItem parent, List<TreeViewItem> draggedItems)
        {
            var scaffoldIsDragged = draggedItems
                .Cast<TemplScaffoldTreeViewItem>()
                .Select(i => i.Node)
                .Any(n => n is TemplScaffold);
            if (parent == null || parent.Node is TemplScaffoldFile || scaffoldIsDragged)
            {
                return false;
            }
            TreeViewItem currentParent = parent;
            while (currentParent != null)
            {
                if (draggedItems.Contains(currentParent))
                {
                    return false;
                }
                currentParent = currentParent.parent;
            }
            return true;
        }

        private void OnScaffoldChange(IReadOnlyList<TemplScaffoldNode> nodes)
        {
            var selectedIDs = nodes.Select(n => GetId(n)).ToArray();
            Reload();
            SetSelection(selectedIDs, TreeViewSelectionOptions.RevealAndFrame);
        }

        private void AddChildrenRecursive(
            IReadOnlyList<TemplScaffoldNode> children,
            int depth,
            List<TreeViewItem> rows)
        {
            foreach (var child in children.OrderBy(n => n.GetType().Name))
            {
                var id = GetId(child);
                var icon = GetIcon(child);
                var item = new TemplScaffoldTreeViewItem(id, depth, child)
                {
                    icon = icon
                };
                rows.Add(item);

                if (child.children.Count > 0)
                {
                    if (IsExpanded(id))
                    {
                        AddChildrenRecursive(child.children, depth + 1, rows);
                    }
                    else
                    {
                        item.children = CreateChildListForCollapsedParent();
                    }
                }
            }
        }

        private int GetId(TemplScaffoldNode node)
        {
            if(!nodeIDs.TryGetValue(node, out var id))
            {
                var last = nodeIDs.LastOrDefault();
                id = last.Value + 1;
                nodeIDs.Add(node, id);
            }
            return id;
        }

        private Texture2D GetIcon(TemplScaffoldNode node)
        {
            if (node is TemplScaffold)
            {
                return scaffoldIcon;
            }
            if (node is TemplScaffoldDirectory)
            {
                return folderIcon;
            }
            if(node is TemplScaffoldFile)
            {
                return fileIcon;
            }
            return null;
        }
    }
}
