using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace JobTest.TacticsVoxel
{
    public sealed class DocsWindow : EditorWindow
    {
        internal const string DocsRoot = "Assets/Docs";
        private static readonly string[] DocumentOrder =
        {
            "README.md", "GAMEPLAY.md", "LEVEL_EDITOR.md", "FEATURES.md",
            "Features/CORE.md", "Features/MECHANISMS.md", "Features/FRONTEND.md",
            "Features/TOOLS.md", "Features/TERRAIN_CHANGES.md", "Features/ART.md",
            "ISSUES.md", "AGENTS.md"
        };
        private static int CompareDocumentPaths(string left, string right)
        {
            int Rank(string path)
            {
                string relative = path.Substring(DocsRoot.Length + 1);
                int index = Array.FindIndex(DocumentOrder, name => string.Equals(name, relative, StringComparison.OrdinalIgnoreCase));
                return index >= 0 ? index : DocumentOrder.Length;
            }
            int result = Rank(left).CompareTo(Rank(right));
            return result != 0 ? result : StringComparer.OrdinalIgnoreCase.Compare(left, right);
        }
        [SerializeField] private string selectedPath = DocsRoot + "/README.md";
        [SerializeField] private bool sourceMode;
        [SerializeField] private string draftPath = "";
        [SerializeField] private string draftText = "";
        [SerializeField] private string savedText = "";
        private readonly List<string> history = new();
        private readonly Dictionary<string, string> documents = new();
        private readonly HashSet<string> unreadable = new();
        private int historyIndex = -1;
        private string search = "";
        private ScrollView directory;
        private ScrollView article;
        private VisualElement contents;
        private Label pathLabel;
        private ToolbarButton back;
        private ToolbarButton forward;
        private ToolbarToggle sourceToggle;
        private ToolbarButton save;
        private ToolbarButton revert;
        private TextField sourceField;
        private HelpBox sourceNotice;
        private string saveError = "";
        private ToolbarButton locate;
        private bool refreshPending;
        private DocsRenderer renderer;
        private bool DraftIsDirty => draftPath == selectedPath && draftText != savedText;

        [MenuItem("TD推箱子/文档")]
        public static void Open() => GetWindow<DocsWindow>("项目文档").Show();

        [OnOpenAsset(0)]
        private static bool OpenMarkdown(int instanceId, int line)
        {
            string path = AssetDatabase.GetAssetPath(instanceId);
            if (!IsDocument(path)) return false;
            DocsWindow window = GetWindow<DocsWindow>("项目文档");
            window.Show();
            window.OpenDocument(path);
            return true;
        }

        private void OnEnable()
        {
            Selection.selectionChanged += FollowSelection;
            EditorApplication.projectChanged += QueueRefresh;
            Undo.undoRedoPerformed += HandleUndo;
            UpdateDirtyState();
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= FollowSelection;
            EditorApplication.projectChanged -= QueueRefresh;
            Undo.undoRedoPerformed -= HandleUndo;
            EditorApplication.delayCall -= Refresh;
            renderer?.Dispose();
        }

        public void CreateGUI()
        {
            minSize = new Vector2(520, 320);
            rootVisualElement.Clear();
            var toolbar = new Toolbar();
            back = IconButton("back", "上一份文档", () => Navigate(-1));
            forward = IconButton("forward", "下一份文档", () => Navigate(1));
            toolbar.Add(back);
            toolbar.Add(forward);
            toolbar.Add(IconButton("Refresh", "刷新文档", Refresh));
            var filter = new ToolbarSearchField();
            filter.style.width = 150;
            filter.RegisterValueChangedCallback(evt => { search = evt.newValue; BuildDirectory(); });
            toolbar.Add(filter);
            pathLabel = new Label();
            pathLabel.style.flexGrow = 1;
            pathLabel.style.flexShrink = 1;
            pathLabel.style.minWidth = 0;
            pathLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            pathLabel.style.overflow = Overflow.Hidden;
            pathLabel.style.textOverflow = TextOverflow.Ellipsis;
            toolbar.Add(pathLabel);
            sourceToggle = new ToolbarToggle { text = "源文件", tooltip = "切换源文编辑与 Markdown 预览" };
            sourceToggle.SetValueWithoutNotify(sourceMode);
            sourceToggle.RegisterValueChangedCallback(evt =>
            {
                sourceMode = evt.newValue;
                RenderDocument();
            });
            save = new ToolbarButton(() => TrySaveDraft()) { text = "保存", tooltip = "保存当前文档 (Ctrl+S)" };
            revert = new ToolbarButton(() =>
            {
                if (EditorUtility.DisplayDialog("还原文档", "放弃当前草稿并重新读取磁盘文件？", "还原", "取消")) DiscardChanges();
            }) { text = "还原", tooltip = "放弃未保存的改动" };
            locate = new ToolbarButton(LocateAsset) { text = "定位" };
            toolbar.Add(sourceToggle);
            toolbar.Add(save);
            toolbar.Add(revert);
            toolbar.Add(locate);
            rootVisualElement.Add(toolbar);
            var split = new TwoPaneSplitView(0, 210, TwoPaneSplitViewOrientation.Horizontal);
            split.style.flexGrow = 1;
            directory = new ScrollView();
            directory.style.minWidth = 130;
            split.Add(directory);
            article = new ScrollView(ScrollViewMode.Vertical);
            article.style.flexGrow = 1;
            article.contentContainer.style.paddingLeft = 22;
            article.contentContainer.style.paddingRight = 22;
            article.contentContainer.style.paddingTop = 14;
            article.contentContainer.style.paddingBottom = 32;
            var reader = new VisualElement();
            reader.style.flexGrow = 1;
            reader.style.minWidth = 0;
            reader.style.minHeight = 0;
            sourceNotice = new HelpBox("", HelpBoxMessageType.Warning);
            reader.Add(sourceNotice);
            reader.Add(article);
            sourceField = new TextField { name = "document-source", multiline = true, isDelayed = false };
            sourceField.RegisterCallback<AttachToPanelEvent>(_ =>
            {
                var scroll = sourceField.Q<ScrollView>();
                if (scroll != null) scroll.verticalScrollerVisibility = ScrollerVisibility.Auto;
            });
            sourceField.style.flexGrow = 1;
            sourceField.style.flexShrink = 1;
            sourceField.style.flexBasis = 0;
            sourceField.style.minHeight = 0;
            sourceField.style.whiteSpace = WhiteSpace.Normal;
            sourceField.style.fontSize = 14;
            foreach (TextElement text in sourceField.Query<TextElement>().ToList())
            {
                text.enableRichText = false;
                text.parseEscapeSequences = false;
            }
            sourceField.RegisterValueChangedCallback(evt =>
            {
                Undo.RecordObject(this, "编辑文档");
                draftText = evt.newValue;
                saveError = "";
                UpdateDirtyState();
            });
            reader.Add(sourceField);
            split.Add(reader);
            rootVisualElement.Add(split);
            rootVisualElement.UnregisterCallback<KeyDownEvent>(HandleSourceKey, TrickleDown.TrickleDown);
            rootVisualElement.RegisterCallback<KeyDownEvent>(HandleSourceKey, TrickleDown.TrickleDown);
            Refresh();
        }

        private static ToolbarButton IconButton(string icon, string tooltip, Action action)
        {
            var button = new ToolbarButton(action) { tooltip = tooltip };
            Texture image = EditorGUIUtility.IconContent(icon).image;
            if (image != null) button.Add(new Image { image = image, style = { width = 16, height = 16 } });
            else button.text = tooltip;
            return button;
        }

        private void FollowSelection()
        {
            string path = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (IsDocument(path)) OpenDocument(path);
        }

        internal static bool IsDocument(string path) => path != null
            && path.StartsWith(DocsRoot + "/", StringComparison.Ordinal)
            && string.Equals(Path.GetExtension(path), ".md", StringComparison.OrdinalIgnoreCase);

        private void QueueRefresh()
        {
            if (refreshPending) return;
            refreshPending = true;
            EditorApplication.delayCall += Refresh;
        }

        internal static void NotifyAssetsChanged()
        {
            foreach (DocsWindow window in Resources.FindObjectsOfTypeAll<DocsWindow>()) window.QueueRefresh();
        }

        private void Refresh()
        {
            EditorApplication.delayCall -= Refresh;
            refreshPending = false;
            if (directory == null) return;
            Vector2 offset = article.scrollOffset;
            documents.Clear();
            unreadable.Clear();
            if (Directory.Exists(DocsRoot))
            {
                foreach (string file in Directory.GetFiles(DocsRoot, "*", SearchOption.AllDirectories))
                {
                    string path = file.Replace('\\', '/');
                    if (!IsDocument(path)) continue;
                    try { documents[path] = File.ReadAllText(path); }
                    catch (IOException) { documents[path] = "无法读取文档，请检查文件是否被占用。"; unreadable.Add(path); }
                    catch (UnauthorizedAccessException) { documents[path] = "没有读取此文档的权限。"; unreadable.Add(path); }
                }
            }
            if (!documents.ContainsKey(selectedPath) && !DraftIsDirty)
            {
                var paths = new List<string>(documents.Keys);
                paths.Sort(CompareDocumentPaths);
                selectedPath = documents.ContainsKey(DocsRoot + "/README.md")
                    ? DocsRoot + "/README.md" : paths.Count > 0 ? paths[0] : "";
            }
            if (historyIndex < 0 && selectedPath.Length > 0) RecordHistory(selectedPath);
            BuildDirectory();
            RenderDocument();
            article.schedule.Execute(() => article.scrollOffset = offset);
        }

        private void BuildDirectory()
        {
            if (directory == null) return;
            directory.Clear();
            var paths = new List<string>(documents.Keys);
            paths.Sort(CompareDocumentPaths);
            var folders = new Dictionary<string, VisualElement> { [DocsRoot] = directory.contentContainer };
            int count = 0;
            foreach (string path in paths)
            {
                if (search.Length > 0 && path.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0
                    && documents[path].IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0) continue;
                VisualElement parent = directory.contentContainer;
                string relative = path.Substring(DocsRoot.Length + 1);
                if (search.Length == 0)
                {
                    string current = DocsRoot;
                    string[] parts = relative.Split('/');
                    for (int i = 0; i < parts.Length - 1; i++)
                    {
                        current += "/" + parts[i];
                        if (!folders.TryGetValue(current, out VisualElement folder))
                        {
                            var foldout = new Foldout { text = parts[i], value = true, viewDataKey = current };
                            parent.Add(foldout);
                            folder = foldout.contentContainer;
                            folders[current] = folder;
                        }
                        parent = folder;
                    }
                }
                var button = new Button(() => OpenDocument(path))
                {
                    text = search.Length > 0 ? relative : Path.GetFileNameWithoutExtension(path), tooltip = path
                };
                button.style.unityTextAlign = TextAnchor.MiddleLeft;
                button.style.whiteSpace = WhiteSpace.Normal;
                button.style.minHeight = 26;
                if (path == selectedPath) button.style.backgroundColor = new Color(0.2f, 0.4f, 0.5f, 0.4f);
                parent.Add(button);
                count++;
            }
            if (count == 0) directory.Add(new Label(search.Length > 0 ? "没有匹配文档" : "Assets/Docs 中没有 Markdown 文档"));
            contents = new Foldout { text = "本页目录", value = true };
            contents.style.marginTop = 14;
            directory.Add(contents);
            renderer?.BuildContents(contents);
        }

        private bool OpenDocument(string path, string anchor = "", bool record = true)
        {
            if (!IsDocument(path)) return false;
            if (!File.Exists(path))
            {
                ShowNotification(new GUIContent("找不到文档：" + path));
                return false;
            }
            if (path != selectedPath && !ConfirmDocumentChange()) return false;
            if (path != selectedPath) Undo.ClearUndo(this);
            selectedPath = path;
            if (record) RecordHistory(path);
            if (article == null) return true;
            if (!documents.ContainsKey(path)) Refresh();
            BuildDirectory();
            RenderDocument();
            article.scrollOffset = Vector2.zero;
            if (anchor.Length > 0) renderer?.JumpTo(anchor);
            return true;
        }

        private void RecordHistory(string path)
        {
            if (historyIndex >= 0 && history[historyIndex] == path) return;
            if (historyIndex + 1 < history.Count) history.RemoveRange(historyIndex + 1, history.Count - historyIndex - 1);
            history.Add(path);
            historyIndex = history.Count - 1;
        }

        private void Navigate(int delta)
        {
            int next = historyIndex + delta;
            if (next < 0 || next >= history.Count) return;
            if (OpenDocument(history[next], record: false))
            {
                historyIndex = next;
                UpdateDirtyState();
            }
        }

        private void RenderDocument()
        {
            renderer?.Dispose();
            renderer = null;
            article.Clear();
            contents?.Clear();
            bool available = documents.TryGetValue(selectedPath, out string markdown);
            if (!DraftIsDirty && available && !unreadable.Contains(selectedPath))
            {
                draftPath = selectedPath;
                savedText = markdown;
                draftText = markdown;
            }
            bool editable = draftPath == selectedPath && (DraftIsDirty || available && !unreadable.Contains(selectedPath));
            sourceToggle.SetEnabled(editable);
            sourceField.SetEnabled(editable);
            sourceField.style.display = sourceMode ? DisplayStyle.Flex : DisplayStyle.None;
            article.style.display = sourceMode ? DisplayStyle.None : DisplayStyle.Flex;
            if (sourceField.value != draftText) sourceField.SetValueWithoutNotify(draftText);
            locate.SetEnabled(available);
            UpdateDirtyState();
            if (sourceMode) return;
            if (!available && !DraftIsDirty)
            {
                article.Add(new Label("将 Markdown 文档放入 Assets/Docs 后点击刷新。"));
                return;
            }
            try
            {
                renderer = new DocsRenderer(article, selectedPath, OpenLink);
                renderer.Render(editable ? draftText : markdown);
                renderer.BuildContents(contents);
            }
            catch (Exception exception)
            {
                article.Add(new HelpBox("文档渲染失败：" + exception.Message, HelpBoxMessageType.Error));
                article.Add(new TextField { multiline = true, value = markdown, isReadOnly = true });
                Debug.LogException(exception);
            }
        }

        private void OpenLink(string target)
        {
            if (target.StartsWith("#", StringComparison.Ordinal))
            {
                renderer?.JumpTo(Uri.UnescapeDataString(target.Substring(1)));
                return;
            }
            if (Uri.TryCreate(target, UriKind.Absolute, out Uri uri) && !uri.IsFile)
            {
                if (uri.Scheme == "https" || uri.Scheme == "http" || uri.Scheme == "mailto") Application.OpenURL(target);
                return;
            }
            int hash = target.IndexOf('#');
            string fragment = hash >= 0 ? Uri.UnescapeDataString(target.Substring(hash + 1)) : "";
            string file = hash >= 0 ? target.Substring(0, hash) : target;
            string path = DocsRenderer.ResolveAssetPath(selectedPath, file);
            if (path == null) { ShowNotification(new GUIContent("链接不在 Assets 内")); return; }
            if (IsDocument(path)) { OpenDocument(path, fragment); return; }
            UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (asset != null) { EditorGUIUtility.PingObject(asset); AssetDatabase.OpenAsset(asset); }
            else ShowNotification(new GUIContent("找不到链接目标：" + file));
        }

        private void UpdateDirtyState()
        {
            hasUnsavedChanges = DraftIsDirty;
            saveChangesMessage = "文档 " + draftPath + " 有未保存的修改。是否保存？";
            if (pathLabel == null) return;
            pathLabel.text = selectedPath + (DraftIsDirty ? " *" : "");
            pathLabel.tooltip = selectedPath;
            save.SetEnabled(DraftIsDirty);
            revert.SetEnabled(DraftIsDirty);
            back.SetEnabled(historyIndex > 0);
            forward.SetEnabled(historyIndex + 1 < history.Count);
            string notice = saveError;
            if (notice.Length == 0 && DraftIsDirty)
            {
                if (!documents.ContainsKey(draftPath)) notice = "源文件已移动或删除，未保存的草稿仍保留在窗口中。";
                else if (unreadable.Contains(draftPath)) notice = "当前无法读取磁盘文件，未保存的草稿仍保留在窗口中。";
                else if (documents[draftPath] != savedText && documents[draftPath] != draftText)
                    notice = "磁盘文件已被外部修改；保存时将确认如何处理。";
            }
            sourceNotice.text = notice;
            sourceNotice.style.display = notice.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void HandleUndo()
        {
            if (sourceField == null) return;
            sourceToggle.SetValueWithoutNotify(sourceMode);
            sourceField.SetValueWithoutNotify(draftText);
            RenderDocument();
        }

        private void HandleSourceKey(KeyDownEvent evt)
        {
            if (!evt.actionKey || evt.keyCode != KeyCode.S) return;
            TrySaveDraft();
            evt.StopImmediatePropagation();
        }

        private bool ConfirmDocumentChange()
        {
            if (!DraftIsDirty) return true;
            int choice = EditorUtility.DisplayDialogComplex("未保存的文档", saveChangesMessage, "保存并切换", "取消", "放弃并切换");
            if (choice == 0) return TrySaveDraft();
            if (choice == 1) return false;
            DiscardChanges();
            return true;
        }

        private bool HasDiskConflict(string diskText) => diskText != savedText && diskText != draftText;

        private bool TrySaveDraft()
        {
            if (!DraftIsDirty) return true;
            string temporary = null;
            try
            {
                if (!File.Exists(draftPath)) throw new IOException("源文件已移动或删除，无法保存。草稿仍保留在窗口中。");
                string diskText = File.ReadAllText(draftPath);
                if (HasDiskConflict(diskText))
                {
                    int choice = EditorUtility.DisplayDialogComplex("磁盘文件已修改", "外部文件与编辑开始时的版本不同。请选择保留哪个版本。", "覆盖保存", "取消", "加载磁盘版本");
                    if (choice == 1) return false;
                    if (choice == 2) { DiscardChanges(); return true; }
                }
                // Replace only after a complete temporary write, keeping the asset GUID and original on write failure.
                temporary = draftPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
                File.WriteAllText(temporary, draftText, new UTF8Encoding(false));
                File.Replace(temporary, draftPath, null);
                temporary = null;
                savedText = draftText;
                documents[draftPath] = draftText;
                unreadable.Remove(draftPath);
                saveError = "";
                Undo.ClearUndo(this);
                UpdateDirtyState();
                AssetDatabase.ImportAsset(draftPath);
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                saveError = "保存失败：" + exception.Message;
                UpdateDirtyState();
                return false;
            }
            finally
            {
                if (temporary != null && File.Exists(temporary))
                {
                    try { File.Delete(temporary); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
            }
        }

        public override void SaveChanges()
        {
            if (!TrySaveDraft()) throw new IOException("文档未能保存，草稿仍保留在窗口中。");
            base.SaveChanges();
        }

        public override void DiscardChanges()
        {
            Undo.ClearUndo(this);
            draftPath = "";
            draftText = "";
            savedText = "";
            saveError = "";
            base.DiscardChanges();
            Refresh();
        }

        private void LocateAsset()
        {
            UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(selectedPath);
            if (asset != null) EditorGUIUtility.PingObject(asset);
        }
    }

    internal sealed class DocsAssetChanges : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (string[] paths in new[] { imported, deleted, moved, movedFrom })
                foreach (string path in paths)
                    if (path == DocsWindow.DocsRoot || path.StartsWith(DocsWindow.DocsRoot + "/", StringComparison.Ordinal))
                    {
                        DocsWindow.NotifyAssetsChanged();
                        return;
                    }
        }
    }
}
