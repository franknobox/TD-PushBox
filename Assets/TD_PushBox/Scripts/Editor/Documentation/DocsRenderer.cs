using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Extensions.TaskLists;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.UIElements.Experimental;

namespace JobTest.TacticsVoxel
{
    internal sealed class DocsRenderer : IDisposable
    {
        private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
            .UsePipeTables().UseTaskLists().UseEmphasisExtras().DisableHtml().Build();
        private readonly ScrollView article;
        private readonly string documentPath;
        private readonly Action<string> openLink;
        private readonly List<(string title, int level, string anchor, VisualElement element)> headings = new();
        private readonly Dictionary<string, VisualElement> anchors = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> links = new();
        private readonly List<Texture2D> ownedImages = new();
        private readonly List<(VisualElement parent, EventCallback<GeometryChangedEvent> callback)> imageLayouts = new();
        private readonly Color border = EditorGUIUtility.isProSkin ? new Color(0.35f, 0.35f, 0.35f) : new Color(0.7f, 0.7f, 0.7f);

        internal DocsRenderer(ScrollView article, string documentPath, Action<string> openLink)
        {
            this.article = article;
            this.documentPath = documentPath;
            this.openLink = openLink;
        }

        internal void Render(string markdown)
        {
            RenderBlocks(Markdown.Parse(markdown, Pipeline), article.contentContainer);
        }

        internal void BuildContents(VisualElement container)
        {
            if (container == null) return;
            container.Clear();
            foreach (var heading in headings)
            {
                string anchor = heading.anchor;
                var button = new Button(() => JumpTo(anchor)) { text = heading.title };
                button.style.unityTextAlign = TextAnchor.MiddleLeft;
                button.style.whiteSpace = WhiteSpace.Normal;
                button.style.marginLeft = (heading.level - 1) * 8;
                container.Add(button);
            }
        }

        internal void JumpTo(string anchor)
        {
            if (anchors.TryGetValue(anchor, out VisualElement target))
                article.schedule.Execute(() => article.ScrollTo(target));
        }

        private void RenderBlocks(ContainerBlock blocks, VisualElement parent)
        {
            foreach (Block block in blocks)
            {
                switch (block)
                {
                    case HeadingBlock heading:
                        AddHeading(heading, parent);
                        break;
                    case ParagraphBlock paragraph:
                        AddParagraph(paragraph, parent);
                        break;
                    case CodeBlock code:
                        AddCode(code, parent);
                        break;
                    case Table table:
                        AddTable(table, parent);
                        break;
                    case ListBlock list:
                        AddList(list, parent);
                        break;
                    case QuoteBlock quote:
                        var quoteView = new VisualElement();
                        quoteView.style.borderLeftWidth = 3;
                        quoteView.style.borderLeftColor = border;
                        quoteView.style.paddingLeft = 12;
                        quoteView.style.marginBottom = 10;
                        parent.Add(quoteView);
                        RenderBlocks(quote, quoteView);
                        break;
                    case ThematicBreakBlock:
                        var divider = new VisualElement();
                        divider.style.height = 1;
                        divider.style.backgroundColor = border;
                        divider.style.marginTop = 10;
                        divider.style.marginBottom = 14;
                        parent.Add(divider);
                        break;
                    case ContainerBlock container:
                        RenderBlocks(container, parent);
                        break;
                    case LeafBlock leaf:
                        parent.Add(Text(leaf.Lines.ToString(), false));
                        break;
                }
            }
        }

        private void AddHeading(HeadingBlock heading, VisualElement parent)
        {
            string title = PlainText(heading.Inline);
            string anchor = MakeAnchor(title);
            string unique = anchor;
            int suffix = 1;
            while (anchors.ContainsKey(unique)) unique = anchor + "-" + suffix++;
            var label = Text(InlineText(heading.Inline));
            label.style.fontSize = heading.Level switch { 1 => 24, 2 => 20, 3 => 17, _ => 15 };
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginTop = heading.Level == 1 ? 0 : 16;
            label.style.marginBottom = 10;
            parent.Add(label);
            anchors[unique] = label;
            if (!anchors.ContainsKey(title)) anchors[title] = label;
            headings.Add((title, heading.Level, unique, label));
        }

        internal static string MakeAnchor(string title)
        {
            var value = new StringBuilder();
            foreach (char character in title.Trim().ToLowerInvariant())
            {
                if (char.IsWhiteSpace(character)) value.Append('-');
                else if (char.IsLetterOrDigit(character) || character == '-' || character == '_') value.Append(character);
            }
            return value.ToString();
        }

        private void AddParagraph(ParagraphBlock paragraph, VisualElement parent)
        {
            if (paragraph.Inline == null) return;
            var text = new StringBuilder();
            foreach (Inline inline in paragraph.Inline)
            {
                if (inline is LinkInline image && image.IsImage)
                {
                    if (text.Length > 0) { parent.Add(Text(text.ToString())); text.Clear(); }
                    AddImage(image, parent);
                }
                else AppendInline(inline, text);
            }
            if (text.Length > 0) parent.Add(Text(text.ToString()));
        }

        private Label Text(string text, bool rich = true)
        {
            var label = new Label(text) { enableRichText = rich, parseEscapeSequences = false };
            label.selection.isSelectable = true;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.fontSize = 14;
            label.style.marginBottom = 8;
            label.style.flexShrink = 1;
            label.RegisterCallback<PointerUpLinkTagEvent>(evt =>
            {
                if (links.TryGetValue(evt.linkID, out string url)) openLink(url);
                evt.StopPropagation();
            });
            return label;
        }

        private string InlineText(ContainerInline inlines)
        {
            var text = new StringBuilder();
            if (inlines != null) foreach (Inline inline in inlines) AppendInline(inline, text);
            return text.ToString();
        }

        private void AppendInline(Inline inline, StringBuilder text)
        {
            switch (inline)
            {
                case LiteralInline literal:
                    text.Append(Escape(literal.Content.ToString()));
                    break;
                case CodeInline code:
                    text.Append("<color=").Append(EditorGUIUtility.isProSkin ? "#E4BF7A" : "#8B521B")
                        .Append('>').Append(Escape(code.Content)).Append("</color>");
                    break;
                case LineBreakInline line:
                    text.Append(line.IsHard ? "\n" : " ");
                    break;
                case LinkInline link:
                    if (link.IsImage) { text.Append(Escape(PlainText(link))); break; }
                    string id = "doc-" + links.Count;
                    links[id] = link.GetDynamicUrl != null ? link.GetDynamicUrl() : link.Url ?? "";
                    text.Append("<link=\"").Append(id).Append("\"><color=")
                        .Append(EditorGUIUtility.isProSkin ? "#78BDDF" : "#176282").Append("><u>")
                        .Append(InlineText(link)).Append("</u></color></link>");
                    break;
                case EmphasisInline emphasis:
                    string tag = emphasis.DelimiterChar == '~' ? "s" : emphasis.DelimiterCount >= 2 ? "b" : "i";
                    text.Append('<').Append(tag).Append('>').Append(InlineText(emphasis))
                        .Append("</").Append(tag).Append('>');
                    break;
                case TaskList task:
                    text.Append(task.Checked ? "[x] " : "[ ] ");
                    break;
                case ContainerInline container:
                    text.Append(InlineText(container));
                    break;
            }
        }

        // Escape '<' without depending on HTML entity support in Unity rich text.
        private static string Escape(string text) => text.Replace("<", "<noparse><</noparse>");

        private static string PlainText(ContainerInline container)
        {
            var text = new StringBuilder();
            if (container == null) return "";
            foreach (Inline inline in container)
            {
                if (inline is LiteralInline literal) text.Append(literal.Content);
                else if (inline is CodeInline code) text.Append(code.Content);
                else if (inline is LineBreakInline) text.Append(' ');
                else if (inline is ContainerInline child) text.Append(PlainText(child));
            }
            return text.ToString();
        }

        private void AddCode(CodeBlock code, VisualElement parent)
        {
            string source = code.Lines.ToString();
            var block = new VisualElement();
            block.style.backgroundColor = EditorGUIUtility.isProSkin ? new Color(0.13f, 0.13f, 0.13f) : new Color(0.9f, 0.9f, 0.9f);
            block.style.paddingLeft = 10;
            block.style.paddingRight = 10;
            block.style.paddingTop = 6;
            block.style.paddingBottom = 6;
            block.style.marginBottom = 12;
            var toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            var language = new Label(code is FencedCodeBlock fenced ? fenced.Info ?? "" : "");
            language.style.flexGrow = 1;
            toolbar.Add(language);
            toolbar.Add(new Button(() => EditorGUIUtility.systemCopyBuffer = source) { text = "复制" });
            block.Add(toolbar);
            var scroll = new ScrollView(ScrollViewMode.Horizontal);
            var label = Text(source, false);
            label.style.whiteSpace = WhiteSpace.NoWrap;
            scroll.Add(label);
            block.Add(scroll);
            parent.Add(block);
        }

        private void AddList(ListBlock list, VisualElement parent)
        {
            int number = int.TryParse(list.OrderedStart, out int start) ? start : 1;
            foreach (Block block in list)
            {
                if (block is not ListItemBlock item) continue;
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                var marker = new Label(list.IsOrdered ? number++ + "." : "•");
                marker.style.width = 26;
                marker.style.flexShrink = 0;
                marker.style.fontSize = 14;
                marker.style.unityTextAlign = TextAnchor.UpperRight;
                marker.style.paddingRight = 8;
                row.Add(marker);
                var body = new VisualElement();
                body.style.flexGrow = 1;
                body.style.flexShrink = 1;
                row.Add(body);
                parent.Add(row);
                RenderBlocks(item, body);
            }
        }

        private void AddTable(Table table, VisualElement parent)
        {
            var scroll = new ScrollView(ScrollViewMode.Horizontal);
            scroll.style.marginBottom = 12;
            // Horizontal ScrollView arranges direct children in a row; stack table rows separately.
            var body = new VisualElement { name = "markdown-table" };
            body.style.flexDirection = FlexDirection.Column;
            body.style.flexShrink = 0;
            scroll.Add(body);
            foreach (Block block in table)
            {
                if (block is not TableRow row) continue;
                var line = new VisualElement();
                line.style.flexDirection = FlexDirection.Row;
                line.style.flexShrink = 0;
                if (row.IsHeader) line.style.backgroundColor = new Color(0.5f, 0.5f, 0.5f, 0.15f);
                int column = 0;
                foreach (Block child in row)
                {
                    if (child is not TableCell cell) continue;
                    var view = new VisualElement();
                    view.style.width = 200;
                    view.style.flexShrink = 0;
                    view.style.paddingLeft = 8;
                    view.style.paddingRight = 8;
                    view.style.paddingTop = 6;
                    view.style.borderBottomWidth = 1;
                    view.style.borderBottomColor = border;
                    view.style.borderRightWidth = 1;
                    view.style.borderRightColor = border;
                    if (row.IsHeader) view.style.unityFontStyleAndWeight = FontStyle.Bold;
                    if (column < table.ColumnDefinitions.Count)
                    {
                        view.style.unityTextAlign = table.ColumnDefinitions[column].Alignment switch
                        {
                            TableColumnAlign.Center => TextAnchor.UpperCenter,
                            TableColumnAlign.Right => TextAnchor.UpperRight,
                            _ => TextAnchor.UpperLeft
                        };
                    }
                    RenderBlocks(cell, view);
                    line.Add(view);
                    column++;
                }
                body.Add(line);
            }
            parent.Add(scroll);
        }

        private void AddImage(LinkInline link, VisualElement parent)
        {
            string path = ResolveAssetPath(documentPath, link.Url ?? "");
            string alt = PlainText(link);
            if (path == null || !File.Exists(path))
            {
                parent.Add(Text("[图片不可用] " + alt + " " + link.Url, false));
                return;
            }
            Texture2D texture = TDProjectPaths.Load<Texture2D>(path);
            if (texture == null)
            {
                string extension = Path.GetExtension(path).ToLowerInvariant();
                if (extension != ".png" && extension != ".jpg" && extension != ".jpeg")
                {
                    parent.Add(Text("[图片格式不支持] " + path, false));
                    return;
                }
                texture = new Texture2D(2, 2) { hideFlags = HideFlags.HideAndDontSave };
                if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(path)))
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                    parent.Add(Text("[图片读取失败] " + path, false));
                    return;
                }
                ownedImages.Add(texture);
            }
            var image = new Image { image = texture, scaleMode = ScaleMode.ScaleToFit, tooltip = alt };
            image.style.alignSelf = Align.FlexStart;
            image.style.marginBottom = 10;
            Texture2D source = texture;
            EventCallback<GeometryChangedEvent> resize = _ =>
            {
                if (source == null) return;
                float width = Mathf.Min(source.width, parent.contentRect.width);
                if (width <= 0) return;
                image.style.width = width;
                image.style.height = width * source.height / source.width;
            };
            parent.RegisterCallback(resize);
            imageLayouts.Add((parent, resize));
            parent.Add(image);
        }

        internal static string ResolveAssetPath(string document, string target)
        {
            try
            {
                string decoded = Uri.UnescapeDataString(target).Replace('\\', '/');
                if (Uri.TryCreate(decoded, UriKind.Absolute, out Uri uri)) return null;
                string absolute = Path.GetFullPath(decoded.StartsWith("Assets/", StringComparison.Ordinal)
                    ? decoded : Path.Combine(Path.GetDirectoryName(document) ?? "", decoded));
                string assets = Path.GetFullPath("Assets") + Path.DirectorySeparatorChar;
                if (!absolute.StartsWith(assets, StringComparison.OrdinalIgnoreCase)) return null;
                return "Assets/" + absolute.Substring(assets.Length).Replace('\\', '/');
            }
            catch (ArgumentException) { return null; }
            catch (NotSupportedException) { return null; }
        }

        public void Dispose()
        {
            foreach (var layout in imageLayouts) layout.parent.UnregisterCallback(layout.callback);
            imageLayouts.Clear();
            foreach (Texture2D image in ownedImages) if (image != null) UnityEngine.Object.DestroyImmediate(image);
            ownedImages.Clear();
        }
    }
}
