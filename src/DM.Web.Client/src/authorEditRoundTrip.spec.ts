import { describe, it, expect } from "vitest";
import { readdirSync, readFileSync, statSync } from "node:fs";
import { join, dirname, relative } from "node:path";
import { fileURLToPath } from "node:url";
import { htmlToBbcode } from "@/shared/lib/utils/bbcode";

/**
 * An AuthorEdit response reaches the editor through htmlToBbcode.
 *
 * Both halves of the author-edit round trip are stated in the glossary: the
 * server returns HTML carrying data-bb-* attributes, the client sends BBCode
 * back. BbConverter performs the first half — it calls RenderHtml for every
 * audience except plain text. htmlToBbcode performs the second, and performing
 * it is the caller's job: BBCodeEditor takes and returns BBCode, and escapes
 * HTML handed to it.
 *
 * Skipping the conversion does not break loudly. The author sees markup instead
 * of text, the save still succeeds, and a private block that lost its wrapper is
 * published to every reader — the server escapes it on the way out like any
 * other text. This has happened once already: third-audit finding security-01
 * was closed by no longer seeding the editor from the Display render, but the
 * reverse conversion was added in one place out of eight, and the next edit went
 * the same way.
 *
 * The rule reads source text because nothing else proves it: each of the eight
 * call sites has its own screen, its own store and its own form, and the only
 * thing they share is the audience they ask for.
 */

const HERE = dirname(fileURLToPath(import.meta.url));

/** An API client call that declares the author's own audience. */
const AUTHOR_EDIT_CALL = /RENDER_AUDIENCE\.AuthorEdit/;

/** A method declared on an API client class. */
const PUBLIC_METHOD = /^\s*public\s+([a-zA-Z][a-zA-Z0-9]*)\s*\(/;

function sourceFiles(dir: string, out: string[] = []): string[] {
  for (const entry of readdirSync(dir)) {
    if (entry === "node_modules" || entry === "dist" || entry === "coverage")
      continue;
    const full = join(dir, entry);
    if (statSync(full).isDirectory()) sourceFiles(full, out);
    else if (/\.(ts|vue)$/.test(entry) && !/\.spec\.ts$/.test(entry))
      out.push(full);
  }
  return out;
}

/** API client methods that ask for the author's own audience. */
function authorEditMethods(): string[] {
  const found = new Set<string>();
  for (const file of sourceFiles(join(HERE, "entities"))) {
    if (!/api[\\/][a-zA-Z]+Api\.ts$/.test(file)) continue;
    const lines = readFileSync(file, "utf8").split("\n");
    let current: string | null = null;
    for (const line of lines) {
      const declared = PUBLIC_METHOD.exec(line);
      if (declared) current = declared[1];
      if (current && AUTHOR_EDIT_CALL.test(line)) {
        found.add(current);
        current = null;
      }
    }
  }
  return [...found];
}

describe("author-edit round trip", () => {
  const methods = authorEditMethods();

  it("finds the API methods that ask for the author's own rendering", () => {
    // A rule over an empty set passes, and the set is discovered by walking the
    // tree: renaming the wrapper must not leave this green over nothing.
    expect(methods.length).toBeGreaterThanOrEqual(6);
  });

  it("passes every response through htmlToBbcode before the editor sees it", () => {
    const offenders: string[] = [];

    for (const file of sourceFiles(HERE)) {
      const text = readFileSync(file, "utf8");
      // API client files declare these methods rather than consuming them.
      if (/api[\\/][a-zA-Z]+Api\.ts$/.test(file)) continue;

      // Consuming, not merely naming: a comment list hands the call down to a
      // child as a function (`const fetchEditSource = (id) => api.method(id)`)
      // and never sees the response — whoever awaits it unwraps it. Awaiting is
      // the marker that separates the reader from the forwarder.
      const calls = methods.filter((m) =>
        new RegExp("await\\s+[\\w.]*\\b" + m + "\\s*\\(").test(text),
      );
      if (!calls.length) continue;
      if (text.includes("htmlToBbcode")) continue;

      offenders.push(relative(HERE, file) + " -> " + calls.join(", "));
    }

    expect(offenders).toEqual([]);
  });

  /**
   * And declares the envelope it actually receives.
   *
   * Every single-resource answer of this API is `{ "resource": ... }`
   * (API_DESIGN.md: "Envelope<T> - стандарт для всех одиночных ресурсов"), and
   * the axios layer hands the body over untouched - unwrapResource is the
   * caller's job. A method declared with the bare payload type therefore lies
   * about its own answer, and the lie is silent: the field the editor reads is
   * undefined, the seeding falls through to whatever it had, and the editor
   * opens on the Display render or on nothing at all.
   *
   * That is not hypothetical. Three of these eight were declared bare: the game
   * post, the private message and the global chat message. The post one
   * published [private] text to the whole room on the first save, because what
   * the editor kept instead was the Display render, where the block is already
   * flattened into ordinary markup.
   *
   * The rule reads source text for the same reason the rule above does: the
   * declared type is the only thing these eight call sites share, and there is
   * nothing for TypeScript to check it against - the type argument is written
   * by hand and the server is not in the compilation.
   */
  it("declares the envelope the server actually sends", () => {
    const ENVELOPE =
      /Api\.(get|post|patch|put)<(Envelope|ListEnvelope|CursorEnvelope)</;
    const CALL = /Api\.(get|post|patch|put)</;
    const offenders: string[] = [];

    for (const file of sourceFiles(join(HERE, "entities"))) {
      if (!/api[\\/][a-zA-Z]+Api\.ts$/.test(file)) continue;
      const lines = readFileSync(file, "utf8").split("\n");

      for (let i = 0; i < lines.length; i++) {
        if (!AUTHOR_EDIT_CALL.test(lines[i])) continue;

        // The audience is an argument of the call, so the declaration is on
        // this line or a few above it - the formatter breaks the call across a
        // handful of lines at most.
        const opening = lines
          .slice(Math.max(0, i - 6), i + 1)
          .reverse()
          .find((line) => CALL.test(line));
        if (opening && ENVELOPE.test(opening)) continue;

        offenders.push(
          relative(HERE, file) +
            ":" +
            (i + 1) +
            " -> " +
            (opening ?? "?").trim(),
        );
      }
    }

    expect(offenders).toEqual([]);
  });
});

/**
 * And htmlToBbcode reads back every tag the server renders for the author.
 *
 * The rows are what the server's author-edit parsers render, and they are the
 * server's half of the pair as well: BbAuthorEditRoundTripShould renders each
 * sample on every surface and fails when a row here is not the markup the
 * server sends, or when a tag of a surface has no sample at all. Nothing held
 * the two sides together before, and they drifted without failing anything:
 * [quote] reached the editor as the raw markup it was rendered to, [noparse]
 * lost its wrapper and the example inside became live markup on the next save,
 * and the head of a [spoiler] was saved as a line of text above the block, one
 * more of them per edit.
 *
 * Each row is the BBCode as htmlToBbcode writes it, and the server's markup for
 * it; a spelling the server reads the same way but the editor writes
 * differently would fail here for the wrong reason.
 */
const SERVER_AUTHOR_EDIT: Array<[bbcode: string, html: string]> = [
  [
    "[b]текст[/b]",
    '<span data-bb-breaks="written"></span><strong>текст</strong>',
  ],
  ["[i]текст[/i]", '<span data-bb-breaks="written"></span><em>текст</em>'],
  ["[u]текст[/u]", '<span data-bb-breaks="written"></span><u>текст</u>'],
  [
    "[strike]текст[/strike]",
    '<span data-bb-breaks="written"></span><s>текст</s>',
  ],
  [
    "[spoiler]текст[/spoiler]",
    '<span data-bb-breaks="written"></span><div class="spoiler" data-bb-tag="spoiler">текст</div>',
  ],
  [
    "[nsfw]текст[/nsfw]",
    '<span data-bb-breaks="written"></span><div class="nsfw-spoiler" data-bb-tag="nsfw">текст</div>',
  ],
  [
    "[quote]текст[/quote]",
    '<span data-bb-breaks="written"></span><div class="quote" data-bb-tag="quote">текст</div>',
  ],
  [
    "[ul][li]раз[/li][li]два[/li][/ul]",
    '<span data-bb-breaks="written"></span><div data-bb-tag="ul"><div data-bb-tag="li">раз</div><div data-bb-tag="li">два</div></div>',
  ],
  [
    "[ol][li]раз[/li][/ol]",
    '<span data-bb-breaks="written"></span><div data-bb-tag="ol"><div data-bb-tag="li">раз</div></div>',
  ],
  [
    "раз[tab]два",
    '<span data-bb-breaks="written"></span>раз<span data-bb-tag="tab">&nbsp;&nbsp;&nbsp;</span>два',
  ],
  // [code] is a monospace font and nothing else, as on DM2: an element in
  // the line that the markup inside it is drawn in, and the editor reads
  // <code> back as [code] without a marker.
  [
    "[code]a < b[/code]",
    '<span data-bb-breaks="written"></span><code class="code">a &lt; b</code>',
  ],
  [
    "[code][b]текст[/b][/code]",
    '<span data-bb-breaks="written"></span><code class="code"><strong>текст</strong></code>',
  ],
  [
    '[code][private="Вася"]секрет[/private][/code]',
    '<span data-bb-breaks="written"></span><code class="code"><span class="private-message" data-bb-tag="private" data-bb-addressees="Вася">секрет</span></code>',
  ],
  [
    "[noparse][b]текст[/b][/noparse]",
    '<span data-bb-breaks="written"></span><span data-bb-tag="noparse">[b]текст[/b]</span>',
  ],
  [
    "[mod]текст[/mod]",
    '<span data-bb-breaks="written"></span><div class="mod-block" data-bb-tag="mod">текст</div>',
  ],
  [
    '[private="Вася"]текст[/private]',
    '<span data-bb-breaks="written"></span><span class="private-message" data-bb-tag="private" data-bb-addressees="Вася">текст</span>',
  ],
  [
    "[private]текст[/private]",
    '<span data-bb-breaks="written"></span><span class="private-message" data-bb-tag="private" data-bb-addressees="">текст</span>',
  ],
  [
    '[private="Вася, Петя"]секрет[/private]',
    '<span data-bb-breaks="written"></span><span class="private-message" data-bb-tag="private" data-bb-addressees="Вася, Петя">секрет</span>',
  ],
  [
    "[private]секрет[/private]",
    '<span data-bb-breaks="written"></span><span class="private-message" data-bb-tag="private" data-bb-addressees="">секрет</span>',
  ],
  [
    "[img]https://example.com/a.png[/img]",
    '<span data-bb-breaks="written"></span><img src="https://example.com/a.png" class="bb-image" data-bb-tag="img" loading="lazy" decoding="async" referrerpolicy="no-referrer" alt="" />',
  ],
  [
    '[img width="100" height="50"]https://example.com/a.png[/img]',
    '<span data-bb-breaks="written"></span><span class="bb-image-frame" data-bb-width="100" data-bb-height="50" style="--bb-image-max-width:100px;--bb-image-max-height:50px"><img src="https://example.com/a.png" class="bb-image" data-bb-tag="img" loading="lazy" decoding="async" referrerpolicy="no-referrer" alt="" /></span>',
  ],
  [
    '[img height="50"]https://example.com/a.png[/img]',
    '<span data-bb-breaks="written"></span><span class="bb-image-frame" data-bb-height="50" style="--bb-image-max-height:50px"><img src="https://example.com/a.png" class="bb-image" data-bb-tag="img" loading="lazy" decoding="async" referrerpolicy="no-referrer" alt="" /></span>',
  ],
  [
    '[img height="200"]https://example.com/a.png[/img]',
    '<span data-bb-breaks="written"></span><span class="bb-image-frame" data-bb-height="200" style="--bb-image-max-height:200px"><img src="https://example.com/a.png" class="bb-image" data-bb-tag="img" loading="lazy" decoding="async" referrerpolicy="no-referrer" alt="" /></span>',
  ],
  // An image has no alt, and a tag with one is a broken tag: the author gets
  // it back as the text it was typed as, to see it and take the alt out.
  [
    '[img alt="подпись"]https://example.com/a.png[/img]',
    '<span data-bb-breaks="written"></span>[img alt="подпись"]https://example.com/a.png[/img]',
  ],
  [
    '[img alt="кот"]https://example.com/a.png[/img]',
    '<span data-bb-breaks="written"></span>[img alt="кот"]https://example.com/a.png[/img]',
  ],
  [
    "[link]https://example.com/[/link]",
    '<span data-bb-breaks="written"></span><a href="https://example.com/" target="_blank" rel="noopener">ссылка</a>',
  ],
  [
    '[link text="текст"]https://example.com/[/link]',
    '<span data-bb-breaks="written"></span><a href="https://example.com/" target="_blank" rel="noopener">текст</a>',
  ],
  [
    '[link text="Главная"]https://example.com/[/link]',
    '<span data-bb-breaks="written"></span><a href="https://example.com/" target="_blank" rel="noopener">Главная</a>',
  ],
  // The caption rides in data-bb-text, on the block the editor reads, and not
  // in the head the reader clicks.
  [
    '[spoiler text="подпись"]текст[/spoiler]',
    '<span data-bb-breaks="written"></span><div class="spoiler" data-bb-tag="spoiler" data-bb-text="подпись">текст</div>',
  ],
  [
    '[spoiler text="Подпись кнопки"]текст[/spoiler]',
    '<span data-bb-breaks="written"></span><div class="spoiler" data-bb-tag="spoiler" data-bb-text="Подпись кнопки">текст</div>',
  ],
  [
    "[mention]Вася[/mention]",
    '<span data-bb-breaks="written"></span><a class="bb-mention" href="/users/%D0%92%D0%B0%D1%81%D1%8F" data-bb-tag="mention" data-bb-user="Вася">@Вася</a>',
  ],
  // Nested, and with text after the inner block. Every pattern of a block stops
  // at the first closing tag it meets, so an outer block converted before the
  // inner one ends on the inner one's tag and the two come back crossed, as
  // "[spoiler][mod]текст[/spoiler] хвост[/mod]" — which moves the tail out of
  // the spoiler for the reader, and into the private block in the row below.
  [
    "[spoiler][mod]текст[/mod] хвост[/spoiler]",
    '<span data-bb-breaks="written"></span><div class="spoiler" data-bb-tag="spoiler"><div class="mod-block" data-bb-tag="mod">текст</div> хвост</div>',
  ],
  [
    '[spoiler][private="Вася"]текст[/private] хвост[/spoiler]',
    '<span data-bb-breaks="written"></span><div class="spoiler" data-bb-tag="spoiler"><span class="private-message" data-bb-tag="private" data-bb-addressees="Вася">текст</span> хвост</div>',
  ],
  // Text as it was typed. The reading typography has no way back: "--" and
  // "---" reached the editor as dash entities, and htmlToBbcode leaves them in
  // the text, so the author's next save wrote "&ndash;" in place of what was
  // typed. A line of "___" is text and nothing else. The line break stays a
  // <br />, except in private messages, which keep it as typed on display as
  // well.
  ["раз -- два", '<span data-bb-breaks="written"></span>раз -- два'],
  ["раз --- два", '<span data-bb-breaks="written"></span>раз --- два'],
  [
    "раз -- два --- три",
    '<span data-bb-breaks="written"></span>раз -- два --- три',
  ],
  ["раз\n___", '<span data-bb-breaks="written"></span>раз<br />___'],
  ["раз\n___", "раз\n___"],
  [
    "раз\n___\nдва",
    '<span data-bb-breaks="written"></span>раз<br />___<br />два',
  ],
  // Found by the generated checks (bbcodeProperties.spec.ts). Every line break
  // of the server's markup is a <br />, and the marker in front of it says so:
  // two blocks side by side were written side by side, and read by the visual
  // mode's rule they came back with a line break between them, one more on
  // every edit. A list is the tags as written, in elements the browser keeps
  // where they stand: the text between its items, the line breaks between
  // them, a list of text alone and an item inside an item all come back as
  // typed. An empty caption is still a caption the author wrote. A CR, alone or
  // before an LF, is one line break (BbAuthorEditRoundTripShould).
  [
    "[quote]раз[/quote][quote]два[/quote]",
    '<span data-bb-breaks="written"></span><div class="quote" data-bb-tag="quote">раз</div><div class="quote" data-bb-tag="quote">два</div>',
  ],
  [
    "[ul]раз[li]два[/li]три[/ul]",
    '<span data-bb-breaks="written"></span><div data-bb-tag="ul">раз<div data-bb-tag="li">два</div>три</div>',
  ],
  [
    "[ul]\n[li]раз[/li]\n[/ul]",
    '<span data-bb-breaks="written"></span><div data-bb-tag="ul"><br /><div data-bb-tag="li">раз</div><br /></div>',
  ],
  [
    "[ol]раз[/ol]",
    '<span data-bb-breaks="written"></span><div data-bb-tag="ol">раз</div>',
  ],
  [
    "[li]раз[li]два[/li]три[/li]",
    '<span data-bb-breaks="written"></span><div data-bb-tag="li">раз<div data-bb-tag="li">два</div>три</div>',
  ],
  [
    '[spoiler text=""]текст[/spoiler]',
    '<span data-bb-breaks="written"></span><div class="spoiler" data-bb-tag="spoiler" data-bb-text="">текст</div>',
  ],
  [
    "раз\nдва\nтри",
    '<span data-bb-breaks="written"></span>раз<br />два<br />три',
  ],
  // The line break typed after a block is not drawn on the page, the block
  // ending its line, and the editor still gets it back.
  [
    'перед\n[private="Вася"]секрет[/private]\nпосле\n[quote]цитата[/quote]\nконец',
    '<span data-bb-breaks="written"></span>перед<br /><span class="private-message" data-bb-tag="private" data-bb-addressees="Вася">секрет</span><br />после<br /><div class="quote" data-bb-tag="quote">цитата</div><br />конец',
  ],
  // A broken tag reaches the editor as the text it is, and so does a DM2 tag
  // written with a value it does not take.
  ["[b=1]текст[/b]", '<span data-bb-breaks="written"></span>[b=1]текст[/b]'],
  [
    '[quote="Имя"]текст[/quote]',
    '<span data-bb-breaks="written"></span>[quote="Имя"]текст[/quote]',
  ],
];

describe("the server's author-edit markup", () => {
  it.each(SERVER_AUTHOR_EDIT)("comes back as %s", (bbcode, html) => {
    expect(htmlToBbcode(html)).toBe(bbcode);
  });
});
