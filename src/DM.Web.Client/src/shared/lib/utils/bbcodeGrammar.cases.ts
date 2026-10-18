/**
 * The BBCode grammar as a table: what an input is, on which surface, and what
 * the reader gets. One table for both sides.
 *
 * The rule itself is written down in docs/architecture/BBCODE_RENDERING.md,
 * section "Грамматика разметки". This file is its executable half: every row is
 * one input and its expected result, stated in a neutral form that neither
 * side's markup leaks into. The server holds its renderer, its save path and
 * its search projection to the rows; the client holds its editor converter and
 * its markup check to the same rows. A row that one side passes and the other
 * does not is the two grammars drifting apart, and that is what this table is
 * for.
 *
 * How the server reads it. The array below is strict JSON between the two
 * marker comments, one row per line: a test under test/ reads this file through
 * RepositoryLayout.Root, takes the text between the markers and deserialises it
 * with System.Text.Json, the way BbAuthorEditRoundTripShould reads
 * authorEditRoundTrip.spec.ts. Keys stay quoted and there is no trailing comma,
 * which is why the declaration carries prettier-ignore.
 *
 * Fields of a row:
 * - id: stable and unique, used as the test name on both sides.
 * - rule: which part of the rule the row holds, one letter per subsection of
 *   the document: A the DM2 tags, B the new forms, C how a value is written,
 *   D broken tags, E [private], F typography, and "___", which is text, G [cut],
 *   H [tip], the one tag of a tag description, and the spelling [tip:] that
 *   came before it, I images of the global chat, J what the author's editor gets
 *   back, K names in any case, L the cost of reading, N a tag left open and a
 *   closing tag with nothing to close, O the characters removed before
 *   reading, P [code] and [noparse] with and around other tags. The seed (M)
 *   has no rows here.
 * - input: the BBCode as the author typed it. A character the eye does not
 *   tell from another, or does not see at all, is written as a \u escape.
 * - surface: the BbSurface the result is for, or "any" when it is the same on
 *   all five surfaces of content. The sixth, Hint, is the description of a
 *   game tag, with a tag set of its own, [tip]; every row for it names it.
 * - audience: "display" when absent; "author_edit" rows state what the author's
 *   editor is given back, as one { source } piece.
 * - expect: the result, as a list of pieces (below). An empty list is nothing
 *   at all.
 * - broken: present when the editor's markup check has something to name in
 *   the input: a broken spelling, an address the page refuses, a tag left
 *   open, a closing tag with nothing to close, a hidden block inside another,
 *   or hiding markup on a surface that does not declare it. The value is the
 *   reason the check names, one of the list in BBCODE_RENDERING.md, section
 *   "Предупреждения редактора"; a row where it names more than one carries
 *   the one the row is about. Nothing inside a closed [noparse] is ever named.
 * - save: "refused" when every save path of that surface refuses the text.
 * - editor: true when the editor itself writes this spelling (its windows, its
 *   toolbar, its visual mode); the markup check must accept it as it stands.
 *
 * Pieces:
 * - { text }: text as the reader sees it, markup characters included. A line
 *   break stays "\n"; the surface decides how it is drawn. The characters that
 *   reorder text for display, U+202A..U+202E and U+2066..U+2069, and the C0 and
 *   C1 control characters other than tab, line feed and carriage return are
 *   never in it: both sides remove them before the text is read, so they can
 *   neither reverse what a reader sees nor split a tag name, and what stood
 *   around one is read joined.
 *   A block ends its line, so the one line break typed straight after its
 *   closing tag is in no piece: the text after [quote]...[/quote] and a line
 *   break starts on the line under the block, as the text over it ends on the
 *   line above (BBCODE_RENDERING.md, "Перевод строки после блока"). A second
 *   break is an empty line and stays "\n". A tag that stands in the line, [b]
 *   or [code], ends none, and the break after it stays; so does a hidden
 *   block, which is text in the line.
 * - { tag, params?, children? }: the tag drawn as markup. img carries src and
 *   whichever of width and height were given; link carries href and the text
 *   it shows; mention carries login; spoiler carries text when a caption was
 *   given. tip carries text and img, whichever it shows, and holds its word as
 *   one text. gate: "compact" on an image of the global chat: behind a spoiler in
 *   the compact layout, in plain view in the full one. code holds its children
 *   drawn as anywhere else, in a monospace font; noparse holds one text, as
 *   typed. A tag that holds hidden blocks a reader does not see and nothing
 *   else but white space is not drawn for that reader at all, as if it had not
 *   been written.
 * - { hidden: { to, seenBy }, typed?, children }: a [private] block. to is the
 *   list of addressee names; seenBy names who reads it: the author, the game
 *   leads (master and assistants), the owners of the addressed characters,
 *   and every reader of the room when the post or the room shares private
 *   text. A reader who sees the block gets with it who reads it, for the
 *   tooltip over it and not as text of the line: "DungeonMaster, Assistant",
 *   the leads first whoever the block names, then ", " and each name of to
 *   that is not one of the two (a name is a lead in any case). An empty
 *   seenBy is nobody, the author included: hiding markup on a surface that
 *   does not declare it, which the page, the plain text and the search all
 *   erase.
 *   A reader who does not see a block that stands on a line of its own -
 *   between two line breaks, or at the start or the end of the text next to
 *   one - does not get that line either: it goes with one of the breaks
 *   around it, as if the author had not typed it. A line with anything else
 *   on it stays, a space included, and inside a tag only a line between two
 *   breaks is one, the tag itself possibly standing in the middle of a line.
 *   The editor shows such a block as the text it was typed: a row where it
 *   stands at the top holds nothing else but plain text around it, and the
 *   preview is the whole input; where it stands inside a tag, typed gives that
 *   text, and the preview has it in the block's place.
 * - { dash: "short" | "long" }: the typographic dash, U+2013 or U+2014.
 * - { source }: author_edit only, the BBCode the editor gets back.
 *
 * What each side checks.
 * - Server, per row and per surface: the display render matches expect, for a
 *   stranger and, where a hidden piece is, for every kind of reader a hidden
 *   block can have (its author, a lead, an addressee, a reader let in by either
 *   setting), each of whom sees the block only when its seenBy names them, with
 *   its recipients on it; the plain-text render and the search
 *   projection never carry the children of a hidden piece; the save validator
 *   of the surface refuses exactly the rows marked save: "refused", naming the
 *   place and the correct spelling.
 * - Client, per row in the editor context of its surface (GamePost: post,
 *   Comment: common, GlobalChatMessage: message, DirectMessage: direct,
 *   Profile: info, Hint: hint): the editor preview draws tags, broken spellings and hidden
 *   blocks as expect says. It shows typography as typed, a chat image without
 *   its spoiler, and a hidden block nobody reads as typed (see the hidden
 *   piece above): the editor holds the author's own text, and the save path
 *   refuses that block with a reason rather than hiding it from its author.
 *   The hint's preview is the reader's markup (hintToHtml), less its hidden
 *   blocks, which it shows as typed for the same reason; the exact markup of
 *   it is held by the pairs of hintMarkup.cases.ts.
 *   bbcodeToHtml followed by htmlToBbcode gives every input back as typed,
 *   names in capitals included, less the characters that reorder text and the
 *   control characters; validateBBCode, in the same context, is silent on rows
 *   without broken and names the reason on rows with it.
 */

export type GrammarSurface =
  | "any"
  | "GamePost"
  | "Comment"
  | "GlobalChatMessage"
  | "Profile"
  | "DirectMessage"
  | "Hint";

export type GrammarAudience = "display" | "author_edit";

/**
 * What the editor's markup check names in a row: why a spelling is broken, an
 * address the page refuses, a tag left open, a closing tag with nothing to
 * close, a hidden block inside another, or hiding markup on a surface that
 * does not declare it. The wording of each is in BBCODE_RENDERING.md, section
 * "Предупреждения редактора".
 */
export type GrammarBrokenReason =
  | "value-after-name"
  | "unknown-param"
  | "unquoted-value"
  | "repeated-param"
  | "bracket-in-value"
  | "newline-in-value"
  | "unterminated-value"
  | "stray-space"
  | "bad-number"
  | "bad-content"
  | "missing-param"
  | "bad-address"
  | "value-on-closing-tag"
  | "private-spelling"
  | "private-nested"
  | "unclosed"
  | "stray-closing"
  | "private-outside-game";

/** Who reads a hidden block. */
export type GrammarReader = "author" | "leads" | "addressees" | "overrides";

export type GrammarPiece =
  | { text: string }
  | {
      tag: string;
      params?: Record<string, string>;
      children?: GrammarPiece[];
      gate?: "compact";
    }
  | {
      hidden: { to: string[]; seenBy: GrammarReader[] };
      typed?: string;
      children: GrammarPiece[];
    }
  | { dash: "short" | "long" }
  | { source: string };

export interface GrammarCase {
  id: string;
  rule: string;
  input: string;
  surface: GrammarSurface;
  audience?: GrammarAudience;
  broken?: GrammarBrokenReason;
  save?: "refused";
  editor?: boolean;
  expect: GrammarPiece[];
}

// prettier-ignore
export const BBCODE_GRAMMAR_CASES: readonly GrammarCase[] = /* grammar:begin */ [
{"id":"a-b","rule":"A","input":"[b]текст[/b]","surface":"any","editor":true,"expect":[{"tag":"b","children":[{"text":"текст"}]}]},
{"id":"a-i","rule":"A","input":"[i]текст[/i]","surface":"any","editor":true,"expect":[{"tag":"i","children":[{"text":"текст"}]}]},
{"id":"a-u","rule":"A","input":"[u]текст[/u]","surface":"any","editor":true,"expect":[{"tag":"u","children":[{"text":"текст"}]}]},
{"id":"a-strike","rule":"A","input":"[strike]текст[/strike]","surface":"any","editor":true,"expect":[{"tag":"strike","children":[{"text":"текст"}]}]},
{"id":"a-code","rule":"A","input":"[code]a < b[/code]","surface":"any","editor":true,"expect":[{"tag":"code","children":[{"text":"a < b"}]}]},
{"id":"a-code-markup","rule":"A","input":"[code][b]жирный[/b] и [i]курсив[/i][/code]","surface":"any","expect":[{"tag":"code","children":[{"tag":"b","children":[{"text":"жирный"}]},{"text":" и "},{"tag":"i","children":[{"text":"курсив"}]}]}]},
{"id":"a-code-spaces","rule":"A","input":"[code]раз  два\n  три[/code]","surface":"any","expect":[{"tag":"code","children":[{"text":"раз  два\n  три"}]}]},
{"id":"a-code-image","rule":"A","input":"[code][img]https://example.com/a.png[/img][/code]","surface":"Comment","expect":[{"tag":"code","children":[{"tag":"img","params":{"src":"https://example.com/a.png"}}]}]},
{"id":"a-spoiler","rule":"A","input":"[spoiler]текст[/spoiler]","surface":"any","editor":true,"expect":[{"tag":"spoiler","children":[{"text":"текст"}]}]},
{"id":"a-nsfw","rule":"A","input":"[nsfw]текст[/nsfw]","surface":"any","editor":true,"expect":[{"tag":"nsfw","children":[{"text":"текст"}]}]},
{"id":"a-ul","rule":"A","input":"[ul][li]раз[/li][li]два[/li][/ul]","surface":"any","editor":true,"expect":[{"tag":"ul","children":[{"tag":"li","children":[{"text":"раз"}]},{"tag":"li","children":[{"text":"два"}]}]}]},
{"id":"a-ol","rule":"A","input":"[ol][li]раз[/li][li]два[/li][/ol]","surface":"any","editor":true,"expect":[{"tag":"ol","children":[{"tag":"li","children":[{"text":"раз"}]},{"tag":"li","children":[{"text":"два"}]}]}]},
{"id":"a-img","rule":"A","input":"[img]https://example.com/a.png[/img]","surface":"Comment","editor":true,"expect":[{"tag":"img","params":{"src":"https://example.com/a.png"}}]},
{"id":"a-link","rule":"A","input":"[link]https://example.com/[/link]","surface":"any","editor":true,"expect":[{"tag":"link","params":{"href":"https://example.com/","text":"ссылка"}}]},
{"id":"a-private","rule":"A","input":"[private=\"Вася\"]текст[/private]","surface":"GamePost","editor":true,"expect":[{"hidden":{"to":["Вася"],"seenBy":["author","leads","addressees","overrides"]},"children":[{"text":"текст"}]}]},
{"id":"a-private-names","rule":"A","input":"[private=\"Вася, Петя\"]текст[/private]","surface":"GamePost","editor":true,"expect":[{"hidden":{"to":["Вася","Петя"],"seenBy":["author","leads","addressees","overrides"]},"children":[{"text":"текст"}]}]},
{"id":"a-private-dm2-name","rule":"A","input":"[private=\"Джулиус `Кобризо` Морвир\"]текст[/private]","surface":"GamePost","expect":[{"hidden":{"to":["Джулиус `Кобризо` Морвир"],"seenBy":["author","leads","addressees","overrides"]},"children":[{"text":"текст"}]}]},
{"id":"a-private-quote-in-name","rule":"A","input":"[private=\"Вася \"Кот\" Пупкин\"]текст[/private]","surface":"GamePost","expect":[{"hidden":{"to":["Вася \"Кот\" Пупкин"],"seenBy":["author","leads","addressees","overrides"]},"children":[{"text":"текст"}]}]},
{"id":"a-private-ends-at-quote-bracket","rule":"A","input":"[private=\"Вася\" x=\"1\"]текст[/private]","surface":"GamePost","expect":[{"hidden":{"to":["Вася\" x=\"1"],"seenBy":["author","leads","addressees","overrides"]},"children":[{"text":"текст"}]}]},
{"id":"a-private-names-trimmed","rule":"A","input":"[private=\"Вася ,  Петя,\"]текст[/private]","surface":"GamePost","expect":[{"hidden":{"to":["Вася","Петя"],"seenBy":["author","leads","addressees","overrides"]},"children":[{"text":"текст"}]}]},
{"id":"a-private-names-lead","rule":"A","input":"[private=\"Assistant, Вася\"]текст[/private]","surface":"GamePost","expect":[{"hidden":{"to":["Assistant","Вася"],"seenBy":["author","leads","addressees","overrides"]},"children":[{"text":"текст"}]}]},
{"id":"a-quote","rule":"A","input":"[quote]текст[/quote]","surface":"any","editor":true,"expect":[{"tag":"quote","children":[{"text":"текст"}]}]},
{"id":"a-tab","rule":"A","input":"раз[tab]два","surface":"any","editor":true,"expect":[{"text":"раз"},{"tag":"tab"},{"text":"два"}]},
{"id":"a-noparse","rule":"A","input":"[noparse][b]текст[/b][/noparse]","surface":"any","editor":true,"expect":[{"tag":"noparse","children":[{"text":"[b]текст[/b]"}]}]},
{"id":"a-mod-comment","rule":"A","input":"[mod]текст[/mod]","surface":"Comment","editor":true,"expect":[{"tag":"mod","children":[{"text":"текст"}]}]},
{"id":"a-mod-chat","rule":"A","input":"[mod]текст[/mod]","surface":"GlobalChatMessage","expect":[{"tag":"mod","children":[{"text":"текст"}]}]},
{"id":"a-mod-post","rule":"A","input":"[mod]текст[/mod]","surface":"GamePost","expect":[{"text":"[mod]текст[/mod]"}]},
{"id":"a-mod-profile","rule":"A","input":"[mod]текст[/mod]","surface":"Profile","expect":[{"text":"[mod]текст[/mod]"}]},
{"id":"a-mod-direct","rule":"A","input":"[mod]текст[/mod]","surface":"DirectMessage","expect":[{"text":"[mod]текст[/mod]"}]},
{"id":"a-nested","rule":"A","input":"[quote][b]жирный[/b] и [i]курсив[/i][/quote]","surface":"any","expect":[{"tag":"quote","children":[{"tag":"b","children":[{"text":"жирный"}]},{"text":" и "},{"tag":"i","children":[{"text":"курсив"}]}]}]},
{"id":"a-b-around-link","rule":"A","input":"[b][link]https://example.com/[/link][/b]","surface":"any","expect":[{"tag":"b","children":[{"tag":"link","params":{"href":"https://example.com/","text":"ссылка"}}]}]},
{"id":"a-links-side-by-side","rule":"A","input":"[link]https://example.com/[/link][link]https://example.com/[/link]","surface":"any","expect":[{"tag":"link","params":{"href":"https://example.com/","text":"ссылка"}},{"tag":"link","params":{"href":"https://example.com/","text":"ссылка"}}]},
{"id":"a-list-first-in-item","rule":"A","input":"[ul][li][ul][li]раз[/li][/ul][/li][/ul]","surface":"any","expect":[{"tag":"ul","children":[{"tag":"li","children":[{"tag":"ul","children":[{"tag":"li","children":[{"text":"раз"}]}]}]}]}]},
{"id":"a-quote-first-in-item","rule":"A","input":"[ul][li][quote]раз[/quote][/li][/ul]","surface":"any","expect":[{"tag":"ul","children":[{"tag":"li","children":[{"tag":"quote","children":[{"text":"раз"}]}]}]}]},
{"id":"a-private-comment","rule":"A","input":"[private=\"Вася\"]секрет[/private]","surface":"Comment","broken":"private-outside-game","save":"refused","expect":[{"hidden":{"to":["Вася"],"seenBy":[]},"children":[{"text":"секрет"}]}]},
{"id":"a-private-profile","rule":"A","input":"[private=\"Вася\"]секрет[/private]","surface":"Profile","broken":"private-outside-game","save":"refused","expect":[{"hidden":{"to":["Вася"],"seenBy":[]},"children":[{"text":"секрет"}]}]},
{"id":"a-private-direct","rule":"A","input":"[private]секрет[/private]","surface":"DirectMessage","broken":"private-outside-game","save":"refused","expect":[{"hidden":{"to":[],"seenBy":[]},"children":[{"text":"секрет"}]}]},
{"id":"a-private-chat","rule":"A","input":"[private]секрет[/private]","surface":"GlobalChatMessage","broken":"private-outside-game","save":"refused","expect":[{"hidden":{"to":[],"seenBy":[]},"children":[{"text":"секрет"}]}]},
{"id":"a-line-after-quote","rule":"A","input":"перед\n[quote]цитата[/quote]\nпосле","surface":"any","expect":[{"text":"перед\n"},{"tag":"quote","children":[{"text":"цитата"}]},{"text":"после"}]},
{"id":"a-line-after-spoiler","rule":"A","input":"[spoiler]текст[/spoiler]\nпосле","surface":"any","expect":[{"tag":"spoiler","children":[{"text":"текст"}]},{"text":"после"}]},
{"id":"a-line-after-list","rule":"A","input":"[ul][li]раз[/li][/ul]\nпосле","surface":"any","expect":[{"tag":"ul","children":[{"tag":"li","children":[{"text":"раз"}]}]},{"text":"после"}]},
{"id":"a-item-in-item-in-spoiler","rule":"A","input":"[li]а[spoiler][li]б[/li]в[/spoiler][/li]","surface":"any","expect":[{"tag":"li","children":[{"text":"а"},{"tag":"spoiler","children":[{"tag":"li","children":[{"text":"б"}]},{"text":"в"}]}]}]},
{"id":"a-item-in-list-in-item","rule":"A","input":"[ul][li]а[ul][li]б[/li][/ul][/li][/ul]","surface":"any","expect":[{"tag":"ul","children":[{"tag":"li","children":[{"text":"а"},{"tag":"ul","children":[{"tag":"li","children":[{"text":"б"}]}]}]}]}]},
{"id":"a-two-lines-after-quote","rule":"A","input":"[quote]цитата[/quote]\n\nпосле","surface":"any","expect":[{"tag":"quote","children":[{"text":"цитата"}]},{"text":"\nпосле"}]},
{"id":"a-line-after-bold-stays","rule":"A","input":"[b]текст[/b]\nпосле","surface":"any","expect":[{"tag":"b","children":[{"text":"текст"}]},{"text":"\nпосле"}]},
{"id":"b-spoiler-text","rule":"B","input":"[spoiler text=\"Подпись кнопки\"]текст[/spoiler]","surface":"any","editor":true,"expect":[{"tag":"spoiler","params":{"text":"Подпись кнопки"},"children":[{"text":"текст"}]}]},
{"id":"b-spoiler-text-empty","rule":"B","input":"[spoiler text=\"\"]текст[/spoiler]","surface":"any","expect":[{"tag":"spoiler","children":[{"text":"текст"}]}]},
{"id":"b-spoiler-text-quotes","rule":"B","input":"[spoiler text=\"Он сказал \"да\"\"]текст[/spoiler]","surface":"any","expect":[{"tag":"spoiler","params":{"text":"Он сказал \"да\""},"children":[{"text":"текст"}]}]},
{"id":"b-img-width","rule":"B","input":"[img width=\"300\"]https://example.com/a.png[/img]","surface":"Comment","editor":true,"expect":[{"tag":"img","params":{"src":"https://example.com/a.png","width":"300"}}]},
{"id":"b-img-height","rule":"B","input":"[img height=\"200\"]https://example.com/a.png[/img]","surface":"Comment","editor":true,"expect":[{"tag":"img","params":{"src":"https://example.com/a.png","height":"200"}}]},
{"id":"b-img-all","rule":"B","input":"[img width=\"300\" height=\"200\"]https://example.com/a.png[/img]","surface":"Comment","editor":true,"expect":[{"tag":"img","params":{"src":"https://example.com/a.png","width":"300","height":"200"}}]},
{"id":"b-img-any-order","rule":"B","input":"[img height=\"200\" width=\"300\"]https://example.com/a.png[/img]","surface":"Comment","expect":[{"tag":"img","params":{"src":"https://example.com/a.png","width":"300","height":"200"}}]},
{"id":"b-img-largest","rule":"B","input":"[img width=\"9999\" height=\"9999\"]https://example.com/a.png[/img]","surface":"Comment","expect":[{"tag":"img","params":{"src":"https://example.com/a.png","width":"9999","height":"9999"}}]},
{"id":"b-img-several-spaces","rule":"B","input":"[img  width=\"300\"   height=\"200\"]https://example.com/a.png[/img]","surface":"Comment","expect":[{"tag":"img","params":{"src":"https://example.com/a.png","width":"300","height":"200"}}]},
{"id":"b-spoiler-text-bracket","rule":"B","input":"[spoiler text=\"a]b\"]текст[/spoiler]","surface":"any","expect":[{"tag":"spoiler","params":{"text":"a]b"},"children":[{"text":"текст"}]}]},
{"id":"b-link-text-quotes","rule":"B","input":"[link text=\"он сказал \"да\" и ушел\"]https://example.com/[/link]","surface":"any","expect":[{"tag":"link","params":{"href":"https://example.com/","text":"он сказал \"да\" и ушел"}}]},
{"id":"b-img-in-spoiler","rule":"B","input":"[spoiler text=\"Карта\"][img]https://example.com/a.png[/img][/spoiler]","surface":"Comment","expect":[{"tag":"spoiler","params":{"text":"Карта"},"children":[{"tag":"img","params":{"src":"https://example.com/a.png"}}]}]},
{"id":"b-img-refused-scheme","rule":"B","input":"[img]javascript:alert(1)[/img]","surface":"Comment","broken":"bad-address","expect":[{"text":"[img]javascript:alert(1)[/img]"}]},
{"id":"b-img-refused-loopback","rule":"B","input":"[img]http://127.0.0.1/a.png[/img]","surface":"Comment","broken":"bad-address","expect":[{"text":"[img]http://127.0.0.1/a.png[/img]"}]},
{"id":"b-img-refused-params","rule":"B","input":"[img width=\"300\" height=\"200\"]http://192.168.0.1/a.png[/img]","surface":"Comment","broken":"bad-address","expect":[{"text":"[img width=\"300\" height=\"200\"]http://192.168.0.1/a.png[/img]"}]},
{"id":"b-img-refused-chat","rule":"B","input":"[img]http://10.0.0.1/a.png[/img]","surface":"GlobalChatMessage","broken":"bad-address","expect":[{"text":"[img]http://10.0.0.1/a.png[/img]"}]},
{"id":"b-link-text","rule":"B","input":"[link text=\"Главная\"]https://example.com/[/link]","surface":"any","editor":true,"expect":[{"tag":"link","params":{"href":"https://example.com/","text":"Главная"}}]},
{"id":"b-link-text-empty","rule":"B","input":"[link text=\"\"]https://example.com/[/link]","surface":"any","expect":[{"tag":"link","params":{"href":"https://example.com/","text":"ссылка"}}]},
{"id":"b-link-text-spaces","rule":"B","input":"[link text=\"Главная страница\"]https://example.com/[/link]","surface":"any","expect":[{"tag":"link","params":{"href":"https://example.com/","text":"Главная страница"}}]},
{"id":"b-link-refused-scheme","rule":"B","input":"[link]javascript:alert(1)[/link]","surface":"any","broken":"bad-address","expect":[{"text":"[link]javascript:alert(1)[/link]"}]},
{"id":"b-link-refused-mailto","rule":"B","input":"[link]mailto:a@example.com[/link]","surface":"any","broken":"bad-address","expect":[{"text":"[link]mailto:a@example.com[/link]"}]},
{"id":"b-link-refused-with-text","rule":"B","input":"[link text=\"Почта\"]mailto:a@example.com[/link]","surface":"any","broken":"bad-address","expect":[{"text":"[link text=\"Почта\"]mailto:a@example.com[/link]"}]},
{"id":"b-link-refused-private-network","rule":"B","input":"[link]http://192.168.0.1/[/link]","surface":"any","broken":"bad-address","expect":[{"text":"[link]http://192.168.0.1/[/link]"}]},
{"id":"b-link-refused-root-dot","rule":"B","input":"[link]http://localhost./[/link]","surface":"any","broken":"bad-address","expect":[{"text":"[link]http://localhost./[/link]"}]},
{"id":"b-img-refused-root-dot","rule":"B","input":"[img]http://127.0.0.1./a.png[/img]","surface":"Comment","broken":"bad-address","expect":[{"text":"[img]http://127.0.0.1./a.png[/img]"}]},
{"id":"b-link-refused-backslash","rule":"B","input":"[link]https://example.com\\@127.0.0.1/[/link]","surface":"any","broken":"bad-address","expect":[{"text":"[link]https://example.com\\@127.0.0.1/[/link]"}]},
{"id":"b-link-refused-empty-host","rule":"B","input":"[link]https:///example.com/[/link]","surface":"any","broken":"bad-address","expect":[{"text":"[link]https:///example.com/[/link]"}]},
{"id":"b-link-refused-percent-host","rule":"B","input":"[link]https://ex%61mple.com/[/link]","surface":"any","broken":"bad-address","expect":[{"text":"[link]https://ex%61mple.com/[/link]"}]},
{"id":"b-link-refused-bom","rule":"B","input":"[link]\uFEFFhttps://example.com/[/link]","surface":"any","broken":"bad-address","expect":[{"text":"[link]\uFEFFhttps://example.com/[/link]"}]},
{"id":"b-link-not-an-address","rule":"B","input":"[link]два слова[/link]","surface":"any","broken":"bad-content","expect":[{"text":"[link]два слова[/link]"}]},
{"id":"b-link-around-mention","rule":"B","input":"[link][mention]Вася[/mention][/link]","surface":"any","broken":"bad-content","expect":[{"text":"[link]"},{"tag":"mention","params":{"login":"Вася"}},{"text":"[/link]"}]},
{"id":"b-link-in-link","rule":"B","input":"[link][link]https://example.com/[/link][/link]","surface":"any","broken":"bad-content","expect":[{"text":"[link]"},{"tag":"link","params":{"href":"https://example.com/","text":"ссылка"}},{"text":"[/link]"}]},
{"id":"b-mention","rule":"B","input":"[mention]Вася[/mention]","surface":"any","expect":[{"tag":"mention","params":{"login":"Вася"}}]},
{"id":"b-mention-spaces","rule":"B","input":"[mention]Бродяга по жизни[/mention]","surface":"any","expect":[{"tag":"mention","params":{"login":"Бродяга по жизни"}}]},
{"id":"b-mention-empty","rule":"B","input":"[mention][/mention]","surface":"any","broken":"bad-content","expect":[{"text":"[mention][/mention]"}]},
{"id":"b-mention-markup","rule":"B","input":"[mention]Ва[b]ся[/b][/mention]","surface":"any","broken":"bad-content","expect":[{"text":"[mention]Ва"},{"tag":"b","children":[{"text":"ся"}]},{"text":"[/mention]"}]},
{"id":"b-mention-in-code","rule":"B","input":"[code][mention]Вася[/mention][/code]","surface":"any","expect":[{"tag":"code","children":[{"tag":"mention","params":{"login":"Вася"}}]}]},
{"id":"b-mention-in-noparse","rule":"B","input":"[noparse][mention]Вася[/mention][/noparse]","surface":"any","expect":[{"tag":"noparse","children":[{"text":"[mention]Вася[/mention]"}]}]},
{"id":"b-mention-profile","rule":"B","input":"[mention]Вася[/mention]","surface":"Profile","expect":[{"tag":"mention","params":{"login":"Вася"}}]},
{"id":"b-mention-direct","rule":"B","input":"[mention]Вася[/mention]","surface":"DirectMessage","expect":[{"tag":"mention","params":{"login":"Вася"}}]},
{"id":"b-mention-bom","rule":"B","input":"[mention]\uFEFFВася[/mention]","surface":"any","expect":[{"tag":"mention","params":{"login":"\uFEFFВася"}}]},
{"id":"b-private-bare","rule":"B","input":"[private]секрет[/private]","surface":"GamePost","editor":true,"expect":[{"hidden":{"to":[],"seenBy":["author","leads"]},"children":[{"text":"секрет"}]}]},
{"id":"b-private-empty-value","rule":"B","input":"[private=\"\"]секрет[/private]","surface":"GamePost","expect":[{"hidden":{"to":[],"seenBy":["author","leads"]},"children":[{"text":"секрет"}]}]},
{"id":"c-unquoted-number","rule":"C","input":"[img width=300]https://example.com/a.png[/img]","surface":"Comment","broken":"unquoted-value","expect":[{"text":"[img width=300]https://example.com/a.png[/img]"}]},
{"id":"c-unquoted-text","rule":"C","input":"[spoiler text=Подпись]текст[/spoiler]","surface":"any","broken":"unquoted-value","expect":[{"text":"[spoiler text=Подпись]текст[/spoiler]"}]},
{"id":"c-bracket-in-value","rule":"C","input":"[spoiler text=\"a[b\"]текст[/spoiler]","surface":"any","broken":"bracket-in-value","expect":[{"text":"[spoiler text=\"a[b\"]текст[/spoiler]"}]},
{"id":"c-newline-in-value","rule":"C","input":"[spoiler text=\"раз\nдва\"]текст[/spoiler]","surface":"any","broken":"newline-in-value","expect":[{"text":"[spoiler text=\"раз\nдва\"]текст[/spoiler]"}]},
{"id":"c-repeated","rule":"C","input":"[img width=\"300\" width=\"400\"]https://example.com/a.png[/img]","surface":"Comment","broken":"repeated-param","expect":[{"text":"[img width=\"300\" width=\"400\"]https://example.com/a.png[/img]"}]},
{"id":"c-misspelled","rule":"C","input":"[img widht=\"300\"]https://example.com/a.png[/img]","surface":"Comment","broken":"unknown-param","expect":[{"text":"[img widht=\"300\"]https://example.com/a.png[/img]"}]},
{"id":"c-unknown-title","rule":"C","input":"[spoiler title=\"Подпись\"]текст[/spoiler]","surface":"any","broken":"unknown-param","expect":[{"text":"[spoiler title=\"Подпись\"]текст[/spoiler]"}]},
{"id":"c-value-ends-before-next-name","rule":"C","input":"[spoiler text=\"a\" b=\"c\"]текст[/spoiler]","surface":"any","broken":"unknown-param","expect":[{"text":"[spoiler text=\"a\" b=\"c\"]текст[/spoiler]"}]},
{"id":"c-unterminated","rule":"C","input":"[spoiler text=\"кот]текст[/spoiler]","surface":"any","broken":"unterminated-value","expect":[{"text":"[spoiler text=\"кот]текст[/spoiler]"}]},
{"id":"c-space-before-bracket","rule":"C","input":"[img width=\"300\" ]https://example.com/a.png[/img]","surface":"Comment","broken":"stray-space","expect":[{"text":"[img width=\"300\" ]https://example.com/a.png[/img]"}]},
{"id":"c-space-around-equals","rule":"C","input":"[spoiler text = \"Подпись\"]текст[/spoiler]","surface":"any","broken":"stray-space","expect":[{"text":"[spoiler text = \"Подпись\"]текст[/spoiler]"}]},
{"id":"c-number-with-unit","rule":"C","input":"[img width=\"300px\"]https://example.com/a.png[/img]","surface":"Comment","broken":"bad-number","expect":[{"text":"[img width=\"300px\"]https://example.com/a.png[/img]"}]},
{"id":"c-number-zero","rule":"C","input":"[img width=\"0\"]https://example.com/a.png[/img]","surface":"Comment","broken":"bad-number","expect":[{"text":"[img width=\"0\"]https://example.com/a.png[/img]"}]},
{"id":"c-number-too-big","rule":"C","input":"[img height=\"10000\"]https://example.com/a.png[/img]","surface":"Comment","broken":"bad-number","expect":[{"text":"[img height=\"10000\"]https://example.com/a.png[/img]"}]},
{"id":"c-number-empty","rule":"C","input":"[img width=\"\"]https://example.com/a.png[/img]","surface":"Comment","broken":"bad-number","expect":[{"text":"[img width=\"\"]https://example.com/a.png[/img]"}]},
{"id":"c-number-word","rule":"C","input":"[img height=\"abc\"]https://example.com/a.png[/img]","surface":"Comment","broken":"bad-number","expect":[{"text":"[img height=\"abc\"]https://example.com/a.png[/img]"}]},
{"id":"d-code-value","rule":"D","input":"[code=\"x\"][b]текст[/b][/code]","surface":"any","broken":"value-after-name","expect":[{"text":"[code=\"x\"]"},{"tag":"b","children":[{"text":"текст"}]},{"text":"[/code]"}]},
{"id":"d-noparse-value","rule":"D","input":"[noparse=x][b]текст[/b][/noparse]","surface":"any","broken":"value-after-name","expect":[{"text":"[noparse=x]"},{"tag":"b","children":[{"text":"текст"}]},{"text":"[/noparse]"}]},
{"id":"d-noparse-param","rule":"D","input":"[noparse text=\"x\"][b]текст[/b][/noparse]","surface":"any","broken":"unknown-param","expect":[{"text":"[noparse text=\"x\"]"},{"tag":"b","children":[{"text":"текст"}]},{"text":"[/noparse]"}]},
{"id":"d-code-param","rule":"D","input":"[code lang=\"js\"][b]текст[/b][/code]","surface":"any","broken":"unknown-param","expect":[{"text":"[code lang=\"js\"]"},{"tag":"b","children":[{"text":"текст"}]},{"text":"[/code]"}]},
{"id":"d-b-value","rule":"D","input":"[b=1]текст[/b]","surface":"any","broken":"value-after-name","expect":[{"text":"[b=1]текст[/b]"}]},
{"id":"d-quote-author","rule":"D","input":"[quote=\"Имя\"]текст[/quote]","surface":"any","broken":"value-after-name","expect":[{"text":"[quote=\"Имя\"]текст[/quote]"}]},
{"id":"d-quote-author-unquoted","rule":"D","input":"[quote=Имя]текст[/quote]","surface":"any","broken":"value-after-name","expect":[{"text":"[quote=Имя]текст[/quote]"}]},
{"id":"d-quote-author-content","rule":"D","input":"[quote=\"Имя\"][b]жирный[/b][/quote]","surface":"any","broken":"value-after-name","expect":[{"text":"[quote=\"Имя\"]"},{"tag":"b","children":[{"text":"жирный"}]},{"text":"[/quote]"}]},
{"id":"d-tab-value","rule":"D","input":"раз[tab=1]два","surface":"any","broken":"value-after-name","expect":[{"text":"раз[tab=1]два"}]},
{"id":"d-ol-value","rule":"D","input":"[ol=1]раз[/ol]","surface":"any","broken":"value-after-name","expect":[{"text":"[ol=1]раз[/ol]"}]},
{"id":"d-nsfw-value","rule":"D","input":"[nsfw=\"1\"]текст[/nsfw]","surface":"any","broken":"value-after-name","expect":[{"text":"[nsfw=\"1\"]текст[/nsfw]"}]},
{"id":"d-mod-value","rule":"D","input":"[mod=\"x\"]текст[/mod]","surface":"Comment","broken":"value-after-name","expect":[{"text":"[mod=\"x\"]текст[/mod]"}]},
{"id":"d-b-param","rule":"D","input":"[b x=\"1\"]текст[/b]","surface":"any","broken":"unknown-param","expect":[{"text":"[b x=\"1\"]текст[/b]"}]},
{"id":"d-spoiler-value-quoted","rule":"D","input":"[spoiler=\"Подпись\"]текст[/spoiler]","surface":"any","broken":"value-after-name","expect":[{"text":"[spoiler=\"Подпись\"]текст[/spoiler]"}]},
{"id":"d-spoiler-value","rule":"D","input":"[spoiler=Подпись]текст[/spoiler]","surface":"any","broken":"value-after-name","expect":[{"text":"[spoiler=Подпись]текст[/spoiler]"}]},
{"id":"d-img-size","rule":"D","input":"[img=300]https://example.com/a.png[/img]","surface":"Comment","broken":"value-after-name","expect":[{"text":"[img=300]https://example.com/a.png[/img]"}]},
{"id":"d-img-size-both","rule":"D","input":"[img=300x200]https://example.com/a.png[/img]","surface":"Comment","broken":"value-after-name","expect":[{"text":"[img=300x200]https://example.com/a.png[/img]"}]},
{"id":"d-img-size-alt","rule":"D","input":"[img=300x200 alt=\"кот\"]https://example.com/a.png[/img]","surface":"Comment","broken":"value-after-name","expect":[{"text":"[img=300x200 alt=\"кот\"]https://example.com/a.png[/img]"}]},
{"id":"d-img-address","rule":"D","input":"[img=https://example.com/a.png]","surface":"Comment","broken":"value-after-name","expect":[{"text":"[img=https://example.com/a.png]"}]},
{"id":"d-img-address-quoted","rule":"D","input":"[img=\"https://example.com/a.png\"]","surface":"Comment","broken":"value-after-name","expect":[{"text":"[img=\"https://example.com/a.png\"]"}]},
{"id":"d-img-alt","rule":"D","input":"[img alt=\"кот на окне\"]https://example.com/a.png[/img]","surface":"Comment","broken":"unknown-param","expect":[{"text":"[img alt=\"кот на окне\"]https://example.com/a.png[/img]"}]},
{"id":"d-img-alt-empty","rule":"D","input":"[img alt=\"\"]https://example.com/a.png[/img]","surface":"Comment","broken":"unknown-param","expect":[{"text":"[img alt=\"\"]https://example.com/a.png[/img]"}]},
{"id":"d-img-alt-with-size","rule":"D","input":"[img width=\"300\" height=\"200\" alt=\"кот\"]https://example.com/a.png[/img]","surface":"Comment","broken":"unknown-param","expect":[{"text":"[img width=\"300\" height=\"200\" alt=\"кот\"]https://example.com/a.png[/img]"}]},
{"id":"d-img-alt-upper","rule":"D","input":"[IMG ALT=\"кот\"]https://example.com/a.png[/IMG]","surface":"Comment","broken":"unknown-param","expect":[{"text":"[IMG ALT=\"кот\"]https://example.com/a.png[/IMG]"}]},
{"id":"d-img-alt-chat","rule":"D","input":"[img alt=\"кот\"]https://example.com/a.png[/img]","surface":"GlobalChatMessage","broken":"unknown-param","expect":[{"text":"[img alt=\"кот\"]https://example.com/a.png[/img]"}]},
{"id":"d-link-address-value","rule":"D","input":"[link=https://example.com/]Главная[/link]","surface":"any","broken":"value-after-name","expect":[{"text":"[link=https://example.com/]Главная[/link]"}]},
{"id":"d-link-text-value","rule":"D","input":"[link=Главная]https://example.com/[/link]","surface":"any","broken":"value-after-name","expect":[{"text":"[link=Главная]https://example.com/[/link]"}]},
{"id":"d-mention-value","rule":"D","input":"[mention=\"Вася\"]","surface":"any","broken":"value-after-name","expect":[{"text":"[mention=\"Вася\"]"}]},
{"id":"d-mention-value-unquoted","rule":"D","input":"[mention=Вася]","surface":"any","broken":"value-after-name","expect":[{"text":"[mention=Вася]"}]},
{"id":"d-closing-value","rule":"D","input":"[b]текст[/b=\"x\"][/b]","surface":"any","broken":"value-on-closing-tag","expect":[{"tag":"b","children":[{"text":"текст[/b=\"x\"]"}]}]},
{"id":"d-closing-value-unquoted","rule":"D","input":"[i]текст[/i=x][/i]","surface":"any","broken":"value-on-closing-tag","expect":[{"tag":"i","children":[{"text":"текст[/i=x]"}]}]},
{"id":"d-around-private","rule":"D","input":"[quote=\"Имя\"][private=\"Вася\"]секрет[/private][/quote]","surface":"GamePost","broken":"value-after-name","expect":[{"text":"[quote=\"Имя\"]"},{"hidden":{"to":["Вася"],"seenBy":["author","leads","addressees","overrides"]},"children":[{"text":"секрет"}]},{"text":"[/quote]"}]},
{"id":"d-removed-tag","rule":"D","input":"[s]текст[/s]","surface":"any","expect":[{"text":"[s]текст[/s]"}]},
{"id":"e-space-before-equals","rule":"E","input":"[private =\"Вася\"]секрет[/private]","surface":"GamePost","broken":"private-spelling","save":"refused","expect":[{"hidden":{"to":[],"seenBy":["author","leads"]},"children":[{"text":"секрет"}]}]},
{"id":"e-space-after-equals","rule":"E","input":"[private= \"Вася\"]секрет[/private]","surface":"GamePost","broken":"private-spelling","save":"refused","expect":[{"hidden":{"to":[],"seenBy":["author","leads"]},"children":[{"text":"секрет"}]}]},
{"id":"e-named-to","rule":"E","input":"[private to=\"Вася\"]секрет[/private]","surface":"GamePost","broken":"private-spelling","save":"refused","expect":[{"hidden":{"to":[],"seenBy":["author","leads"]},"children":[{"text":"секрет"}]}]},
{"id":"e-unquoted","rule":"E","input":"[private=Вася]секрет[/private]","surface":"GamePost","broken":"private-spelling","save":"refused","expect":[{"hidden":{"to":[],"seenBy":["author","leads"]},"children":[{"text":"секрет"}]}]},
{"id":"e-unquoted-with-space","rule":"E","input":"[private=Вася Пупкин]секрет[/private]","surface":"GamePost","broken":"private-spelling","save":"refused","expect":[{"hidden":{"to":[],"seenBy":["author","leads"]},"children":[{"text":"секрет"}]}]},
{"id":"e-no-closing-quote","rule":"E","input":"[private=\"Вася]секрет[/private]","surface":"GamePost","broken":"private-spelling","save":"refused","expect":[{"hidden":{"to":[],"seenBy":["author","leads"]},"children":[{"text":"секрет"}]}]},
{"id":"e-no-opening-quote","rule":"E","input":"[private=Вася\"]секрет[/private]","surface":"GamePost","broken":"private-spelling","save":"refused","expect":[{"hidden":{"to":[],"seenBy":["author","leads"]},"children":[{"text":"секрет"}]}]},
{"id":"e-newline-in-name","rule":"E","input":"[private=\"Ва\nся\"]секрет[/private]","surface":"GamePost","broken":"private-spelling","save":"refused","expect":[{"hidden":{"to":[],"seenBy":["author","leads"]},"children":[{"text":"секрет"}]}]},
{"id":"e-newline-after-name","rule":"E","input":"[private\n=\"Вася\"]секрет[/private]","surface":"GamePost","broken":"private-spelling","save":"refused","expect":[{"hidden":{"to":[],"seenBy":["author","leads"]},"children":[{"text":"секрет"}]}]},
{"id":"e-bracket-in-name","rule":"E","input":"[private=\"Ва[ся\"]секрет[/private]","surface":"GamePost","broken":"private-spelling","save":"refused","expect":[{"hidden":{"to":[],"seenBy":["author","leads"]},"children":[{"text":"=\"Ва[ся\"]секрет"}]}]},
{"id":"e-upper-case","rule":"E","input":"[PRIVATE=\"Вася\"]секрет[/private]","surface":"GamePost","expect":[{"hidden":{"to":["Вася"],"seenBy":["author","leads","addressees","overrides"]},"children":[{"text":"секрет"}]}]},
{"id":"e-mixed-case-bare","rule":"E","input":"[Private]секрет[/private]","surface":"GamePost","expect":[{"hidden":{"to":[],"seenBy":["author","leads"]},"children":[{"text":"секрет"}]}]},
{"id":"e-upper-case-closing","rule":"E","input":"[private]секрет[/PRIVATE] хвост","surface":"GamePost","expect":[{"hidden":{"to":[],"seenBy":["author","leads"]},"children":[{"text":"секрет"}]},{"text":" хвост"}]},
{"id":"e-value-on-closing","rule":"E","input":"[private]секрет[/private=\"x\"] хвост","surface":"GamePost","broken":"private-spelling","save":"refused","expect":[{"hidden":{"to":[],"seenBy":["author","leads"]},"children":[{"text":"секрет[/private=\"x\"] хвост"}]}]},
{"id":"e-end-of-text","rule":"E","input":"текст [private","surface":"GamePost","broken":"private-spelling","save":"refused","expect":[{"text":"текст "},{"hidden":{"to":[],"seenBy":["author","leads"]},"children":[]}]},
{"id":"e-inside-b","rule":"E","input":"[b][private=\"Вася\"]секрет[/private][/b]","surface":"GamePost","expect":[{"tag":"b","children":[{"hidden":{"to":["Вася"],"seenBy":["author","leads","addressees","overrides"]},"children":[{"text":"секрет"}]}]}]},
{"id":"e-broken-inside-spoiler","rule":"E","input":"[spoiler][private to=\"Вася\"]секрет[/private] хвост[/spoiler]","surface":"GamePost","broken":"private-spelling","save":"refused","expect":[{"tag":"spoiler","children":[{"hidden":{"to":[],"seenBy":["author","leads"]},"children":[{"text":"секрет"}]},{"text":" хвост"}]}]},
{"id":"e-broken-inside-quote","rule":"E","input":"[quote][private=Вася]секрет[/private][/quote]","surface":"GamePost","broken":"private-spelling","save":"refused","expect":[{"tag":"quote","children":[{"hidden":{"to":[],"seenBy":["author","leads"]},"children":[{"text":"секрет"}]}]}]},
{"id":"e-after-unclosed-code-open-block","rule":"E","input":"[code]пример [private=\"Вася\"]секрет","surface":"GamePost","broken":"private-spelling","save":"refused","expect":[{"text":"[code]пример "},{"hidden":{"to":["Вася"],"seenBy":["author","leads","addressees","overrides"]},"children":[{"text":"секрет"}]}]},
{"id":"e-after-unclosed-code","rule":"E","input":"[code]пример [private=\"Вася\"]секрет[/private] хвост","surface":"GamePost","broken":"unclosed","expect":[{"text":"[code]пример "},{"hidden":{"to":["Вася"],"seenBy":["author","leads","addressees","overrides"]},"children":[{"text":"секрет"}]},{"text":" хвост"}]},
{"id":"e-under-unclosed-noparse-closed-block","rule":"E","input":"[noparse]пример [private=\"Вася\"]секрет[/private] хвост","surface":"GamePost","broken":"private-spelling","save":"refused","expect":[{"text":"[noparse]пример "},{"hidden":{"to":["Вася"],"seenBy":["author","leads","addressees","overrides"]},"children":[{"text":"секрет"}]},{"text":" хвост"}]},
{"id":"e-under-unclosed-noparse","rule":"E","input":"[noparse][private]секрет[/private]","surface":"GamePost","broken":"private-spelling","save":"refused","expect":[{"text":"[noparse]"},{"hidden":{"to":[],"seenBy":["author","leads"]},"children":[{"text":"секрет"}]}]},
{"id":"e-in-closed-code","rule":"E","input":"[code][private=\"Вася\"]пример[/private][/code]","surface":"GamePost","expect":[{"tag":"code","children":[{"hidden":{"to":["Вася"],"seenBy":["author","leads","addressees","overrides"]},"children":[{"text":"пример"}]}]}]},
{"id":"e-under-closed-noparse","rule":"E","input":"[noparse][private]пример[/private][/noparse] и [private=\"Вася\"]секрет[/private]","surface":"GamePost","expect":[{"tag":"noparse","children":[{"text":"[private]пример[/private]"}]},{"text":" и "},{"hidden":{"to":["Вася"],"seenBy":["author","leads","addressees","overrides"]},"children":[{"text":"секрет"}]}]},
{"id":"e-under-closed-upper-case-noparse","rule":"E","input":"[NOPARSE][private=\"Вася\"]пример[/private][/NOPARSE]","surface":"GamePost","expect":[{"tag":"noparse","children":[{"text":"[private=\"Вася\"]пример[/private]"}]}]},
{"id":"e-under-noparse-closed-mixed-case","rule":"E","input":"[noparse][private=\"Вася\"]пример[/private][/NoParse]","surface":"GamePost","expect":[{"tag":"noparse","children":[{"text":"[private=\"Вася\"]пример[/private]"}]}]},
{"id":"e-under-upper-case-noparse","rule":"E","input":"[NOPARSE][private]секрет[/private]","surface":"GamePost","broken":"private-spelling","save":"refused","expect":[{"text":"[NOPARSE]"},{"hidden":{"to":[],"seenBy":["author","leads"]},"children":[{"text":"секрет"}]}]},
{"id":"e-under-unclosed-noparse-after-parent","rule":"E","input":"[quote][noparse]пример[/quote] [private=\"Вася\"]секрет[/private]","surface":"GamePost","broken":"private-spelling","save":"refused","expect":[{"tag":"quote","children":[{"text":"[noparse]пример"}]},{"text":" "},{"hidden":{"to":["Вася"],"seenBy":["author","leads","addressees","overrides"]},"children":[{"text":"секрет"}]}]},
{"id":"e-after-unclosed-code-in-parent","rule":"E","input":"[quote][code]пример[/quote] [private=\"Вася\"]секрет[/private]","surface":"GamePost","broken":"unclosed","expect":[{"tag":"quote","children":[{"text":"[code]пример"}]},{"text":" "},{"hidden":{"to":["Вася"],"seenBy":["author","leads","addressees","overrides"]},"children":[{"text":"секрет"}]}]},
{"id":"e-under-unclosed-noparse-closing","rule":"E","input":"[private=\"Вася\"]секрет [noparse]пример[/private] хвост","surface":"GamePost","broken":"private-spelling","save":"refused","expect":[{"hidden":{"to":["Вася"],"seenBy":["author","leads","addressees","overrides"]},"children":[{"text":"секрет [noparse]пример"}]},{"text":" хвост"}]},
{"id":"e-after-unclosed-code-closing","rule":"E","input":"[private=\"Вася\"]секрет [code]пример[/private] хвост","surface":"GamePost","broken":"unclosed","expect":[{"hidden":{"to":["Вася"],"seenBy":["author","leads","addressees","overrides"]},"children":[{"text":"секрет [code]пример"}]},{"text":" хвост"}]},
{"id":"e-under-broken-code","rule":"E","input":"[code=\"x\"][private=\"Вася\"]секрет[/private][/code]","surface":"GamePost","broken":"value-after-name","expect":[{"text":"[code=\"x\"]"},{"hidden":{"to":["Вася"],"seenBy":["author","leads","addressees","overrides"]},"children":[{"text":"секрет"}]},{"text":"[/code]"}]},
{"id":"e-under-broken-noparse","rule":"E","input":"[noparse=x][private=\"Вася\"]секрет[/private][/noparse]","surface":"GamePost","broken":"value-after-name","expect":[{"text":"[noparse=x]"},{"hidden":{"to":["Вася"],"seenBy":["author","leads","addressees","overrides"]},"children":[{"text":"секрет"}]},{"text":"[/noparse]"}]},
{"id":"e-noparse-in-private","rule":"E","input":"[private=\"Вася\"]секрет [noparse][b]пример[/b][/noparse][/private]","surface":"GamePost","expect":[{"hidden":{"to":["Вася"],"seenBy":["author","leads","addressees","overrides"]},"children":[{"text":"секрет "},{"tag":"noparse","children":[{"text":"[b]пример[/b]"}]}]}]},
{"id":"e-noparse-closing-in-private","rule":"E","input":"[private=\"Вася\"]секрет [noparse][/PRIVATE][/noparse][/private]","surface":"GamePost","expect":[{"hidden":{"to":["Вася"],"seenBy":["author","leads","addressees","overrides"]},"children":[{"text":"секрет "},{"tag":"noparse","children":[{"text":"[/PRIVATE]"}]}]}]},
{"id":"e-noparse-holds-opening","rule":"E","input":"[noparse][private=\"Вася\"][/noparse] и дальше","surface":"GamePost","expect":[{"tag":"noparse","children":[{"text":"[private=\"Вася\"]"}]},{"text":" и дальше"}]},
{"id":"e-noparse-holds-closing","rule":"E","input":"текст [noparse][/private][/noparse] хвост","surface":"GamePost","expect":[{"text":"текст "},{"tag":"noparse","children":[{"text":"[/private]"}]},{"text":" хвост"}]},
{"id":"e-nested-bare","rule":"E","input":"[private=\"Вася\"]раз [private]два[/private][/private]","surface":"GamePost","broken":"private-nested","save":"refused","expect":[{"hidden":{"to":["Вася"],"seenBy":["author","leads","addressees","overrides"]},"children":[{"text":"раз "},{"hidden":{"to":[],"seenBy":["author","leads"]},"children":[{"text":"два"}]}]}]},
{"id":"e-word-in-prose","rule":"E","input":"[privateer] и [private_x]","surface":"GamePost","expect":[{"text":"[privateer] и [private_x]"}]},
{"id":"e-no-break-space-after-name","rule":"E","input":"[private\u00A0]секрет[/private]","surface":"GamePost","broken":"private-spelling","save":"refused","expect":[{"hidden":{"to":[],"seenBy":["author","leads"]},"children":[{"text":"секрет"}]}]},
{"id":"e-broken-name-then-tag","rule":"E","input":"[u][private=\"Ва[ся\"]секрет[/private][/u]","surface":"GamePost","broken":"private-spelling","save":"refused","expect":[{"tag":"u","children":[{"hidden":{"to":[],"seenBy":["author","leads"]},"children":[{"text":"=\"Ва[ся\"]секрет"}]}]}]},
{"id":"e-broken-on-comment","rule":"E","input":"[private to=\"Вася\"]секрет[/private]","surface":"Comment","broken":"private-spelling","save":"refused","expect":[{"hidden":{"to":[],"seenBy":[]},"children":[{"text":"секрет"}]}]},
{"id":"e-offgame-around","rule":"E","input":"до [private=\"Вася\"]секрет[/private] после","surface":"Comment","broken":"private-outside-game","save":"refused","expect":[{"text":"до "},{"hidden":{"to":["Вася"],"seenBy":[]},"children":[{"text":"секрет"}]},{"text":" после"}]},
{"id":"e-offgame-unclosed","rule":"E","input":"до [private]секрет","surface":"Profile","broken":"private-spelling","save":"refused","expect":[{"text":"до "},{"hidden":{"to":[],"seenBy":[]},"children":[{"text":"секрет"}]}]},
{"id":"e-offgame-example","rule":"E","input":"[noparse][private=\"Вася\"]пример[/private][/noparse]","surface":"Comment","expect":[{"tag":"noparse","children":[{"text":"[private=\"Вася\"]пример[/private]"}]}]},
{"id":"e-offgame-example-closing","rule":"E","input":"[noparse][/private][/noparse]","surface":"Comment","expect":[{"tag":"noparse","children":[{"text":"[/private]"}]}]},
{"id":"e-offgame-in-code","rule":"E","input":"[code][private=\"Вася\"]секрет[/private][/code]","surface":"Comment","broken":"private-outside-game","save":"refused","expect":[{"tag":"code","children":[{"hidden":{"to":["Вася"],"seenBy":[]},"typed":"[private=\"Вася\"]секрет[/private]","children":[{"text":"секрет"}]}]}]},
{"id":"e-line-after-block","rule":"E","input":"перед\n[private=\"Вася\"]секрет[/private]\nпосле","surface":"GamePost","expect":[{"text":"перед\n"},{"hidden":{"to":["Вася"],"seenBy":["author","leads","addressees","overrides"]},"children":[{"text":"секрет"}]},{"text":"\nпосле"}]},
{"id":"e-line-after-text-after-block","rule":"E","input":"[private]секрет[/private] и замечают.\nДверь","surface":"GamePost","expect":[{"hidden":{"to":[],"seenBy":["author","leads"]},"children":[{"text":"секрет"}]},{"text":" и замечают.\nДверь"}]},
{"id":"e-erased-leaves-no-tag","rule":"E","input":"Раз [b][private]x[/private][/b] два","surface":"GamePost","expect":[{"text":"Раз "},{"tag":"b","children":[{"hidden":{"to":[],"seenBy":["author","leads"]},"children":[{"text":"x"}]}]},{"text":" два"}]},
{"id":"e-erased-leaves-no-tag-around-spaces","rule":"E","input":"раз [i] [private=\"Вася\"]секрет[/private] [/i] два","surface":"GamePost","expect":[{"text":"раз "},{"tag":"i","children":[{"text":" "},{"hidden":{"to":["Вася"],"seenBy":["author","leads","addressees","overrides"]},"children":[{"text":"секрет"}]},{"text":" "}]},{"text":" два"}]},
{"id":"e-erased-leaves-no-spoiler","rule":"E","input":"[spoiler]\n[private=\"Вася\"]секрет[/private]\n[/spoiler]после","surface":"GamePost","expect":[{"tag":"spoiler","children":[{"text":"\n"},{"hidden":{"to":["Вася"],"seenBy":["author","leads","addressees","overrides"]},"children":[{"text":"секрет"}]},{"text":"\n"}]},{"text":"после"}]},
{"id":"e-erased-keeps-typed-empty-tag","rule":"E","input":"[b][/b][private]секрет[/private]","surface":"GamePost","expect":[{"tag":"b"},{"hidden":{"to":[],"seenBy":["author","leads"]},"children":[{"text":"секрет"}]}]},
{"id":"e-erased-own-line","rule":"E","input":"Эльвира обходит каюту и садится за стол капитана.\n[private]Она прячет найденный ключ.[/private]\nПотом она выходит на палубу.","surface":"GamePost","expect":[{"text":"Эльвира обходит каюту и садится за стол капитана.\n"},{"hidden":{"to":[],"seenBy":["author","leads"]},"children":[{"text":"Она прячет найденный ключ."}]},{"text":"\nПотом она выходит на палубу."}]},
{"id":"e-erased-own-line-at-start","rule":"E","input":"[private=\"Вася\"]секрет[/private]\nпосле","surface":"GamePost","expect":[{"hidden":{"to":["Вася"],"seenBy":["author","leads","addressees","overrides"]},"children":[{"text":"секрет"}]},{"text":"\nпосле"}]},
{"id":"e-erased-own-line-at-end","rule":"E","input":"перед\n[private=\"Вася\"]секрет[/private]","surface":"GamePost","expect":[{"text":"перед\n"},{"hidden":{"to":["Вася"],"seenBy":["author","leads","addressees","overrides"]},"children":[{"text":"секрет"}]}]},
{"id":"e-erased-own-lines-in-a-row","rule":"E","input":"перед\n[private]раз[/private]\n[private=\"Вася\"]два[/private]\nпосле","surface":"GamePost","expect":[{"text":"перед\n"},{"hidden":{"to":[],"seenBy":["author","leads"]},"children":[{"text":"раз"}]},{"text":"\n"},{"hidden":{"to":["Вася"],"seenBy":["author","leads","addressees","overrides"]},"children":[{"text":"два"}]},{"text":"\nпосле"}]},
{"id":"e-erased-line-with-space-stays","rule":"E","input":"перед\n [private]секрет[/private]\nпосле","surface":"GamePost","expect":[{"text":"перед\n "},{"hidden":{"to":[],"seenBy":["author","leads"]},"children":[{"text":"секрет"}]},{"text":"\nпосле"}]},
{"id":"e-erased-own-line-in-quote","rule":"E","input":"[quote]перед\n[private]секрет[/private]\nпосле[/quote]","surface":"GamePost","expect":[{"tag":"quote","children":[{"text":"перед\n"},{"hidden":{"to":[],"seenBy":["author","leads"]},"children":[{"text":"секрет"}]},{"text":"\nпосле"}]}]},
{"id":"e-erased-joins-no-dash","rule":"E","input":"а-[private]x[/private]-б","surface":"GamePost","expect":[{"text":"а-"},{"hidden":{"to":[],"seenBy":["author","leads"]},"children":[{"text":"x"}]},{"text":"-б"}]},
{"id":"f-short-dash","rule":"F","input":"раз -- два","surface":"any","expect":[{"text":"раз "},{"dash":"short"},{"text":" два"}]},
{"id":"f-long-dash","rule":"F","input":"раз --- два","surface":"any","expect":[{"text":"раз "},{"dash":"long"},{"text":" два"}]},
{"id":"f-dashes-without-spaces","rule":"F","input":"раз--два---три","surface":"any","expect":[{"text":"раз"},{"dash":"short"},{"text":"два"},{"dash":"long"},{"text":"три"}]},
{"id":"f-dash-in-b","rule":"F","input":"[b]раз -- два[/b]","surface":"any","expect":[{"tag":"b","children":[{"text":"раз "},{"dash":"short"},{"text":" два"}]}]},
{"id":"f-dash-in-code","rule":"F","input":"[code]раз -- два --- три[/code]","surface":"any","expect":[{"tag":"code","children":[{"text":"раз "},{"dash":"short"},{"text":" два "},{"dash":"long"},{"text":" три"}]}]},
{"id":"f-dash-in-noparse","rule":"F","input":"[noparse]раз -- два[/noparse]","surface":"any","expect":[{"tag":"noparse","children":[{"text":"раз -- два"}]}]},
{"id":"f-dash-in-value","rule":"F","input":"[link text=\"раз -- два\"]https://example.com/[/link]","surface":"any","expect":[{"tag":"link","params":{"href":"https://example.com/","text":"раз -- два"}}]},
{"id":"f-dash-author-edit","rule":"F","input":"раз -- два --- три","surface":"any","audience":"author_edit","expect":[{"source":"раз -- два --- три"}]},
{"id":"f-underscores-alone","rule":"F","input":"___","surface":"any","expect":[{"text":"___"}]},
{"id":"f-underscores-lines","rule":"F","input":"раз\n___\nдва","surface":"any","expect":[{"text":"раз\n___\nдва"}]},
{"id":"f-underscores-blank-lines","rule":"F","input":"раз\n\n___\n\nдва","surface":"any","expect":[{"text":"раз\n\n___\n\nдва"}]},
{"id":"f-underscores-in-line","rule":"F","input":"раз ___ два","surface":"any","expect":[{"text":"раз ___ два"}]},
{"id":"f-underscores-in-spoiler","rule":"F","input":"[spoiler]\n___\n[/spoiler]","surface":"any","expect":[{"tag":"spoiler","children":[{"text":"\n___\n"}]}]},
{"id":"f-underscores-then-dash","rule":"F","input":"___\nраз -- два","surface":"any","expect":[{"text":"___\nраз "},{"dash":"short"},{"text":" два"}]},
{"id":"f-underscores-author-edit","rule":"F","input":"раз\n___\nдва","surface":"GamePost","audience":"author_edit","expect":[{"source":"раз\n___\nдва"}]},
{"id":"f-underscores-author-edit-profile","rule":"F","input":"раз\n___\nдва","surface":"Profile","audience":"author_edit","expect":[{"source":"раз\n___\nдва"}]},
{"id":"g-cut","rule":"G","input":"раз[cut]два","surface":"any","expect":[{"text":"раз[cut]два"}]},
{"id":"g-cut-pair","rule":"G","input":"[cut]раз[/cut]","surface":"any","expect":[{"text":"[cut]раз[/cut]"}]},
{"id":"h-tip","rule":"H","input":"[tip:подсказка]текст[/tip]","surface":"any","expect":[{"text":"[tip:подсказка]текст[/tip]"}]},
{"id":"h-tipimg","rule":"H","input":"[tipimg:https://example.com/a.png]текст[/tipimg]","surface":"any","expect":[{"text":"[tipimg:https://example.com/a.png]текст[/tipimg]"}]},
{"id":"h-tip-content","rule":"H","input":"[tip text=\"подсказка\"]слово[/tip]","surface":"any","expect":[{"text":"[tip text=\"подсказка\"]слово[/tip]"}]},
{"id":"h-hint-tip-text","rule":"H","input":"[tip text=\"Политика, религия, национальные конфликты\"]спорные[/tip]","surface":"Hint","editor":true,"expect":[{"tag":"tip","params":{"text":"Политика, религия, национальные конфликты"},"children":[{"text":"спорные"}]}]},
{"id":"h-hint-tip-img","rule":"H","input":"[tip img=\"https://example.com/a.gif\"]слово[/tip]","surface":"Hint","editor":true,"expect":[{"tag":"tip","params":{"img":"https://example.com/a.gif"},"children":[{"text":"слово"}]}]},
{"id":"h-hint-tip-site-path","rule":"H","input":"[tip img=\"/images/erp-tooltip.gif\"]эротические сцены[/tip]","surface":"Hint","editor":true,"expect":[{"tag":"tip","params":{"img":"/images/erp-tooltip.gif"},"children":[{"text":"эротические сцены"}]}]},
{"id":"h-hint-tip-both","rule":"H","input":"[tip img=\"https://example.com/a.gif\" text=\"Подпись\"]слово[/tip]","surface":"Hint","editor":true,"expect":[{"tag":"tip","params":{"text":"Подпись","img":"https://example.com/a.gif"},"children":[{"text":"слово"}]}]},
{"id":"h-hint-tip-in-text","rule":"H","input":"Игра затрагивает [tip text=\"Политика, религия\"]спорные[/tip] темы","surface":"Hint","expect":[{"text":"Игра затрагивает "},{"tag":"tip","params":{"text":"Политика, религия"},"children":[{"text":"спорные"}]},{"text":" темы"}]},
{"id":"h-hint-tip-address-trimmed","rule":"H","input":"[tip img=\" /images/a.gif \"]слово[/tip]","surface":"Hint","expect":[{"tag":"tip","params":{"img":"/images/a.gif"},"children":[{"text":"слово"}]}]},
{"id":"h-hint-tip-upper","rule":"H","input":"[TIP TEXT=\"подсказка\"]слово[/Tip]","surface":"Hint","expect":[{"tag":"tip","params":{"text":"подсказка"},"children":[{"text":"слово"}]}]},
{"id":"h-hint-tip-no-params","rule":"H","input":"[tip]слово[/tip]","surface":"Hint","broken":"missing-param","expect":[{"text":"[tip]слово[/tip]"}]},
{"id":"h-hint-tip-empty-text","rule":"H","input":"[tip text=\"\"]слово[/tip]","surface":"Hint","broken":"missing-param","expect":[{"text":"[tip text=\"\"]слово[/tip]"}]},
{"id":"h-hint-tip-blank-text","rule":"H","input":"[tip text=\"  \"]слово[/tip]","surface":"Hint","broken":"missing-param","expect":[{"text":"[tip text=\"  \"]слово[/tip]"}]},
{"id":"h-hint-tip-unknown-param","rule":"H","input":"[tip src=\"https://example.com/a.gif\"]слово[/tip]","surface":"Hint","broken":"unknown-param","expect":[{"text":"[tip src=\"https://example.com/a.gif\"]слово[/tip]"}]},
{"id":"h-hint-tip-value-after-name","rule":"H","input":"[tip=\"подсказка\"]слово[/tip]","surface":"Hint","broken":"value-after-name","expect":[{"text":"[tip=\"подсказка\"]слово[/tip]"}]},
{"id":"h-hint-tip-no-scheme","rule":"H","input":"[tip img=\"images/erp-tooltip.gif\"]слово[/tip]","surface":"Hint","broken":"bad-address","expect":[{"text":"[tip img=\"images/erp-tooltip.gif\"]слово[/tip]"}]},
{"id":"h-hint-tip-other-host","rule":"H","input":"[tip img=\"//example.com/a.gif\"]слово[/tip]","surface":"Hint","broken":"bad-address","expect":[{"text":"[tip img=\"//example.com/a.gif\"]слово[/tip]"}]},
{"id":"h-hint-tip-backslash","rule":"H","input":"[tip img=\"/\\example.com/a.gif\"]слово[/tip]","surface":"Hint","broken":"bad-address","expect":[{"text":"[tip img=\"/\\example.com/a.gif\"]слово[/tip]"}]},
{"id":"h-hint-tip-space-in-path","rule":"H","input":"[tip img=\"/images/a b.gif\"]слово[/tip]","surface":"Hint","broken":"bad-address","expect":[{"text":"[tip img=\"/images/a b.gif\"]слово[/tip]"}]},
{"id":"h-hint-tip-root-alone","rule":"H","input":"[tip img=\"/\"]слово[/tip]","surface":"Hint","broken":"bad-address","expect":[{"text":"[tip img=\"/\"]слово[/tip]"}]},
{"id":"h-hint-tip-loopback","rule":"H","input":"[tip img=\"http://127.0.0.1/a.gif\"]слово[/tip]","surface":"Hint","broken":"bad-address","expect":[{"text":"[tip img=\"http://127.0.0.1/a.gif\"]слово[/tip]"}]},
{"id":"h-hint-tip-script","rule":"H","input":"[tip img=\"javascript:alert(1)\"]слово[/tip]","surface":"Hint","broken":"bad-address","expect":[{"text":"[tip img=\"javascript:alert(1)\"]слово[/tip]"}]},
{"id":"h-hint-tip-refused-with-text","rule":"H","input":"[tip text=\"Подпись\" img=\"images/a.gif\"]слово[/tip]","surface":"Hint","broken":"bad-address","expect":[{"text":"[tip text=\"Подпись\" img=\"images/a.gif\"]слово[/tip]"}]},
{"id":"h-hint-tip-in-tip","rule":"H","input":"[tip text=\"раз\"]а [tip text=\"два\"]б[/tip][/tip]","surface":"Hint","broken":"bad-content","expect":[{"text":"[tip text=\"раз\"]а "},{"tag":"tip","params":{"text":"два"},"children":[{"text":"б"}]},{"text":"[/tip]"}]},
{"id":"h-hint-tip-broken-in-tip","rule":"H","input":"[tip text=\"раз\"]а [tip]б[/tip][/tip]","surface":"Hint","broken":"bad-content","expect":[{"text":"[tip text=\"раз\"]а [tip]б[/tip][/tip]"}]},
{"id":"h-hint-tip-line-break","rule":"H","input":"[tip text=\"раз\"]сло\nво[/tip]","surface":"Hint","broken":"bad-content","expect":[{"text":"[tip text=\"раз\"]сло\nво[/tip]"}]},
{"id":"h-hint-tip-empty-word","rule":"H","input":"[tip text=\"раз\"] [/tip]","surface":"Hint","broken":"bad-content","expect":[{"text":"[tip text=\"раз\"] [/tip]"}]},
{"id":"h-hint-tip-unclosed","rule":"H","input":"[tip text=\"раз\"]слово","surface":"Hint","broken":"unclosed","expect":[{"text":"[tip text=\"раз\"]слово"}]},
{"id":"h-hint-tip-old-spelling","rule":"H","input":"[tip:подсказка]текст[/tip]","surface":"Hint","broken":"stray-closing","expect":[{"text":"[tip:подсказка]текст[/tip]"}]},
{"id":"h-hint-tipimg-old-spelling","rule":"H","input":"[tipimg:/images/erp-tooltip.gif]текст[/tipimg]","surface":"Hint","expect":[{"text":"[tipimg:/images/erp-tooltip.gif]текст[/tipimg]"}]},
{"id":"h-hint-post-tags-are-text","rule":"H","input":"[b]жирный[/b] и [img]https://example.com/a.png[/img]","surface":"Hint","expect":[{"text":"[b]жирный[/b] и [img]https://example.com/a.png[/img]"}]},
{"id":"h-hint-value-quotes","rule":"H","input":"[tip text=\"он сказал \"да\" & ушел <сразу>\"]a < b[/tip]","surface":"Hint","expect":[{"tag":"tip","params":{"text":"он сказал \"да\" & ушел <сразу>"},"children":[{"text":"a < b"}]}]},
{"id":"h-hint-noparse","rule":"H","input":"[noparse][tip text=\"подсказка\"]слово[/tip][/noparse]","surface":"Hint","expect":[{"tag":"noparse","children":[{"text":"[tip text=\"подсказка\"]слово[/tip]"}]}]},
{"id":"h-hint-private","rule":"H","input":"[private]секрет[/private]","surface":"Hint","broken":"private-outside-game","save":"refused","expect":[{"hidden":{"to":[],"seenBy":[]},"children":[{"text":"секрет"}]}]},
{"id":"h-hint-private-named","rule":"H","input":"до [private=\"Вася\"]секрет[/private] после","surface":"Hint","broken":"private-outside-game","save":"refused","expect":[{"text":"до "},{"hidden":{"to":["Вася"],"seenBy":[]},"children":[{"text":"секрет"}]},{"text":" после"}]},
{"id":"h-hint-private-example","rule":"H","input":"[noparse][private=\"Вася\"]пример[/private][/noparse]","surface":"Hint","expect":[{"tag":"noparse","children":[{"text":"[private=\"Вася\"]пример[/private]"}]}]},
{"id":"h-hint-line-breaks","rule":"H","input":"раз\nдва\n\nтри","surface":"Hint","expect":[{"text":"раз\nдва\n\nтри"}]},
{"id":"h-hint-dashes-as-typed","rule":"F","input":"раз -- два --- три","surface":"Hint","expect":[{"text":"раз -- два --- три"}]},
{"id":"h-hint-tip-dash-in-word","rule":"F","input":"[tip text=\"раз -- два\"]три -- четыре[/tip]","surface":"Hint","expect":[{"tag":"tip","params":{"text":"раз -- два"},"children":[{"text":"три -- четыре"}]}]},
{"id":"i-chat-image","rule":"I","input":"[img]https://example.com/a.png[/img]","surface":"GlobalChatMessage","expect":[{"tag":"img","params":{"src":"https://example.com/a.png"},"gate":"compact"}]},
{"id":"i-chat-image-params","rule":"I","input":"[img width=\"300\" height=\"200\"]https://example.com/a.png[/img]","surface":"GlobalChatMessage","expect":[{"tag":"img","params":{"src":"https://example.com/a.png","width":"300","height":"200"},"gate":"compact"}]},
{"id":"i-chat-image-in-spoiler","rule":"I","input":"[spoiler][img]https://example.com/a.png[/img][/spoiler]","surface":"GlobalChatMessage","expect":[{"tag":"spoiler","children":[{"tag":"img","params":{"src":"https://example.com/a.png"},"gate":"compact"}]}]},
{"id":"i-post-image","rule":"I","input":"[img]https://example.com/a.png[/img]","surface":"GamePost","expect":[{"tag":"img","params":{"src":"https://example.com/a.png"}}]},
{"id":"i-profile-image","rule":"I","input":"[img]https://example.com/a.png[/img]","surface":"Profile","expect":[{"tag":"img","params":{"src":"https://example.com/a.png"}}]},
{"id":"i-direct-image","rule":"I","input":"[img]https://example.com/a.png[/img]","surface":"DirectMessage","expect":[{"tag":"img","params":{"src":"https://example.com/a.png"}}]},
{"id":"i-chat-image-author-edit","rule":"I","input":"[img]https://example.com/a.png[/img]","surface":"GlobalChatMessage","audience":"author_edit","expect":[{"source":"[img]https://example.com/a.png[/img]"}]},
{"id":"j-link","rule":"J","input":"[link]https://example.com/[/link]","surface":"any","audience":"author_edit","expect":[{"source":"[link]https://example.com/[/link]"}]},
{"id":"j-link-text","rule":"J","input":"[link text=\"Главная\"]https://example.com/[/link]","surface":"any","audience":"author_edit","expect":[{"source":"[link text=\"Главная\"]https://example.com/[/link]"}]},
{"id":"j-img","rule":"J","input":"[img]https://example.com/a.png[/img]","surface":"Comment","audience":"author_edit","expect":[{"source":"[img]https://example.com/a.png[/img]"}]},
{"id":"j-img-all","rule":"J","input":"[img width=\"100\" height=\"50\"]https://example.com/a.png[/img]","surface":"Comment","audience":"author_edit","expect":[{"source":"[img width=\"100\" height=\"50\"]https://example.com/a.png[/img]"}]},
{"id":"j-img-height","rule":"J","input":"[img height=\"200\"]https://example.com/a.png[/img]","surface":"Comment","audience":"author_edit","expect":[{"source":"[img height=\"200\"]https://example.com/a.png[/img]"}]},
{"id":"j-img-alt-stays","rule":"J","input":"[img alt=\"кот\"]https://example.com/a.png[/img]","surface":"Comment","audience":"author_edit","broken":"unknown-param","expect":[{"source":"[img alt=\"кот\"]https://example.com/a.png[/img]"}]},
{"id":"j-code-markup","rule":"J","input":"[code][b]текст[/b][/code]","surface":"any","audience":"author_edit","expect":[{"source":"[code][b]текст[/b][/code]"}]},
{"id":"j-code-private","rule":"J","input":"[code][private=\"Вася\"]секрет[/private][/code]","surface":"GamePost","audience":"author_edit","expect":[{"source":"[code][private=\"Вася\"]секрет[/private][/code]"}]},
{"id":"j-spoiler","rule":"J","input":"[spoiler]текст[/spoiler]","surface":"any","audience":"author_edit","expect":[{"source":"[spoiler]текст[/spoiler]"}]},
{"id":"j-spoiler-text","rule":"J","input":"[spoiler text=\"Подпись кнопки\"]текст[/spoiler]","surface":"any","audience":"author_edit","expect":[{"source":"[spoiler text=\"Подпись кнопки\"]текст[/spoiler]"}]},
{"id":"j-private","rule":"J","input":"[private=\"Вася, Петя\"]секрет[/private]","surface":"GamePost","audience":"author_edit","expect":[{"source":"[private=\"Вася, Петя\"]секрет[/private]"}]},
{"id":"j-private-bare","rule":"J","input":"[private]секрет[/private]","surface":"GamePost","audience":"author_edit","expect":[{"source":"[private]секрет[/private]"}]},
{"id":"j-mention","rule":"J","input":"[mention]Вася[/mention]","surface":"any","audience":"author_edit","expect":[{"source":"[mention]Вася[/mention]"}]},
{"id":"j-broken-stays","rule":"J","input":"[b=1]текст[/b]","surface":"any","audience":"author_edit","broken":"value-after-name","expect":[{"source":"[b=1]текст[/b]"}]},
{"id":"j-quote-author-stays","rule":"J","input":"[quote=\"Имя\"]текст[/quote]","surface":"any","audience":"author_edit","broken":"value-after-name","expect":[{"source":"[quote=\"Имя\"]текст[/quote]"}]},
{"id":"j-line-breaks-with-cr","rule":"J","input":"раз\r\nдва\rтри","surface":"any","audience":"author_edit","expect":[{"source":"раз\nдва\nтри"}]},
{"id":"j-blocks-side-by-side","rule":"J","input":"[quote]раз[/quote][quote]два[/quote]","surface":"any","audience":"author_edit","expect":[{"source":"[quote]раз[/quote][quote]два[/quote]"}]},
{"id":"j-list-text-between-items","rule":"J","input":"[ul]раз[li]два[/li]три[/ul]","surface":"any","audience":"author_edit","expect":[{"source":"[ul]раз[li]два[/li]три[/ul]"}]},
{"id":"j-list-lines-between-items","rule":"J","input":"[ul]\n[li]раз[/li]\n[/ul]","surface":"any","audience":"author_edit","expect":[{"source":"[ul]\n[li]раз[/li]\n[/ul]"}]},
{"id":"j-list-of-text","rule":"J","input":"[ol]раз[/ol]","surface":"any","audience":"author_edit","expect":[{"source":"[ol]раз[/ol]"}]},
{"id":"j-item-in-item","rule":"J","input":"[li]раз[li]два[/li]три[/li]","surface":"any","audience":"author_edit","expect":[{"source":"[li]раз[li]два[/li]три[/li]"}]},
{"id":"j-spoiler-empty-caption","rule":"J","input":"[spoiler text=\"\"]текст[/spoiler]","surface":"any","audience":"author_edit","expect":[{"source":"[spoiler text=\"\"]текст[/spoiler]"}]},
{"id":"j-line-after-blocks","rule":"J","input":"перед\n[private=\"Вася\"]секрет[/private]\nпосле\n[quote]цитата[/quote]\nконец","surface":"GamePost","audience":"author_edit","expect":[{"source":"перед\n[private=\"Вася\"]секрет[/private]\nпосле\n[quote]цитата[/quote]\nконец"}]},
{"id":"k-b-upper","rule":"K","input":"[B]текст[/B]","surface":"any","expect":[{"tag":"b","children":[{"text":"текст"}]}]},
{"id":"k-spoiler-capitalised","rule":"K","input":"[Spoiler]текст[/Spoiler]","surface":"any","expect":[{"tag":"spoiler","children":[{"text":"текст"}]}]},
{"id":"k-img-upper","rule":"K","input":"[IMG]https://example.com/a.png[/IMG]","surface":"Comment","expect":[{"tag":"img","params":{"src":"https://example.com/a.png"}}]},
{"id":"k-link-upper","rule":"K","input":"[LINK]https://example.com/[/LINK]","surface":"any","expect":[{"tag":"link","params":{"href":"https://example.com/","text":"ссылка"}}]},
{"id":"k-mention-upper","rule":"K","input":"[MENTION]Вася[/MENTION]","surface":"any","expect":[{"tag":"mention","params":{"login":"Вася"}}]},
{"id":"k-code-upper","rule":"K","input":"[CODE][b]текст[/b][/CODE]","surface":"any","expect":[{"tag":"code","children":[{"tag":"b","children":[{"text":"текст"}]}]}]},
{"id":"k-noparse-upper","rule":"K","input":"[NOPARSE][b]текст[/b][/NOPARSE]","surface":"any","expect":[{"tag":"noparse","children":[{"text":"[b]текст[/b]"}]}]},
{"id":"k-noparse-closing-mixed","rule":"K","input":"[noparse][b]текст[/b][/NoParse]","surface":"any","expect":[{"tag":"noparse","children":[{"text":"[b]текст[/b]"}]}]},
{"id":"k-closing-upper","rule":"K","input":"[b]текст[/B][/b]","surface":"any","broken":"stray-closing","expect":[{"tag":"b","children":[{"text":"текст"}]},{"text":"[/b]"}]},
{"id":"k-param-upper","rule":"K","input":"[img WIDTH=\"300\"]https://example.com/a.png[/img]","surface":"Comment","expect":[{"tag":"img","params":{"src":"https://example.com/a.png","width":"300"}}]},
{"id":"k-mixed-pair","rule":"K","input":"[Quote]текст[/QUOTE]","surface":"any","expect":[{"tag":"quote","children":[{"text":"текст"}]}]},
{"id":"k-params-mixed","rule":"K","input":"[IMG Width=\"300\" Height=\"200\"]https://example.com/a.png[/img]","surface":"Comment","expect":[{"tag":"img","params":{"src":"https://example.com/a.png","width":"300","height":"200"}}]},
{"id":"k-param-repeated-in-other-case","rule":"K","input":"[img width=\"300\" WIDTH=\"400\"]https://example.com/a.png[/img]","surface":"Comment","broken":"repeated-param","expect":[{"text":"[img width=\"300\" WIDTH=\"400\"]https://example.com/a.png[/img]"}]},
{"id":"k-kelvin","rule":"K","input":"[lin\u212A]https://example.com/[/lin\u212A]","surface":"any","expect":[{"text":"[lin\u212A]https://example.com/[/lin\u212A]"}]},
{"id":"k-name-then-ideographic-space","rule":"K","input":"[quote]раз [quote\u3000]два[/quote] три[/quote]","surface":"any","broken":"stray-space","expect":[{"tag":"quote","children":[{"text":"раз [quote\u3000]два[/quote] три"}]}]},
{"id":"l-name-then-spaces","rule":"L","input":"[img                                        x","surface":"Comment","expect":[{"text":"[img                                        x"}]},
{"id":"l-repeated-pairs","rule":"L","input":"[img a=\"1\" a=\"1\" a=\"1\" a=\"1\" x","surface":"Comment","expect":[{"text":"[img a=\"1\" a=\"1\" a=\"1\" a=\"1\" x"}]},
{"id":"l-repeated-openings","rule":"L","input":"[spoiler text=\"[spoiler text=\"[spoiler text=\"x","surface":"any","expect":[{"text":"[spoiler text=\"[spoiler text=\"[spoiler text=\"x"}]},
{"id":"l-many-quotes","rule":"L","input":"[spoiler text=\"\"\"\"\"\"\"\" x","surface":"any","expect":[{"text":"[spoiler text=\"\"\"\"\"\"\"\" x"}]},
{"id":"n-stray-closing","rule":"N","input":"закрывается так: [/b] - и все","surface":"any","broken":"stray-closing","expect":[{"text":"закрывается так: [/b] - и все"}]},
{"id":"n-stray-closing-inside","rule":"N","input":"[i]раз[/b] два[/i]","surface":"any","broken":"stray-closing","expect":[{"tag":"i","children":[{"text":"раз[/b] два"}]}]},
{"id":"n-stray-closing-private","rule":"N","input":"текст[/private] хвост","surface":"GamePost","broken":"stray-closing","save":"refused","expect":[{"text":"текст[/private] хвост"}]},
{"id":"n-unclosed-b","rule":"N","input":"[b]текст","surface":"any","broken":"unclosed","expect":[{"text":"[b]текст"}]},
{"id":"n-unclosed-b-content","rule":"N","input":"[b]жирный [i]курсив[/i]","surface":"any","broken":"unclosed","expect":[{"text":"[b]жирный "},{"tag":"i","children":[{"text":"курсив"}]}]},
{"id":"n-unclosed-quote","rule":"N","input":"Он сказал: [quote]я приду завтра\nА я не поверил.","surface":"any","broken":"unclosed","expect":[{"text":"Он сказал: [quote]я приду завтра\nА я не поверил."}]},
{"id":"n-unclosed-spoiler","rule":"N","input":"[spoiler text=\"Подпись\"]текст","surface":"any","broken":"unclosed","expect":[{"text":"[spoiler text=\"Подпись\"]текст"}]},
{"id":"n-unclosed-list","rule":"N","input":"[ul][li]раз[/li][li]два[/li]","surface":"any","broken":"unclosed","expect":[{"text":"[ul]"},{"tag":"li","children":[{"text":"раз"}]},{"tag":"li","children":[{"text":"два"}]}]},
{"id":"n-unclosed-inner","rule":"N","input":"[quote]раз [b]два[/quote]","surface":"any","broken":"unclosed","expect":[{"tag":"quote","children":[{"text":"раз [b]два"}]}]},
{"id":"n-unclosed-code","rule":"N","input":"[code][b]текст[/b]","surface":"any","broken":"unclosed","expect":[{"text":"[code]"},{"tag":"b","children":[{"text":"текст"}]}]},
{"id":"n-unclosed-noparse","rule":"N","input":"[noparse][b]текст[/b]","surface":"any","broken":"unclosed","expect":[{"text":"[noparse]"},{"tag":"b","children":[{"text":"текст"}]}]},
{"id":"n-unclosed-code-dash","rule":"N","input":"[code]раз -- два","surface":"any","broken":"unclosed","expect":[{"text":"[code]раз "},{"dash":"short"},{"text":" два"}]},
{"id":"n-unclosed-noparse-dash","rule":"N","input":"[noparse]раз -- два","surface":"any","broken":"unclosed","expect":[{"text":"[noparse]раз "},{"dash":"short"},{"text":" два"}]},
{"id":"n-unclosed-code-private","rule":"N","input":"[code][b]жирный[/b] [private=\"Вася\"]секрет[/private]","surface":"GamePost","broken":"unclosed","expect":[{"text":"[code]"},{"tag":"b","children":[{"text":"жирный"}]},{"text":" "},{"hidden":{"to":["Вася"],"seenBy":["author","leads","addressees","overrides"]},"children":[{"text":"секрет"}]}]},
{"id":"n-unclosed-noparse-private","rule":"N","input":"[noparse][b]жирный[/b] [private=\"Вася\"]секрет[/private]","surface":"GamePost","broken":"private-spelling","save":"refused","expect":[{"text":"[noparse]"},{"tag":"b","children":[{"text":"жирный"}]},{"text":" "},{"hidden":{"to":["Вася"],"seenBy":["author","leads","addressees","overrides"]},"children":[{"text":"секрет"}]}]},
{"id":"n-code-crossing","rule":"N","input":"[code][b]раз[/code] два[/b]","surface":"any","broken":"unclosed","expect":[{"tag":"code","children":[{"text":"[b]раз"}]},{"text":" два[/b]"}]},
{"id":"n-noparse-in-noparse","rule":"N","input":"[noparse]раз [noparse]два[/noparse] три[/noparse]","surface":"any","broken":"stray-closing","expect":[{"tag":"noparse","children":[{"text":"раз [noparse]два"}]},{"text":" три[/noparse]"}]},
{"id":"n-noparse-takes-parent-closing","rule":"N","input":"[quote][noparse]раз[/quote] два[/noparse]","surface":"any","broken":"unclosed","expect":[{"text":"[quote]"},{"tag":"noparse","children":[{"text":"раз[/quote] два"}]}]},
{"id":"n-noparse-takes-code-closing","rule":"N","input":"[code][noparse]раз[/code] два[/noparse]","surface":"any","broken":"unclosed","expect":[{"text":"[code]"},{"tag":"noparse","children":[{"text":"раз[/code] два"}]}]},
{"id":"n-stray-closing-noparse","rule":"N","input":"пример [/noparse] и все","surface":"any","broken":"stray-closing","expect":[{"text":"пример [/noparse] и все"}]},
{"id":"n-stray-closing-sealed","rule":"N","input":"[b]раз [private]два[/b] три[/private][/b]","surface":"GamePost","broken":"stray-closing","expect":[{"tag":"b","children":[{"text":"раз "},{"hidden":{"to":[],"seenBy":["author","leads"]},"children":[{"text":"два[/b] три"}]}]}]},
{"id":"o-bidi-text","rule":"O","input":"а\u202Aб\u202Bв\u202Cг\u202Dд\u202Eе\u2066ж\u2067з\u2068и\u2069к","surface":"any","expect":[{"text":"абвгдежзик"}]},
{"id":"o-bidi-in-name","rule":"O","input":"[b\u202E]текст[/b]","surface":"any","expect":[{"tag":"b","children":[{"text":"текст"}]}]},
{"id":"o-bidi-in-value","rule":"O","input":"[private=\"Ва\u2067ся\"]секрет[/private]","surface":"GamePost","expect":[{"hidden":{"to":["Вася"],"seenBy":["author","leads","addressees","overrides"]},"children":[{"text":"секрет"}]}]},
{"id":"o-control-in-closing","rule":"O","input":"[private=\"Вася\"]секрет[/priv\u0001ate] хвост","surface":"GamePost","expect":[{"hidden":{"to":["Вася"],"seenBy":["author","leads","addressees","overrides"]},"children":[{"text":"секрет"}]},{"text":" хвост"}]},
{"id":"o-control-in-code","rule":"O","input":"[co\u0001de][private=\"Вася\"]пример[/private][/code]","surface":"GamePost","expect":[{"tag":"code","children":[{"hidden":{"to":["Вася"],"seenBy":["author","leads","addressees","overrides"]},"children":[{"text":"пример"}]}]}]},
{"id":"o-control-in-noparse","rule":"O","input":"[nopa\u0001rse][private=\"Вася\"]пример[/private][/noparse]","surface":"GamePost","expect":[{"tag":"noparse","children":[{"text":"[private=\"Вася\"]пример[/private]"}]}]},
{"id":"o-c1-control-in-name","rule":"O","input":"[b\u0085]текст[/b\u009F]","surface":"any","expect":[{"tag":"b","children":[{"text":"текст"}]}]},
{"id":"o-no-break-space-kept","rule":"O","input":"раз\u00A0два","surface":"any","expect":[{"text":"раз\u00A0два"}]},
{"id":"p-code-empty","rule":"P","input":"[code][/code]","surface":"any","expect":[{"tag":"code"}]},
{"id":"p-noparse-empty","rule":"P","input":"[noparse][/noparse]","surface":"any","expect":[{"tag":"noparse"}]},
{"id":"p-code-in-code","rule":"P","input":"[code]раз [code]два[/code] три[/code]","surface":"any","expect":[{"tag":"code","children":[{"text":"раз "},{"tag":"code","children":[{"text":"два"}]},{"text":" три"}]}]},
{"id":"p-code-around-quote","rule":"P","input":"[code][quote]цитата[/quote][/code]","surface":"any","expect":[{"tag":"code","children":[{"tag":"quote","children":[{"text":"цитата"}]}]}]},
{"id":"p-code-around-list","rule":"P","input":"[code][ul][li]раз[/li][li]два[/li][/ul][/code]","surface":"any","expect":[{"tag":"code","children":[{"tag":"ul","children":[{"tag":"li","children":[{"text":"раз"}]},{"tag":"li","children":[{"text":"два"}]}]}]}]},
{"id":"p-code-around-spoiler","rule":"P","input":"[code][spoiler]текст[/spoiler] хвост[/code]","surface":"any","expect":[{"tag":"code","children":[{"tag":"spoiler","children":[{"text":"текст"}]},{"text":" хвост"}]}]},
{"id":"p-noparse-in-code","rule":"P","input":"[code][noparse][b]текст[/b][/noparse][/code]","surface":"any","expect":[{"tag":"code","children":[{"tag":"noparse","children":[{"text":"[b]текст[/b]"}]}]}]},
{"id":"p-code-in-noparse","rule":"P","input":"[noparse][code][b]текст[/b][/code][/noparse]","surface":"any","expect":[{"tag":"noparse","children":[{"text":"[code][b]текст[/b][/code]"}]}]},
{"id":"p-noparse-holds-code-closing","rule":"P","input":"[code]раз [noparse][/code][/noparse] два[/code]","surface":"any","expect":[{"tag":"code","children":[{"text":"раз "},{"tag":"noparse","children":[{"text":"[/code]"}]},{"text":" два"}]}]},
{"id":"p-code-left-open-around-noparse","rule":"P","input":"[code]раз [noparse][b][/noparse]","surface":"any","broken":"unclosed","expect":[{"text":"[code]раз "},{"tag":"noparse","children":[{"text":"[b]"}]}]},
{"id":"p-noparse-in-spoiler","rule":"P","input":"[spoiler][noparse][/spoiler][/noparse][/spoiler]","surface":"any","expect":[{"tag":"spoiler","children":[{"tag":"noparse","children":[{"text":"[/spoiler]"}]}]}]},
{"id":"p-noparse-in-quote","rule":"P","input":"[quote][noparse][b]жирный[/b][/noparse][/quote]","surface":"any","expect":[{"tag":"quote","children":[{"tag":"noparse","children":[{"text":"[b]жирный[/b]"}]}]}]},
{"id":"p-noparse-in-list","rule":"P","input":"[ul][li][noparse][li][/noparse][/li][/ul]","surface":"any","expect":[{"tag":"ul","children":[{"tag":"li","children":[{"tag":"noparse","children":[{"text":"[li]"}]}]}]}]},
{"id":"p-noparse-in-link","rule":"P","input":"[link][noparse]https://example.com/[/noparse][/link]","surface":"any","broken":"bad-content","expect":[{"text":"[link]"},{"tag":"noparse","children":[{"text":"https://example.com/"}]},{"text":"[/link]"}]},
{"id":"p-noparse-in-image","rule":"P","input":"[img][noparse]https://example.com/a.png[/noparse][/img]","surface":"Comment","broken":"bad-content","expect":[{"text":"[img]"},{"tag":"noparse","children":[{"text":"https://example.com/a.png"}]},{"text":"[/img]"}]},
{"id":"p-links-in-noparse","rule":"P","input":"[noparse][img]https://example.com/a.png[/img] [link]https://example.com/[/link][/noparse]","surface":"Comment","expect":[{"tag":"noparse","children":[{"text":"[img]https://example.com/a.png[/img] [link]https://example.com/[/link]"}]}]},
{"id":"p-noparse-silences-the-check","rule":"P","input":"[noparse][b]раз [/i] [tab=1] [img width=300]адрес[/img] [private=Вася] [spoiler text=\"a[b\"][/noparse]","surface":"GamePost","expect":[{"tag":"noparse","children":[{"text":"[b]раз [/i] [tab=1] [img width=300]адрес[/img] [private=Вася] [spoiler text=\"a[b\"]"}]}]}
] /* grammar:end */;
