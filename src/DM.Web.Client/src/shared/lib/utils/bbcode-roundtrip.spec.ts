/**
 * @vitest-environment jsdom
 *
 * BBCode Round-Trip Integration Tests
 *
 * Tests the COMPLETE pipeline of the visual mode:
 * BBCode → bbcodeToHtml → Tiptap setContent → Tiptap getHTML → htmlToBbcode → BBCode
 *
 * The editor is built from the very extensions and parse options BBCodeEditor
 * uses (createEditorExtensions, EDITOR_PARSE_OPTIONS), so a change there is a
 * change here.
 */

import { describe, it, expect, beforeEach, afterEach } from "vitest";
import { Editor } from "@tiptap/core";
import { bbcodeToHtml, htmlToBbcode, type BBCodeContext } from "./bbcode";
import {
  createEditorExtensions,
  EDITOR_PARSE_OPTIONS,
} from "@/shared/ui/BBCodeEditor/lib";

function createProductionEditor() {
  return new Editor({ extensions: createEditorExtensions(), content: "" });
}

describe("BBCode Round-Trip Integration", () => {
  let editor: Editor;

  beforeEach(() => {
    editor = createProductionEditor();
  });

  afterEach(() => {
    editor.destroy();
  });

  /** Follow a text through the visual mode and back. */
  function roundTrip(inputBbcode: string, context?: BBCodeContext) {
    const html = bbcodeToHtml(inputBbcode, { context });
    editor.commands.setContent(html, {
      emitUpdate: false,
      parseOptions: EDITOR_PARSE_OPTIONS,
    });
    const editorHtml = editor.getHTML();
    const outputBbcode = htmlToBbcode(editorHtml);
    return { html, editorHtml, outputBbcode };
  }

  describe("Image and link", () => {
    it("keeps [img]URL[/img] and [link]URL[/link] with a blank line between", () => {
      const input = `[img]https://i.gyazo.com/72303c63ffdb7d4d21c9a2b8f6fff02b.jpg[/img]

[link]https://i.gyazo.com/72303c63ffdb7d4d21c9a2b8f6fff02b.jpg[/link]`;

      const { editorHtml, outputBbcode } = roundTrip(input);

      expect(editorHtml).toContain("<img");
      expect(outputBbcode).toBe(input);
    });

    it("keeps [img]URL[/img]", () => {
      const input = "[img]https://example.com/image.png[/img]";
      expect(roundTrip(input).outputBbcode).toBe(input);
    });

    it("keeps an image with text around it", () => {
      const input = "Before [img]https://example.com/image.png[/img] After";
      expect(roundTrip(input).outputBbcode).toBe(input);
    });

    it("keeps an image and a link side by side", () => {
      const input =
        "[img]https://img.com/a.png[/img][link]https://link.com[/link]";
      expect(roundTrip(input).outputBbcode).toBe(input);
    });

    // The window of the visual mode asked for it, and the image was saved as
    // a bare [img]: the size went nowhere.
    it.each([
      '[img width="300"]https://example.com/a.png[/img]',
      '[img height="200"]https://example.com/a.png[/img]',
      '[img width="300" height="200"]https://example.com/a.png[/img]',
    ])("keeps the size: %s", (input) => {
      expect(roundTrip(input).outputBbcode).toBe(input);
    });

    // An image has no alt, and a tag with one is the text it was typed as: the
    // visual mode shows it as text and gives it back as typed, for the author
    // to see and take out.
    it.each([
      '[img alt="кот на окне"]https://example.com/a.png[/img]',
      '[img width="300" alt="кот"]https://example.com/a.png[/img]',
    ])("keeps an image with an alt as the text it is: %s", (input) => {
      const { editorHtml, outputBbcode } = roundTrip(input);
      expect(editorHtml).not.toContain("<img");
      expect(outputBbcode).toBe(input);
    });

    it.each([
      "[link]https://example.com[/link]",
      '[link text="Главная"]https://example.com/[/link]',
      '[link text=""]https://example.com/[/link]',
    ])("keeps a link: %s", (input) => {
      expect(roundTrip(input).outputBbcode).toBe(input);
    });

    // The server prints a tag whose address it refuses as typed, and so does
    // the preview. The visual mode used to be handed an empty mark for it and
    // saved nothing in its place.
    it.each([
      "[img]javascript:alert(1)[/img]",
      "до [img]http://192.168.0.1/a.png[/img] после",
      '[link text="Почта"]mailto:a@example.com[/link]',
    ])("keeps a tag whose address the server refuses: %s", (input) => {
      expect(roundTrip(input).outputBbcode).toBe(input);
    });

    // Without a node of its own the mention was read as the text "@Вася".
    it("keeps a mention", () => {
      const input = "Привет, [mention]Вася[/mention]!";
      const { editorHtml, outputBbcode } = roundTrip(input);
      expect(editorHtml).toContain('data-bb-tag="mention"');
      expect(outputBbcode).toBe(input);
    });
  });

  // The visual mode is the default, so this was every edit of a post with
  // code in it: the parser folded the line breaks and the indentation into
  // single spaces, and the code was saved as one line.
  describe("Code and noparse", () => {
    it.each([
      "[code]строка1\nстрока2\n    отступ[/code]",
      "[code]\nfunction f() {\n  return 1;\n}\n[/code]",
      "[noparse]раз\n  [b]два[/b]\nтри[/noparse]",
      "Текст [code]a  b[/code] и дальше",
    ])("keep their lines and spaces: %j", (input) => {
      expect(roundTrip(input).outputBbcode).toBe(input);
    });

    // [code] changes the font and nothing else, so the markup inside it works
    // in the visual mode as well. The code mark of the kit excluded every
    // other mark, and the bold inside it was gone at the next save; the
    // noparse mark excluded the code mark, and the way DM2 writes an example
    // in a monospace font lost its [code].
    it.each([
      [
        "[code][b]жирный[/b] и [i]курсив[/i][/code]",
        "<code><strong>жирный</strong> и <em>курсив</em></code>",
      ],
      [
        "[code]раз [u]два[/u] [strike]три[/strike][/code]",
        "<code>раз <u>два</u> <s>три</s></code>",
      ],
      [
        "[code][noparse][b]пример[/b][/noparse][/code]",
        '<code><span data-bb-tag="noparse" class="bb-noparse">[b]пример[/b]</span></code>',
      ],
      [
        "[code]раз [noparse][/code][/noparse] два[/code]",
        '<code>раз <span data-bb-tag="noparse" class="bb-noparse">[/code]</span> два</code>',
      ],
      [
        "[b][noparse]пример[/noparse][/b]",
        '<strong><span data-bb-tag="noparse" class="bb-noparse">пример</span></strong>',
      ],
      [
        "[code]адрес [link]https://example.com/[/link][/code]",
        '<code>адрес <a target="_blank" rel="noopener" href="https://example.com/"',
      ],
    ])("keep the markup inside [code]: %j", (input, drawn) => {
      const { editorHtml, outputBbcode } = roundTrip(input);
      expect(editorHtml).toContain(drawn);
      expect(outputBbcode).toBe(input);
    });
  });

  describe("Spoiler", () => {
    it.each([
      "[spoiler]текст[/spoiler]",
      '[spoiler text="Карта"]текст[/spoiler]',
      '[spoiler text=""]текст[/spoiler]',
    ])("keeps its caption: %s", (input) => {
      expect(roundTrip(input).outputBbcode).toBe(input);
    });

    // The block keeps the spelling it was typed with, both of its tags.
    it("keeps a spelling in capitals", () => {
      const input = '[Spoiler text="Карта"]текст[/SPOILER]';
      const { editorHtml, outputBbcode } = roundTrip(input);
      expect(editorHtml).toContain('data-bb-text="Карта"');
      expect(outputBbcode).toBe(input);
    });

    // The lines around the line of underscores were folded into the block,
    // and the line went with them.
    it("keeps the lines it holds", () => {
      const input = "[spoiler]\n___\n[/spoiler]";
      expect(roundTrip(input).outputBbcode).toBe(input);
    });
  });

  describe("Lines", () => {
    // Every line was trimmed, so " ___" came back as "___", which the server
    // draws as a horizontal line.
    it("keep the spaces they start with", () => {
      const input = "раз\n ___\nдва";
      expect(roundTrip(input).outputBbcode).toBe(input);
    });

    it("keep the underscores and the dashes as typed", () => {
      const input = "раз -- два --- три\n___\nчетыре";
      expect(roundTrip(input).outputBbcode).toBe(input);
    });

    it("keep any number of blank lines", () => {
      const input = "раз\n\n\n\nдва";
      expect(roundTrip(input).outputBbcode).toBe(input);
    });

    it("keep a tab three spaces wide", () => {
      const { editorHtml, outputBbcode } = roundTrip("раз[tab]два");
      expect(editorHtml).toContain(">&nbsp;&nbsp;&nbsp;</span>");
      expect(outputBbcode).toBe("раз[tab]два");
    });
  });

  // A trip through the visual mode is no reason to rewrite what the author
  // typed: a name in capitals comes back in capitals, as it does from the
  // BBCode mode. It used to come back in small letters, every tag but the
  // spoiler, the loose item and the hidden block.
  it.each([
    "[B]раз[/B] [I]два[/i]",
    "[U]раз[/u] [Strike]два[/STRIKE]",
    "[CODE][b]раз[/b][/Code]",
    "[NOPARSE][b]раз[/b][/NoParse]",
    "[QUOTE]раз[/Quote]",
    "[NSFW]раз[/nsfw]",
    "[UL][Li]раз[/LI][/ul]",
    "[OL][li]раз[/li][/OL]",
    "раз[TAB]два",
    "[b]раз[/B][/b]",
  ])("keeps a name as it was typed: %j", (input) => {
    expect(roundTrip(input).outputBbcode).toBe(input);
  });

  it("keeps [mod] in capitals where it is a tag", () => {
    expect(roundTrip("[MOD]раз[/Mod]", "common").outputBbcode).toBe(
      "[MOD]раз[/Mod]",
    );
  });

  // The visual mode puts a block on lines of its own. Where the author wrote
  // no line break at the edge of a block, the way back used to put one.
  it.each([
    "[quote]раз[/quote]два",
    "раз[spoiler]два[/spoiler]",
    "[quote][noparse]пример[/quote] хвост",
    "[nsfw]раз[/nsfw][quote]два[/quote]",
    "[ul][li]раз[/li][/ul]два",
    "[quote]раз[/quote]\n",
  ])("keeps a block glued to what is next to it: %j", (input) => {
    expect(roundTrip(input).outputBbcode).toBe(input);
  });

  // An item outside a list is drawn as a div, a li outside a list being no
  // HTML, and the visual mode used to read that div as bare paragraphs: the
  // [li] tags were gone at the next save. A list whose [ul] was never closed
  // prints the [ul] as text and keeps its items, so every such list lost them.
  describe("An item outside a list", () => {
    it.each([
      ["[ul][li]раз[/li][li]два[/li]", "[ul][li]раз[/li][li]два[/li]"],
      ["[li]раз[/li]", "[li]раз[/li]"],
      ["[LI]раз[/Li]", "[LI]раз[/Li]"],
      ["[quote][li]раз[/li][/quote]", "[quote][li]раз[/li][/quote]"],
    ])("keeps its tags: %j", (input, output) => {
      const { editorHtml, outputBbcode } = roundTrip(input);
      expect(editorHtml).toContain('data-bb-tag="li"');
      expect(outputBbcode).toBe(output);
    });
  });

  describe("Private", () => {
    // A name in capitals is the same name: the block keeps its addressee and
    // its end, and both of its tags come back as typed.
    it("reads a block in capitals as the block and keeps its spelling", () => {
      const input = '[PRIVATE="Вася"]секрет[/Private] хвост';
      const { editorHtml, outputBbcode } = roundTrip(input, "post");
      expect(editorHtml).toContain('data-bb-character="Вася"');
      expect(editorHtml).not.toContain("data-bb-unclosed");
      expect(outputBbcode).toBe(input);
    });

    it.each([
      '[private="Вася, Петя"]секрет[/private]',
      "[private]секрет[/private]",
      '[private="Вася"]раз\nдва[/private]',
      // Its closing tag in capitals ends it, and comes back as typed.
      "[private]секрет[/PRIVATE] хвост",
      // A spelling the grammar does not know stays a hidden block and comes
      // back as typed, so the save refuses it and names the place.
      // (A block with nothing in it has no text for the mark to stand on, and
      // the visual mode drops it; there is no secret in it to publish.)
      "[private=Вася]секрет[/private]",
      '[private to="Вася"]секрет[/private]',
    ])("keeps the block and its spelling: %j", (input) => {
      const { editorHtml, outputBbcode } = roundTrip(input, "post");
      expect(editorHtml).toContain('data-bb-tag="private"');
      expect(outputBbcode).toBe(input);
    });
  });

  // The visual mode is the default one, and a hidden block that lost its
  // wrapper on the way through it is published to the room on the next save:
  // the save path accepts the text, because nothing in it is unbalanced any
  // more. The code mark and the noparse mark used to exclude every other mark,
  // and the link wrote itself from its attributes with nothing of what it held,
  // so a [private] around any of the three came off it.
  describe("Private around other markup", () => {
    /** The text with every [private] block taken out, as a stranger reads it. */
    function outsideHidden(bbcode: string): string {
      let out = "";
      let at = 0;
      for (;;) {
        const open = bbcode.indexOf("[private", at);
        if (open < 0) return out + bbcode.slice(at);
        out += bbcode.slice(at, open);
        const close = bbcode.indexOf("[/private]", open);
        if (close < 0) return out;
        at = close + "[/private]".length;
      }
    }

    it.each([
      '[private="A"][code]секрет[/code][/private]',
      '[private="A"]до [code]секрет[/code] после[/private]',
      '[private="A"]до [code]раз\nсекрет\n  два[/code] после[/private]',
      '[private="A"][noparse]секрет[/noparse][/private]',
      '[private="A"]до [noparse][b]секрет[/b][/noparse] после[/private]',
      '[private="A"]до [link]https://example.com/секрет[/link] после[/private]',
      '[private="A"]до [link text="секрет"]https://example.com/x[/link] после[/private]',
      '[private="A"][link text="секрет"]https://example.com/x[/link][/private]',
      "[private]до [code]секрет[/code] после[/private]",
      "[private]до [link]https://example.com/секрет[/link] после[/private]",
      '[private="A"]до [img]https://example.com/секрет.png[/img] после[/private]',
      '[private="A"]до [mention]секрет[/mention] после[/private]',
      '[private="A"]до [b]секрет[/b] после[/private]',
      '[private="A"]раз\n[code]секрет[/code]\nдва[/private]',
    ])("keeps the block around it: %j", (input) => {
      const { outputBbcode } = roundTrip(input, "post");
      expect(outsideHidden(outputBbcode)).not.toContain("секрет");
      expect(outputBbcode).toBe(input);
    });

    // A block inside another is read by those who read both. The visual mode
    // used to put the inner one beside the outer one, which the save takes,
    // and the text of the inner block went to its addressee.
    it.each([
      '[private]раз [private="Вася"]два[/private] три[/private]',
      '[private="Вася"]раз [private]два[/private][/private]',
      '[private="Вася"][private="Петя"]два[/private][/private]',
    ])("keeps a block inside another inside it: %j", (input) => {
      expect(roundTrip(input, "post").outputBbcode).toBe(input);
    });

    // A hidden block is a mark, and a mark used to stand on the text inside a
    // block and not on the block: the block came back outside the hidden one.
    // A spoiler written in a hidden block was saved as a spoiler every reader
    // saw, its caption included, and an empty quote as an empty frame on the
    // page of every reader; the text around the block came back as blocks of
    // its own, each with its line of recipients.
    it.each([
      '[private="A"]до [spoiler]секрет[/spoiler] после[/private]',
      '[private="A"]до [quote]секрет[/quote] после[/private]',
      '[private="A"]до [nsfw][code]секрет[/code][/nsfw] после[/private]',
      '[private="A"][spoiler text="секрет"]текст[/spoiler][/private]',
      '[private="A"][quote][/quote][/private]',
      "[private][ul][li]секрет[/li][/ul][/private]",
      '[private="A"]раз\n[quote]секрет[/quote]\nдва[/private]',
      '[quote][private="A"][spoiler]секрет[/spoiler][/private][/quote]',
    ])("keeps the block around a block: %j", (input) => {
      const { outputBbcode } = roundTrip(input, "post");
      expect(outsideHidden(outputBbcode)).not.toContain("секрет");
      expect(outputBbcode).toBe(input);
    });

    // A list of the visual mode holds items and nothing else, and it moved a
    // spoiler written straight in a list out of the list, and out of the
    // hidden block around the list with it.
    it("keeps a block written straight in a list inside the hidden block", () => {
      const input =
        '[private="A"][ul][spoiler text="секрет"]текст[/spoiler][/ul][/private]';
      const { outputBbcode } = roundTrip(input, "post");
      expect(outsideHidden(outputBbcode)).not.toContain("секрет");
      expect(outputBbcode).toBe(input);
    });

    // The hidden block is a block here, and the text is glued to it.
    it.each([
      'до[private="A"][quote]секрет[/quote][/private]после',
      'до [private="A"][spoiler]секрет[/spoiler][/private]\nпосле',
    ])("keeps the line breaks around a hidden block: %j", (input) => {
      expect(roundTrip(input, "post").outputBbcode).toBe(input);
    });
  });

  // A list that holds words or blocks besides its items. The visual mode used
  // to put the words into items of their own, so a reader saw a bullet where
  // the author wrote none, and moved a block out of the list; an empty list
  // came back with an empty item. Such a list is drawn as a block of what it
  // holds and comes back as typed. The spaces and line breaks between items
  // go, as the page does not show them either.
  describe("A list that holds more than items", () => {
    it.each([
      "[ol]раз[/ol]",
      "[ul]раз[li]два[/li]три[/ul]",
      "[ul][/ul]",
      "[ol]  [/ol]",
      "[ul][quote]цитата[/quote][li]раз[/li][/ul]",
      "[UL]раз[/Ul]",
    ])("comes back as typed: %j", (input) => {
      const { editorHtml, outputBbcode } = roundTrip(input);
      expect(editorHtml).toContain("data-bb-loose");
      expect(outputBbcode).toBe(input);
    });

    it("is a list when it holds items and the space between them", () => {
      const { editorHtml, outputBbcode } = roundTrip(
        "[ul]\n[li]раз[/li]\n[li]два[/li]\n[/ul]",
      );
      expect(editorHtml).toContain("<ul");
      expect(editorHtml).not.toContain("data-bb-loose");
      expect(outputBbcode).toBe("[ul][li]раз[/li][li]два[/li][/ul]");
    });
  });

  // A quote sets text apart, as in DM2: a block with the text in it and no
  // author line above, and the tag knows no attribute to name one with.
  describe("Quote", () => {
    it("is a block with nothing but the text in it, and comes back as [quote]", () => {
      const { editorHtml, outputBbcode } = roundTrip(
        "[quote]чужая реплика[/quote]",
      );

      expect(editorHtml).toContain(
        '<blockquote data-bb-tag="quote" class="bb-quote"><p>чужая реплика</p></blockquote>',
      );
      expect(outputBbcode).toBe("[quote]чужая реплика[/quote]");
    });

    it.each([
      "[quote=Вася]чужая реплика[/quote]",
      '[quote="Вася"]чужая реплика[/quote]',
    ])(
      "draws no author line for %s and hands back what was written",
      (input) => {
        const { editorHtml, outputBbcode } = roundTrip(input);

        expect(editorHtml).not.toContain("blockquote");
        expect(editorHtml).not.toContain("data-bb-author");
        expect(outputBbcode).toBe(input);
      },
    );

    it("drops an author attribute the visual mode is handed", () => {
      editor.commands.setContent(
        '<blockquote data-bb-tag="quote" data-bb-author="Вася"><p>чужая реплика</p></blockquote>',
        { emitUpdate: false },
      );

      expect(htmlToBbcode(editor.getHTML())).toBe(
        "[quote]чужая реплика[/quote]",
      );
    });
  });

  // The editor puts the text after an inner block in a paragraph of its own,
  // inside the outer block. A crossed pair moved that text out of it on save.
  describe("Blocks inside blocks", () => {
    it.each([
      "[spoiler][nsfw]раз[/nsfw]\nдва[/spoiler]",
      "[spoiler][quote]раз[/quote]\nдва[/spoiler]",
      "[quote][spoiler]раз[/spoiler]\nдва[/quote]",
      "раз\n[spoiler]два[/spoiler]\nтри",
    ])("come back nested: %j", (input) => {
      expect(roundTrip(input).outputBbcode).toBe(input);
    });
  });

  describe("htmlToBbcode output inspection", () => {
    it("should convert img HTML to BBCode", () => {
      const html =
        '<p><img src="https://example.com/image.png" data-bb-tag="img" class="bb-image" /></p>';
      expect(htmlToBbcode(html)).toBe(
        "[img]https://example.com/image.png[/img]",
      );
    });

    it("should convert img HTML without data-bb-tag to BBCode", () => {
      const html = '<p><img src="https://example.com/image.png" /></p>';
      expect(htmlToBbcode(html)).toBe(
        "[img]https://example.com/image.png[/img]",
      );
    });
  });
});
