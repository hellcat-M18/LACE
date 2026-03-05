using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Lace.Runtime;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace Lace.Editor
{
    /// <summary>
    /// 選択中の GameObject を制御対象にした CostumeItem を生成するための
    /// Tools 起動ウィンドウ。
    /// </summary>
    internal static class CostumeItemCreateFromSelection
    {
        private const string ParentName = "LACE Menu Items";

        [MenuItem("Tools/LACE/Create CostumeItem", false, 10)]
        private static void OpenWindowFromTools()
        {
            CostumeItemCreateWindow.Open();
        }

        private class CostumeItemCreateWindow : EditorWindow
        {
            private GameObject[] _targets;
            private VRCAvatarDescriptor _avatar;
            private string _itemName;
            private int _installTargetIndex;

            private List<MenuEntry> _menuEntries;

            private struct MenuEntry
            {
                public string Path;
                public VRCExpressionsMenu Menu;
            }

            public static void Open()
            {
                // 既に開いている場合はそれを使い回す
                var existing = Resources.FindObjectsOfTypeAll<CostumeItemCreateWindow>();
                var win = existing != null && existing.Length > 0 ? existing[0] : null;
                if (win == null)
                    win = CreateInstance<CostumeItemCreateWindow>();

                win.titleContent = new GUIContent("メニュー生成");
                win.minSize = new Vector2(360, 160);
                win.ResetFromSelection();

                // 常設ウィンドウとして表示（生成後も閉じない）
                win.Show();
                win.Focus();
            }

            private void OnEnable()
            {
                ResetFromSelection();
            }

            private void OnSelectionChange()
            {
                ResetFromSelection();
                Repaint();
            }

            private void ResetFromSelection()
            {
                _targets = Selection.gameObjects ?? Array.Empty<GameObject>();
                _avatar = FindSingleAvatar(_targets);

                // 初期値は選択オブジェクト名（ユーザーが空欄にした場合は Generate() 側で補完）
                _itemName = GuessDefaultName(_targets);

                _menuEntries = BuildMenuEntries(_avatar);
                _installTargetIndex = 0; // (未指定)
            }

            private static string GuessDefaultName(GameObject[] targets)
            {
                if (targets == null || targets.Length == 0) return "LACE Item";
                foreach (var t in targets)
                {
                    if (t != null) return t.name;
                }
                return "LACE Item";
            }

            private static VRCAvatarDescriptor FindSingleAvatar(GameObject[] targets)
            {
                VRCAvatarDescriptor avatar = null;
                foreach (var go in targets)
                {
                    if (go == null) continue;
                    var a = FindAvatarDescriptor(go.transform);
                    if (a == null) continue;
                    if (avatar == null) avatar = a;
                    else if (avatar != a) return null; // 複数アバター混在
                }
                return avatar;
            }

            private static List<MenuEntry> BuildMenuEntries(VRCAvatarDescriptor avatar)
            {
                var entries = new List<MenuEntry>();

                // 「未指定」＝ルート直下
                entries.Add(new MenuEntry { Path = "(未指定)", Menu = null });

                if (avatar == null || avatar.expressionsMenu == null)
                    return entries;

                CollectMenuHierarchy(avatar.expressionsMenu, "", entries);
                return entries;
            }

            private void OnGUI()
            {
                // EditorGUILayout.LabelField("選択オブジェクトから CostumeItem を生成", EditorStyles.boldLabel);
                // EditorGUILayout.Space(4);

                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.IntField("対象数", _targets?.Length ?? 0);
                }

                _itemName = EditorGUILayout.TextField(
                    new GUIContent("メニュー名", "空欄なら選択オブジェクトの名前を使用します。"),
                    _itemName);

                DrawInstallTargetField();

                EditorGUILayout.Space(8);

                // バリデーション
                string error = Validate();
                if (!string.IsNullOrEmpty(error))
                    EditorGUILayout.HelpBox(error, MessageType.Warning);

                using (new EditorGUI.DisabledScope(!string.IsNullOrEmpty(error)))
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("生成", GUILayout.Width(90)))
                    {
                        Generate();
                        GUIUtility.ExitGUI();
                    }
                    if (GUILayout.Button("閉じる", GUILayout.Width(90)))
                    {
                        Close();
                        GUIUtility.ExitGUI();
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }

            private void DrawInstallTargetField()
            {
                if (_menuEntries == null) _menuEntries = BuildMenuEntries(_avatar);

                var labels = new string[_menuEntries.Count];
                for (int i = 0; i < _menuEntries.Count; i++)
                    labels[i] = _menuEntries[i].Path;

                var tooltip = "未指定の場合はルート直下にインストールします。サブメニューを選ぶと、その配下にインストールします。";
                _installTargetIndex = EditorGUILayout.Popup(
                    new GUIContent("インストール先", tooltip),
                    Mathf.Clamp(_installTargetIndex, 0, _menuEntries.Count - 1),
                    labels);
            }

            private string Validate()
            {
                if (_targets == null || _targets.Length == 0)
                    return "対象オブジェクトを選択してください。";

                foreach (var go in _targets)
                {
                    if (go == null) return "対象に null が含まれています。選択をやり直してください。";
                }

                // アバター未検出は許容（ただし installTarget のドロップダウンがルートのみ）
                // ただし複数アバター混在はNG
                var single = FindSingleAvatar(_targets);
                if (_avatar == null && single == null)
                {
                    // _avatar==null は「未検出 or 混在」。混在だけ弾く。
                    bool hasAny = false;
                    VRCAvatarDescriptor first = null;
                    foreach (var go in _targets)
                    {
                        var a = FindAvatarDescriptor(go.transform);
                        if (a == null) continue;
                        hasAny = true;
                        if (first == null) first = a;
                        else if (first != a) return "複数のアバターにまたがって選択されています。1体のアバター配下で選択してください。";
                    }
                    if (hasAny) _avatar = first;
                }

                return null;
            }

            private void Generate()
            {
                // 生成は現在のウィンドウの状態（選択スナップショット）に対して行う
                var avatar = FindSingleAvatar(_targets) ?? _avatar;

                var itemName = string.IsNullOrWhiteSpace(_itemName)
                    ? GuessDefaultName(_targets)
                    : _itemName.Trim();

                Transform parent = null;
                if (avatar != null)
                {
                    parent = EnsureParent(avatar.transform);
                }
                else
                {
                    // アバターが見つからない場合は、選択オブジェクトの親のルートに作る
                    parent = EnsureParent(_targets[0].transform.root);
                }

                Undo.IncrementCurrentGroup();
                int group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Create LACE Costume Item");

                var go = new GameObject(itemName);
                Undo.RegisterCreatedObjectUndo(go, "Create LACE Costume Item");
                go.transform.SetParent(parent, false);

                var item = Undo.AddComponent<CostumeItem>(go);

                // パラメータ名は基本的にアイテム名
                item.parameterName = go.name;

                // インストール先
                if (_menuEntries == null || _menuEntries.Count == 0)
                {
                    item.installTargetMenu = null;
                }
                else
                {
                    var entry = _menuEntries[Mathf.Clamp(_installTargetIndex, 0, _menuEntries.Count - 1)];
                    item.installTargetMenu = entry.Menu;
                }

                // 制御対象
                item.target = RuleTarget.GameObject;
                item.targetObjects = new List<GameObject>(_targets);

                Undo.CollapseUndoOperations(group);

                // 選択を勝手に変えない（選択変更でウィンドウ内容がリセットされるため）
                EditorGUIUtility.PingObject(go);
            }

            private static Transform EnsureParent(Transform avatarRoot)
            {
                if (avatarRoot == null) return null;

                Transform existing = null;
                for (int i = 0; i < avatarRoot.childCount; i++)
                {
                    var c = avatarRoot.GetChild(i);
                    if (c != null && c.name == ParentName)
                    {
                        existing = c;
                        break;
                    }
                }

                if (existing != null) return existing;

                var parentGo = new GameObject(ParentName);
                Undo.RegisterCreatedObjectUndo(parentGo, "Create LACE Parent");
                parentGo.transform.SetParent(avatarRoot, false);
                return parentGo.transform;
            }

            private static void CollectMenuHierarchy(
                VRCExpressionsMenu menu, string parentPath,
                List<MenuEntry> entries, int depth = 0)
            {
                if (menu == null || depth > 10) return;

                foreach (var control in menu.controls)
                {
                    if (control.type != VRCExpressionsMenu.Control.ControlType.SubMenu)
                        continue;
                    if (control.subMenu == null) continue;

                    var path = string.IsNullOrEmpty(parentPath)
                        ? control.name
                        : $"{parentPath}/{control.name}";

                    entries.Add(new MenuEntry { Path = path, Menu = control.subMenu });
                    CollectMenuHierarchy(control.subMenu, path, entries, depth + 1);
                }
            }

            private static VRCAvatarDescriptor FindAvatarDescriptor(Transform t)
            {
                while (t != null)
                {
                    var desc = t.GetComponent<VRCAvatarDescriptor>();
                    if (desc != null) return desc;
                    t = t.parent;
                }
                return null;
            }
        }
    }
}
