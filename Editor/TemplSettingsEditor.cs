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
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditorInternal;
using UnityEngine;

namespace Willykc.Templ.Editor
{
    using static TemplEditorInitialization;
    using static TemplProcessor;

    [CustomEditor(typeof(TemplSettings))]
    internal sealed class TemplSettingsEditor : UnityEditor.Editor
    {
        internal const string MenuName = "Window/Templ/Settings";
        private const int Padding = 5;
        private const int MaxFilenameLength = 64;
        private const int ValidInputFieldCount = 1;
        private const int Spacing = 2;
        private const int Double = 2;
        private const int MaxScaffoldsWidth = 1000;
        private const int NewButtonWidth = 40;
        private const int DirectoryButtonWidth = 30;
        private const int FileButtonWidth = 30;
        private const int RemoveButtonWidth = 30;
        private const float Half = .5f;
        private const string Header = "Templ Entries";
        private const string ForceRenderButtonText = "Force Render Templates";
        private const string LiveTitle = "Live";
        private const string ScaffoldsTitle = "Scaffolds";
        private const string NewButtonText = "New";
        private const string ScribanIconPath = "Packages/com.willykc.templ/Icons/sbn_logo.png";
        private const string FolderIconName = "Folder Icon";
        private const string DeleteIconName = "Toolbar Minus";
        private const string ScaffoldIconName = "d_VerticalLayoutGroup Icon";
        private const string ScaffoldTooltip = "Create New Scaffold";
        private const string FolderTooltip = "Add Directory";
        private const string FileTooltip = "Add Template";
        private const string DeleteTooltip = "Delete";
        private const string SessionStateKeyPrefix = "TemplScaffold";
        private static readonly string ErrorMessage = "Invalid entries detected. All fields must " +
            $"have values. {nameof(ScribanAsset)} or {nameof(TemplSettings)} can not be used as " +
            $"input. {Capitalize(nameof(TemplEntry.template))} must be valid. " +
            $"{Capitalize(nameof(TemplEntry.filename))} field must not contain invalid " +
            "characters and must be unique under the same " +
            $"{Capitalize(nameof(TemplEntry.directory))}. Templ will only render templates for " +
            "valid entries.";
        private static readonly Color InvalidColor = new Color(1, .3f, .3f, 1);
        private static readonly Color ValidColor = Color.white;
        private static readonly float Line = EditorGUIUtility.singleLineHeight;
        private static readonly float DoubleLine = EditorGUIUtility.singleLineHeight * Double;

        private static Texture2D FolderIcon;
        private static Texture2D FileIcon;
        private static Texture2D DeleteIcon;
        private static Texture2D ScaffoldIcon;

        private ReorderableList list;
        private string[] fullPathDuplicates;
        private TemplSettings settings;
        private bool isValid;
        private Type[] entryTypes;
        private SerializedProperty entriesProperty;
        private SerializedProperty scaffoldsProperty;
        private TemplScaffoldTreeView scaffoldsTreeView;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            if (Foldout(entriesProperty, LiveTitle))
            {
                DrawLiveTemplEntries();
            }
            GUILayout.Space(Padding);
            if (Foldout(scaffoldsProperty, ScaffoldsTitle))
            {
                DrawTemplScaffolds();
            }
        }

        private void OnEnable()
        {
            LoadIcons();
            entryTypes = TypeCache
                .Where(IsEntryType)
                .ToArray();
            entriesProperty =
                serializedObject.FindProperty(nameof(TemplSettings.Entries).ToLower());
            scaffoldsProperty =
                serializedObject.FindProperty(nameof(TemplSettings.Scaffolds).ToLower());
            settings = serializedObject.targetObject as TemplSettings;
            list = new ReorderableList(serializedObject, entriesProperty,
                true, true, true, true)
            {
                elementHeight = (DoubleLine * Double) + Spacing + Padding
            };
            InitializeTreeView();
            list.drawElementCallback += OnDrawElement;
            list.drawHeaderCallback += OnDrawHeader;
            list.onAddDropdownCallback += OnAddDropdown;
            Undo.undoRedoPerformed += OnChange;
            settings.FullReset += OnChange;
            settings.ScaffoldChange += OnScaffoldChange;
            scaffoldsTreeView.BeforeDrop += OnBeforeScaffoldDrop;
            OnChange();
        }

        private void OnDisable()
        {
            list.drawElementCallback -= OnDrawElement;
            list.drawHeaderCallback -= OnDrawHeader;
            list.onAddDropdownCallback -= OnAddDropdown;
            Undo.undoRedoPerformed -= OnChange;
            settings.FullReset -= OnChange;
            settings.ScaffoldChange -= OnScaffoldChange;
            scaffoldsTreeView.BeforeDrop -= OnBeforeScaffoldDrop;
            SessionState.SetString(SessionStateKeyPrefix + settings.GetInstanceID(),
                JsonUtility.ToJson(scaffoldsTreeView.state));
        }

        private void DrawLiveTemplEntries()
        {
            EditorGUI.BeginChangeCheck();
            list.DoLayoutList();
            serializedObject.ApplyModifiedProperties();
            if (EditorGUI.EndChangeCheck())
            {
                OnChange();
            }
            GUI.enabled = settings.Entries.Count > 0;
            GUILayout.Space(Padding);
            if (GUILayout.Button(ForceRenderButtonText))
            {
                Core.RenderAllValidEntries();
            }
            GUI.enabled = true;
            if (!isValid)
            {
                EditorGUILayout.HelpBox(ErrorMessage, MessageType.Error);
            }
        }

        private void DrawTemplScaffolds()
        {
            var rect = GUILayoutUtility.GetRect(0, Line + Padding);
            var totalWidth = rect.width;
            rect = new Rect(rect.x, rect.y, NewButtonWidth, rect.height - Spacing);
            if (GUI.Button(rect, new GUIContent(NewButtonText, ScaffoldTooltip)))
            {
                ScaffoldAction(_ => settings.CreateNewScaffold(),
                nameof(settings.CreateNewScaffold));
            }
            rect = new Rect(
                totalWidth - FileButtonWidth - RemoveButtonWidth - (Padding * Double) - Spacing,
                rect.y,
                DirectoryButtonWidth,
                rect.height);
            if (GUI.Button(rect, new GUIContent(FolderIcon, FolderTooltip)))
            {
                ScaffoldAction(settings.AddScaffoldDirectoryNode, nameof(settings.AddScaffoldDirectoryNode));
            }
            rect = new Rect(rect.x + DirectoryButtonWidth, rect.y, FileButtonWidth, rect.height);
            if (GUI.Button(rect, new GUIContent(FileIcon, FileTooltip)))
            {
                ScaffoldAction(settings.AddScaffoldFileNode, nameof(settings.AddScaffoldFileNode));
            }
            rect = new Rect(rect.x + FileButtonWidth, rect.y, RemoveButtonWidth, rect.height);
            if (GUI.Button(rect, new GUIContent(DeleteIcon, DeleteTooltip)))
            {
                ScaffoldAction(settings.RemoveScaffoldNodes, nameof(settings.RemoveScaffoldNodes));
            }
            rect = GUILayoutUtility.GetRect(0, MaxScaffoldsWidth, 0, scaffoldsTreeView.totalHeight);
            scaffoldsTreeView.OnGUI(rect);
        }

        private void LoadIcons()
        {
            ScaffoldIcon = ScaffoldIcon
                ? ScaffoldIcon
                : EditorGUIUtility.FindTexture(ScaffoldIconName);
            FolderIcon = FolderIcon
                ? FolderIcon
                : EditorGUIUtility.FindTexture(FolderIconName);
            FileIcon = FileIcon
                ? FileIcon
                : AssetDatabase.LoadAssetAtPath<Texture2D>(ScribanIconPath);
            DeleteIcon = DeleteIcon
                ? DeleteIcon
                : EditorGUIUtility.FindTexture(DeleteIconName);
        }

        private void InitializeTreeView()
        {
            var treeViewState = new TreeViewState();
            var jsonState = SessionState
                .GetString(SessionStateKeyPrefix + settings.GetInstanceID(), "");
            if (!string.IsNullOrEmpty(jsonState))
                JsonUtility.FromJsonOverwrite(jsonState, treeViewState);
            scaffoldsTreeView = new TemplScaffoldTreeView(
                treeViewState,
                settings, ScaffoldIcon, FolderIcon, FileIcon);
        }

        private void ScaffoldAction(Action<TemplScaffoldNode[]> action, string name)
        {
            Undo.RecordObject(settings, name);
            var selectedNodes = scaffoldsTreeView.GetNodeSelection();
            action(selectedNodes);
        }

        private void OnScaffoldChange(IReadOnlyList<TemplScaffoldNode> nodes) =>
            EditorUtility.SetDirty(settings);

        private void OnBeforeScaffoldDrop() =>
            Undo.RecordObject(settings, nameof(settings.MoveScaffoldNodes));

        private void OnChange()
        {
            CollectDuplicates();
            CheckValidity();
            scaffoldsTreeView.Reload();
        }

        private void OnDrawElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            EditorGUI.BeginChangeCheck();
            var element = list.serializedProperty.GetArrayElementAtIndex(index);
            var entry = settings.Entries[index];
            DrawHeaderLine(rect);
            rect.y += 4;
            DrawFirstRow(rect, element, entry);
            DrawSecondRow(rect, element, entry);
            if (EditorGUI.EndChangeCheck())
            {
                Core.FlagChangedEntry(entry);
            }
        }

        private void DrawFirstRow(Rect rect, SerializedProperty element, TemplEntry entry)
        {
            DrawPropertyField(new Rect(
                rect.x,
                rect.y,
                (rect.width * Half) - Padding,
                Line),
                element.FindPropertyRelative(entry.InputFieldName),
                _ => entry.IsValidInput);
            DrawPropertyField(new Rect(
                rect.x + (rect.width * Half) + Padding,
                rect.y,
                (rect.width * Half) - Padding,
                Line),
                element.FindPropertyRelative(nameof(TemplEntry.directory)),
                p => NotNullReference(p));
        }

        private void DrawSecondRow(Rect rect, SerializedProperty element, TemplEntry entry)
        {
            DrawPropertyField(new Rect(
                rect.x,
                rect.y + Spacing + DoubleLine,
                (rect.width * Half) - Padding,
                Line),
                element.FindPropertyRelative(nameof(TemplEntry.template)),
                p => NotNullReference(p) && IsValidTemplate(p));
            DrawPropertyField(new Rect(
                rect.x + (rect.width * Half) + Padding,
                rect.y + Spacing + DoubleLine,
                (rect.width * Half) - Padding,
                Line),
                element.FindPropertyRelative(nameof(TemplEntry.filename)),
                p => ValidFilename(p, entry));
        }

        private void OnDrawHeader(Rect rect) => EditorGUI.LabelField(rect, Header);

        private void OnAddDropdown(Rect buttonRect, ReorderableList list)
        {
            var menu = new GenericMenu();
            foreach (var entryType in entryTypes)
            {
                menu.AddItem(new GUIContent(entryType.Name),
                false, OnAddElement,
                entryType);
            }
            menu.ShowAsContext();
        }

        private void OnAddElement(object target)
        {
            var index = list.serializedProperty.arraySize;
            list.serializedProperty.arraySize++;
            list.index = index;
            var element = list.serializedProperty.GetArrayElementAtIndex(index);
            element.managedReferenceValue = Activator.CreateInstance(target as Type);
            serializedObject.ApplyModifiedProperties();
            OnChange();
        }

        private void DrawPropertyField(
            Rect rect,
            SerializedProperty property,
            Func<SerializedProperty, bool> isValid)
        {
            var style = new GUIStyle()
            {
                normal = { textColor = isValid(property) ? ValidColor : InvalidColor }
            };
            EditorGUI.LabelField(
                new Rect(rect.x, rect.y, rect.width, rect.height),
                property.displayName, style);
            EditorGUI.PropertyField(
                new Rect(rect.x, rect.y + Line, rect.width, rect.height),
                property, GUIContent.none);
        }

        private void CollectDuplicates() =>
            fullPathDuplicates = settings.Entries
            .Select(e => e.fullPathCache = e.FullPath)
            .GroupBy(p => p)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToArray();

        private void CheckValidity() =>
            isValid = settings.Entries.All(e => e.IsValid) && fullPathDuplicates.Length == 0;

        private bool ValidFilename(SerializedProperty property, TemplEntry entry)
        {
            property.stringValue = property.stringValue.Trim();
            property.stringValue = property.stringValue.Length > MaxFilenameLength
                ? property.stringValue.Substring(0, MaxFilenameLength)
                : property.stringValue;
            return !string.IsNullOrWhiteSpace(property.stringValue) &&
            property.stringValue.IndexOfAny(Path.GetInvalidFileNameChars()) == -1 &&
            (!fullPathDuplicates?.Contains(entry.fullPathCache) ?? true);
        }

        [MenuItem(MenuName, priority = 10)]
        private static void FindSettings()
        {
            var settings = TemplSettings.Instance
                ? TemplSettings.Instance
                : TemplSettings.CreateNewSettings();
            Selection.activeObject = settings;
        }

        private static bool IsEntryType(Type type) =>
            type.IsSubclassOf(typeof(TemplEntry)) && !type.IsAbstract &&
            type.IsDefined(typeof(TemplEntryInfoAttribute), false) &&
            type.GetFields().Count(IsValidInputField) == ValidInputFieldCount;

        private static bool IsValidInputField(FieldInfo field) =>
            field.IsDefined(typeof(TemplInputAttribute), false) &&
            field.FieldType.IsSubclassOf(typeof(UnityEngine.Object));

        private static void DrawHeaderLine(Rect rect) =>
            EditorGUI.LabelField(new Rect(
                rect.x,
                rect.y - 6,
                rect.width,
                rect.height), string.Empty, GUI.skin.horizontalSlider);

        private static bool NotNullReference(SerializedProperty property) =>
            property.objectReferenceValue;

        private static bool IsValidTemplate(SerializedProperty property) =>
            property.objectReferenceValue is ScribanAsset template && !template.HasErrors;

        private static string Capitalize(string input) =>
            char.ToUpper(input[0]) + input.Substring(1);

        private static bool Foldout(SerializedProperty property, string name) =>
            property.isExpanded = EditorGUILayout.Foldout(property.isExpanded, name);
    }
}
