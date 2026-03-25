using Bunit;
using SamurAICouncil.Web.Components.Shared;

namespace SamurAICouncil.Web.Tests.Components;

[TestClass]
public class MarkdownRendererTests : BunitTestBase
{
    #region Test Infrastructure (4.1)

    [TestMethod]
    public void MarkdownRenderer_WithNullContent_RendersEmptyDiv()
    {
        var cut = Render<MarkdownRenderer>(parameters => parameters
            .Add(p => p.Content, null));

        var div = cut.Find(".markdown-content");
        Assert.IsNotNull(div);
        Assert.AreEqual(string.Empty, div.InnerHtml.Trim());
    }

    [TestMethod]
    public void MarkdownRenderer_WithEmptyContent_RendersEmptyDiv()
    {
        var cut = Render<MarkdownRenderer>(parameters => parameters
            .Add(p => p.Content, ""));

        var div = cut.Find(".markdown-content");
        Assert.IsNotNull(div);
        Assert.AreEqual(string.Empty, div.InnerHtml.Trim());
    }

    [TestMethod]
    public void MarkdownRenderer_HasMarkdownContentClass()
    {
        var cut = Render<MarkdownRenderer>(parameters => parameters
            .Add(p => p.Content, "Hello"));

        var div = cut.Find(".markdown-content");
        Assert.IsNotNull(div);
    }

    #endregion

    #region Code Block Style Tests (4.2)

    [TestMethod]
    public void CodeBlock_RendersPreAndCodeElements()
    {
        var cut = Render<MarkdownRenderer>(parameters => parameters
            .Add(p => p.Content, "```python\nprint('hello')\n```"));

        Assert.IsTrue(cut.Markup.Contains("<pre>"));
        Assert.IsTrue(cut.Markup.Contains("<code"));
    }

    [TestMethod]
    public void CodeBlock_HasLanguageClass()
    {
        var cut = Render<MarkdownRenderer>(parameters => parameters
            .Add(p => p.Content, "```python\nprint('hello')\n```"));

        // Markdig adds language class to code element
        Assert.IsTrue(cut.Markup.Contains("language-python"));
    }

    [TestMethod]
    public void CodeBlock_PreservesCodeContent()
    {
        var cut = Render<MarkdownRenderer>(parameters => parameters
            .Add(p => p.Content, "```javascript\nconst x = 42;\n```"));

        Assert.IsTrue(cut.Markup.Contains("const x = 42;"));
    }

    [TestMethod]
    public void CodeBlock_WithoutLanguage_StillRendersPreCode()
    {
        var cut = Render<MarkdownRenderer>(parameters => parameters
            .Add(p => p.Content, "```\nplain code\n```"));

        Assert.IsTrue(cut.Markup.Contains("<pre>"));
        Assert.IsTrue(cut.Markup.Contains("plain code"));
    }

    [TestMethod]
    public void InlineCode_RendersCodeElement()
    {
        var cut = Render<MarkdownRenderer>(parameters => parameters
            .Add(p => p.Content, "Use `console.log()` to debug"));

        // Should have inline code wrapped in <code> but not <pre>
        var html = cut.Markup;
        Assert.IsTrue(html.Contains("<code>console.log()</code>"));
        // The code should not be wrapped in pre (inline)
        Assert.IsFalse(html.Contains("<pre><code>console.log()</code>"));
    }

    [TestMethod]
    public void CodeBlock_MultipleLanguages_EachHasCorrectClass()
    {
        var markdown = @"
```csharp
var x = 1;
```

```typescript
const y = 2;
```
";
        var cut = Render<MarkdownRenderer>(parameters => parameters
            .Add(p => p.Content, markdown));

        Assert.IsTrue(cut.Markup.Contains("language-csharp"));
        Assert.IsTrue(cut.Markup.Contains("language-typescript"));
    }

    #endregion

    #region Typography Style Tests (4.3)

    [TestMethod]
    public void Heading1_RendersH1Element()
    {
        var cut = Render<MarkdownRenderer>(parameters => parameters
            .Add(p => p.Content, "# Heading 1"));

        var h1 = cut.Find("h1");
        Assert.IsNotNull(h1);
        Assert.AreEqual("Heading 1", h1.TextContent);
    }

    [TestMethod]
    public void Heading2_RendersH2Element()
    {
        var cut = Render<MarkdownRenderer>(parameters => parameters
            .Add(p => p.Content, "## Heading 2"));

        var h2 = cut.Find("h2");
        Assert.IsNotNull(h2);
        Assert.AreEqual("Heading 2", h2.TextContent);
    }

    [TestMethod]
    public void Heading3_RendersH3Element()
    {
        var cut = Render<MarkdownRenderer>(parameters => parameters
            .Add(p => p.Content, "### Heading 3"));

        var h3 = cut.Find("h3");
        Assert.IsNotNull(h3);
    }

    [TestMethod]
    public void AllHeadingLevels_RenderCorrectly()
    {
        var markdown = @"
# H1
## H2
### H3
#### H4
##### H5
###### H6
";
        var cut = Render<MarkdownRenderer>(parameters => parameters
            .Add(p => p.Content, markdown));

        Assert.IsNotNull(cut.Find("h1"));
        Assert.IsNotNull(cut.Find("h2"));
        Assert.IsNotNull(cut.Find("h3"));
        Assert.IsNotNull(cut.Find("h4"));
        Assert.IsNotNull(cut.Find("h5"));
        Assert.IsNotNull(cut.Find("h6"));
    }

    [TestMethod]
    public void Paragraph_RendersPElement()
    {
        var cut = Render<MarkdownRenderer>(parameters => parameters
            .Add(p => p.Content, "This is a paragraph."));

        var para = cut.Find("p");
        Assert.IsNotNull(para);
        Assert.AreEqual("This is a paragraph.", para.TextContent);
    }

    [TestMethod]
    public void MultipleParagraphs_RenderSeparatePElements()
    {
        var cut = Render<MarkdownRenderer>(parameters => parameters
            .Add(p => p.Content, "Paragraph 1.\n\nParagraph 2."));

        var paragraphs = cut.FindAll("p");
        Assert.AreEqual(2, paragraphs.Count);
    }

    [TestMethod]
    public void Bold_RendersStrongElement()
    {
        var cut = Render<MarkdownRenderer>(parameters => parameters
            .Add(p => p.Content, "This is **bold** text."));

        var strong = cut.Find("strong");
        Assert.IsNotNull(strong);
        Assert.AreEqual("bold", strong.TextContent);
    }

    [TestMethod]
    public void Italic_RendersEmElement()
    {
        var cut = Render<MarkdownRenderer>(parameters => parameters
            .Add(p => p.Content, "This is *italic* text."));

        var em = cut.Find("em");
        Assert.IsNotNull(em);
        Assert.AreEqual("italic", em.TextContent);
    }

    [TestMethod]
    public void BoldItalic_RendersBothElements()
    {
        var cut = Render<MarkdownRenderer>(parameters => parameters
            .Add(p => p.Content, "This is ***bold and italic*** text."));

        var strong = cut.Find("strong");
        var em = cut.Find("em");
        Assert.IsNotNull(strong);
        Assert.IsNotNull(em);
    }

    [TestMethod]
    public void Strikethrough_RendersDelElement()
    {
        var cut = Render<MarkdownRenderer>(parameters => parameters
            .Add(p => p.Content, "This is ~~strikethrough~~ text."));

        var del = cut.Find("del");
        Assert.IsNotNull(del);
        Assert.AreEqual("strikethrough", del.TextContent);
    }

    #endregion

    #region List Style Tests (4.4)

    [TestMethod]
    public void UnorderedList_RendersUlElement()
    {
        var cut = Render<MarkdownRenderer>(parameters => parameters
            .Add(p => p.Content, "- Item 1\n- Item 2\n- Item 3"));

        var ul = cut.Find("ul");
        Assert.IsNotNull(ul);
    }

    [TestMethod]
    public void UnorderedList_RendersAllItems()
    {
        var cut = Render<MarkdownRenderer>(parameters => parameters
            .Add(p => p.Content, "- Item 1\n- Item 2\n- Item 3"));

        var items = cut.FindAll("li");
        Assert.AreEqual(3, items.Count);
        Assert.AreEqual("Item 1", items[0].TextContent);
        Assert.AreEqual("Item 2", items[1].TextContent);
        Assert.AreEqual("Item 3", items[2].TextContent);
    }

    [TestMethod]
    public void OrderedList_RendersOlElement()
    {
        var cut = Render<MarkdownRenderer>(parameters => parameters
            .Add(p => p.Content, "1. First\n2. Second\n3. Third"));

        var ol = cut.Find("ol");
        Assert.IsNotNull(ol);
    }

    [TestMethod]
    public void OrderedList_RendersAllItems()
    {
        var cut = Render<MarkdownRenderer>(parameters => parameters
            .Add(p => p.Content, "1. First\n2. Second\n3. Third"));

        var items = cut.FindAll("li");
        Assert.AreEqual(3, items.Count);
    }

    [TestMethod]
    public void NestedUnorderedList_RendersNestedUlElements()
    {
        var markdown = @"- Parent 1
  - Child 1
  - Child 2
- Parent 2";

        var cut = Render<MarkdownRenderer>(parameters => parameters
            .Add(p => p.Content, markdown));

        var ulElements = cut.FindAll("ul");
        Assert.IsTrue(ulElements.Count >= 2, "Should have at least 2 ul elements (parent and nested)");
    }

    [TestMethod]
    public void NestedOrderedList_RendersNestedOlElements()
    {
        var markdown = @"1. Parent 1
   1. Child 1
   2. Child 2
2. Parent 2";

        var cut = Render<MarkdownRenderer>(parameters => parameters
            .Add(p => p.Content, markdown));

        var olElements = cut.FindAll("ol");
        Assert.IsTrue(olElements.Count >= 2, "Should have at least 2 ol elements (parent and nested)");
    }

    [TestMethod]
    public void MixedNestedLists_RenderCorrectly()
    {
        var markdown = @"1. Ordered item
   - Unordered child
   - Another unordered
2. Second ordered";

        var cut = Render<MarkdownRenderer>(parameters => parameters
            .Add(p => p.Content, markdown));

        Assert.IsNotNull(cut.Find("ol"));
        Assert.IsNotNull(cut.Find("ul"));
    }

    [TestMethod]
    public void TaskList_RendersCheckboxes()
    {
        var markdown = @"- [ ] Todo item
- [x] Completed item";

        var cut = Render<MarkdownRenderer>(parameters => parameters
            .Add(p => p.Content, markdown));

        var checkboxes = cut.FindAll("input[type='checkbox']");
        Assert.AreEqual(2, checkboxes.Count);
    }

    #endregion

    #region Table Style Tests (4.5)

    [TestMethod]
    public void Table_RendersTableElement()
    {
        var markdown = @"| Header 1 | Header 2 |
|----------|----------|
| Cell 1   | Cell 2   |";

        var cut = Render<MarkdownRenderer>(parameters => parameters
            .Add(p => p.Content, markdown));

        var table = cut.Find("table");
        Assert.IsNotNull(table);
    }

    [TestMethod]
    public void Table_RendersTheadAndTbody()
    {
        var markdown = @"| Header 1 | Header 2 |
|----------|----------|
| Cell 1   | Cell 2   |";

        var cut = Render<MarkdownRenderer>(parameters => parameters
            .Add(p => p.Content, markdown));

        var thead = cut.Find("thead");
        var tbody = cut.Find("tbody");
        Assert.IsNotNull(thead);
        Assert.IsNotNull(tbody);
    }

    [TestMethod]
    public void Table_RendersThElements()
    {
        var markdown = @"| Header 1 | Header 2 |
|----------|----------|
| Cell 1   | Cell 2   |";

        var cut = Render<MarkdownRenderer>(parameters => parameters
            .Add(p => p.Content, markdown));

        var headers = cut.FindAll("th");
        Assert.AreEqual(2, headers.Count);
        Assert.AreEqual("Header 1", headers[0].TextContent);
        Assert.AreEqual("Header 2", headers[1].TextContent);
    }

    [TestMethod]
    public void Table_RendersTdElements()
    {
        var markdown = @"| Header 1 | Header 2 |
|----------|----------|
| Cell 1   | Cell 2   |
| Cell 3   | Cell 4   |";

        var cut = Render<MarkdownRenderer>(parameters => parameters
            .Add(p => p.Content, markdown));

        var cells = cut.FindAll("td");
        Assert.AreEqual(4, cells.Count);
    }

    [TestMethod]
    public void Table_MultipleRows_RendersTrElements()
    {
        var markdown = @"| Col 1 | Col 2 |
|-------|-------|
| A     | B     |
| C     | D     |
| E     | F     |";

        var cut = Render<MarkdownRenderer>(parameters => parameters
            .Add(p => p.Content, markdown));

        // 1 header row + 3 body rows = 4 tr elements
        var rows = cut.FindAll("tr");
        Assert.AreEqual(4, rows.Count);
    }

    [TestMethod]
    public void Table_WithAlignment_RendersCorrectly()
    {
        var markdown = @"| Left | Center | Right |
|:-----|:------:|------:|
| L    | C      | R     |";

        var cut = Render<MarkdownRenderer>(parameters => parameters
            .Add(p => p.Content, markdown));

        var table = cut.Find("table");
        Assert.IsNotNull(table);
        // Alignment is typically added as style or class by Markdig
    }

    #endregion

    #region Other Element Tests (4.6)

    [TestMethod]
    public void Blockquote_RendersBlockquoteElement()
    {
        var cut = Render<MarkdownRenderer>(parameters => parameters
            .Add(p => p.Content, "> This is a quote"));

        var blockquote = cut.Find("blockquote");
        Assert.IsNotNull(blockquote);
        Assert.IsTrue(blockquote.TextContent.Contains("This is a quote"));
    }

    [TestMethod]
    public void NestedBlockquote_RendersNestedElements()
    {
        var markdown = @"> Level 1
>> Level 2";

        var cut = Render<MarkdownRenderer>(parameters => parameters
            .Add(p => p.Content, markdown));

        var blockquotes = cut.FindAll("blockquote");
        Assert.IsTrue(blockquotes.Count >= 2);
    }

    [TestMethod]
    public void HorizontalRule_RendersHrElement()
    {
        var cut = Render<MarkdownRenderer>(parameters => parameters
            .Add(p => p.Content, "Above\n\n---\n\nBelow"));

        var hr = cut.Find("hr");
        Assert.IsNotNull(hr);
    }

    [TestMethod]
    public void Link_RendersAnchorElement()
    {
        var cut = Render<MarkdownRenderer>(parameters => parameters
            .Add(p => p.Content, "[Click here](https://example.com)"));

        var anchor = cut.Find("a");
        Assert.IsNotNull(anchor);
        Assert.AreEqual("https://example.com", anchor.GetAttribute("href"));
        Assert.AreEqual("Click here", anchor.TextContent);
    }

    [TestMethod]
    public void Link_WithTitle_HasTitleAttribute()
    {
        var cut = Render<MarkdownRenderer>(parameters => parameters
            .Add(p => p.Content, "[Click](https://example.com \"Link title\")"));

        var anchor = cut.Find("a");
        Assert.IsNotNull(anchor);
        Assert.AreEqual("Link title", anchor.GetAttribute("title"));
    }

    [TestMethod]
    public void Image_RendersImgElement()
    {
        var cut = Render<MarkdownRenderer>(parameters => parameters
            .Add(p => p.Content, "![Alt text](https://example.com/image.png)"));

        var img = cut.Find("img");
        Assert.IsNotNull(img);
        Assert.AreEqual("https://example.com/image.png", img.GetAttribute("src"));
        Assert.AreEqual("Alt text", img.GetAttribute("alt"));
    }

    [TestMethod]
    public void Image_WithTitle_HasTitleAttribute()
    {
        var cut = Render<MarkdownRenderer>(parameters => parameters
            .Add(p => p.Content, "![Alt](https://example.com/img.png \"Image title\")"));

        var img = cut.Find("img");
        Assert.IsNotNull(img);
        Assert.AreEqual("Image title", img.GetAttribute("title"));
    }

    [TestMethod]
    public void AutoLink_RendersAnchorElement()
    {
        var cut = Render<MarkdownRenderer>(parameters => parameters
            .Add(p => p.Content, "<https://example.com>"));

        var anchor = cut.Find("a");
        Assert.IsNotNull(anchor);
        Assert.AreEqual("https://example.com", anchor.GetAttribute("href"));
    }

    #endregion

    #region Advanced Markdown Features

    [TestMethod]
    public void FootnoteReference_RendersCorrectly()
    {
        var markdown = @"Here is a footnote[^1].

[^1]: This is the footnote content.";

        var cut = Render<MarkdownRenderer>(parameters => parameters
            .Add(p => p.Content, markdown));

        // Markdig with advanced extensions should handle footnotes
        Assert.IsTrue(cut.Markup.Contains("footnote") || cut.Markup.Contains("This is the footnote content"));
    }

    [TestMethod]
    public void ComplexMarkdown_RendersAllElements()
    {
        var markdown = @"# Main Title

This is a **bold** and *italic* paragraph with `inline code`.

## Code Example

```csharp
public class Test
{
    public void Method() { }
}
```

### Lists

- Bullet 1
- Bullet 2
  - Nested bullet

1. Numbered 1
2. Numbered 2

> This is a blockquote

| Column A | Column B |
|----------|----------|
| Value 1  | Value 2  |

---

[Link](https://example.com)
";

        var cut = Render<MarkdownRenderer>(parameters => parameters
            .Add(p => p.Content, markdown));

        // Verify all major elements are present
        Assert.IsNotNull(cut.Find("h1"));
        Assert.IsNotNull(cut.Find("h2"));
        Assert.IsNotNull(cut.Find("h3"));
        Assert.IsNotNull(cut.Find("p"));
        Assert.IsNotNull(cut.Find("strong"));
        Assert.IsNotNull(cut.Find("em"));
        Assert.IsNotNull(cut.Find("code"));
        Assert.IsNotNull(cut.Find("pre"));
        Assert.IsNotNull(cut.Find("ul"));
        Assert.IsNotNull(cut.Find("ol"));
        Assert.IsNotNull(cut.Find("blockquote"));
        Assert.IsNotNull(cut.Find("table"));
        Assert.IsNotNull(cut.Find("hr"));
        Assert.IsNotNull(cut.Find("a"));
    }

    #endregion

    #region Edge Cases

    [TestMethod]
    public void WhitespaceOnlyContent_RendersEmpty()
    {
        var cut = Render<MarkdownRenderer>(parameters => parameters
            .Add(p => p.Content, "   \n\n   "));

        var div = cut.Find(".markdown-content");
        Assert.AreEqual(string.Empty, div.InnerHtml.Trim());
    }

    [TestMethod]
    public void SpecialCharacters_AreEscaped()
    {
        var cut = Render<MarkdownRenderer>(parameters => parameters
            .Add(p => p.Content, "Use `<div>` and `&amp;` in HTML."));

        // Verify that HTML entities are properly handled
        Assert.IsTrue(cut.Markup.Contains("&lt;div&gt;") || cut.Markup.Contains("<code>&lt;div&gt;</code>"));
    }

    [TestMethod]
    public void DifferentContent_RendersDifferently()
    {
        // Verify that different content produces different output
        var cut1 = Render<MarkdownRenderer>(parameters => parameters
            .Add(p => p.Content, "First content"));

        var cut2 = Render<MarkdownRenderer>(parameters => parameters
            .Add(p => p.Content, "Second content"));

        Assert.IsTrue(cut1.Markup.Contains("First content"));
        Assert.IsTrue(cut2.Markup.Contains("Second content"));
        Assert.IsFalse(cut1.Markup.Contains("Second content"));
    }

    #endregion
}
