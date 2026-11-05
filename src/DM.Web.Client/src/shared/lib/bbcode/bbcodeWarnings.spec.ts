/**
 * @vitest-environment jsdom
 */

/**
 * The words of the markup check are the table of BBCODE_RENDERING.md,
 * "Предупреждения редактора", both ways: every text said is one of its code's,
 * and every text of the table is said for some input.
 */

import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { describe, expect, it } from "vitest";
import {
  validateBBCode,
  type BBCodeIssue,
  type BBCodeIssueReason,
} from "./bbcodeWarnings";
import { type BBCodeContext } from "./bbcodeTagSet";
import {
  BBCODE_GRAMMAR_CASES,
  type GrammarSurface,
} from "./bbcodeGrammar.cases";

const HERE = dirname(fileURLToPath(import.meta.url));
const DOCUMENT = resolve(
  HERE,
  "../../../../../../docs/architecture/BBCODE_RENDERING/grammar.md",
);

/** What stands in place of each placeholder of the table. */
const PLACEHOLDERS: Record<string, string> = {
  "{тег}": "[a-z]+",
  "{параметр}": "[A-Za-z]+",
  "{запись}": "\\[[^\\n]{0,40}(?:\\.\\.\\.)?",
  "{параметры}": "(?:width и height|text и img|text)",
  "{пример}": '(?:width="300"|text="Текст"|text="Подпись")',
};

interface Template {
  reason: BBCodeIssueReason;
  text: string;
  pattern: RegExp;
}

/** The rows of the table of the section, the code carried down its rows. */
function templates(): Template[] {
  const document = readFileSync(DOCUMENT, "utf8").replace(/\r\n/g, "\n");
  const start = document.indexOf("## Предупреждения редактора");
  expect(start, "the section of the document").toBeGreaterThanOrEqual(0);
  const section = document.slice(start, document.indexOf("\n## ", start + 1));
  const found: Template[] = [];
  let reason: BBCodeIssueReason | null = null;
  for (const line of section.split("\n")) {
    if (!line.startsWith("| ") || line.startsWith("| Код")) continue;
    const cells = line.split("|").map((cell) => cell.trim());
    const code = /^`([a-z-]+)`$/.exec(cells[1]);
    if (code) reason = code[1] as BBCodeIssueReason;
    const text = /^`([^`]+)`/.exec(cells[3]);
    if (!reason || !text) continue;
    let pattern = text[1].replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
    for (const [name, value] of Object.entries(PLACEHOLDERS)) {
      const escaped = name.replace(/[{}]/g, "\\$&");
      pattern = pattern.split(escaped).join(value);
    }
    found.push({ reason, text: text[1], pattern: new RegExp(`^${pattern}$`) });
  }
  return found;
}

const TEMPLATES = templates();

const CONTEXT_OF: Record<Exclude<GrammarSurface, "any">, BBCodeContext> = {
  GamePost: "post",
  Comment: "common",
  GlobalChatMessage: "message",
  DirectMessage: "direct",
  Profile: "info",
  Hint: "hint",
};

/** The five surfaces of content "any" stands for: the hint is named by its rows. */
const CONTENT_CONTEXTS: BBCodeContext[] = [
  "post",
  "common",
  "message",
  "direct",
  "info",
];

/**
 * Inputs for the cases no row of the grammar table has: the table holds a
 * spelling to one code, and a code can have several texts.
 */
const MORE: Array<[string, BBCodeContext]> = [
  ['[img ="300"]https://example.com/a.png[/img]', "common"],
  ['[link ="Главная"]https://example.com/[/link]', "common"],
  ['[b x="1"]текст[/b]', "common"],
  ["[b ]текст[/b]", "common"],
  ['[img width="300"  ]https://example.com/a.png[/img]', "common"],
  ['[link text="Главная"]два слова[/link]', "common"],
  ['[private="Вася"]раз [private]два[/private][/private]', "post"],
  ["[b]раз [private]два[/b] три[/private][/b]", "post"],
  [`[private=${"а".repeat(60)}]текст[/private]`, "post"],
  ["[b]".repeat(600) + "текст", "common"],
];

/** Every finding the check makes over the table and the inputs above. */
function findings(): Array<{ input: string; issue: BBCodeIssue }> {
  const all: Array<{ input: string; issue: BBCodeIssue }> = [];
  const check = (input: string, context: BBCodeContext) => {
    for (const issue of validateBBCode(input, context)) {
      all.push({ input, issue });
    }
  };
  for (const row of BBCODE_GRAMMAR_CASES) {
    const contexts =
      row.surface === "any" ? CONTENT_CONTEXTS : [CONTEXT_OF[row.surface]];
    for (const context of contexts) check(row.input, context);
  }
  for (const [input, context] of MORE) check(input, context);
  return all;
}

const FINDINGS = findings();

describe("validateBBCode", () => {
  it("returns empty array for valid BBCode", () => {
    expect(validateBBCode("[b]text[/b]")).toEqual([]);
  });

  it("returns empty array for nested valid BBCode", () => {
    expect(validateBBCode("[b][i]text[/i][/b]")).toEqual([]);
  });

  it("returns empty array for standalone tags", () => {
    expect(validateBBCode("[tab]")).toEqual([]);
  });

  it("detects unclosed tags", () => {
    expect(validateBBCode("[b]text")).toEqual([
      {
        reason: "unclosed",
        line: 1,
        text: "[b] не закрыт, добавьте [/b]",
        message: "Строка 1: [b] не закрыт, добавьте [/b]",
      },
    ]);
  });

  // A closing tag closes the nearest open tag of its name, and what was
  // opened inside it stays unclosed; the closing tag after it closes nothing.
  it("detects crossed tags", () => {
    expect(
      validateBBCode("[b][i]text[/b][/i]").map((issue) => issue.reason),
    ).toEqual(["unclosed", "stray-closing"]);
  });

  it("detects unexpected closing tags", () => {
    expect(validateBBCode("text[/b]")).toEqual([
      {
        reason: "stray-closing",
        line: 1,
        text: "[/b] ничего не закрывает, уберите его или откройте [b] выше",
        message:
          "Строка 1: [/b] ничего не закрывает, уберите его или откройте [b] выше",
      },
    ]);
  });

  it("names the line a finding is on", () => {
    expect(
      validateBBCode(
        "раз\nдва\n[img width=300]https://example.com/a.png[/img]",
      ),
    ).toEqual([
      {
        reason: "unquoted-value",
        line: 3,
        text: 'значение width пишется в прямых кавычках: width="..."',
        message:
          'Строка 3: значение width пишется в прямых кавычках: width="..."',
      },
    ]);
  });

  it("names a parameter as it was typed", () => {
    expect(
      validateBBCode('[img WIDTH="0"]https://example.com/a.png[/img]')[0].text,
    ).toBe("значение WIDTH должно быть целым числом от 1 до 9999");
  });

  it("is silent on what the editor's windows write", () => {
    for (const written of [
      '[link text="Главная"]https://example.com/[/link]',
      '[img width="300" height="200"]https://example.com/a.png[/img]',
      '[img height="200"]https://example.com/a.png[/img]',
      '[spoiler text="Карта"]текст[/spoiler]',
      '[private="Вася, Петя"]текст[/private]',
      "[private]текст[/private]",
      "[ul]\n[li]пункт[/li]\n[/ul]",
      "[code]текст[/code]",
      "[noparse][b]текст[/b][/noparse]",
    ]) {
      expect(validateBBCode(written, "post"), written).toEqual([]);
    }
  });

  // An image has no alt: a reader never sees it, and text nobody sees is
  // where an insult goes unnoticed. The page prints such a tag as typed.
  it("names an alt on an image, an empty one too", () => {
    for (const written of [
      '[img alt="кот"]https://example.com/a.png[/img]',
      '[img alt=""]https://example.com/a.png[/img]',
      '[img width="300" alt="кот"]https://example.com/a.png[/img]',
    ]) {
      expect(validateBBCode(written, "common"), written).toEqual([
        {
          reason: "unknown-param",
          line: 1,
          text: "у [img] нет параметра alt, есть только width и height",
          message:
            "Строка 1: у [img] нет параметра alt, есть только width и height",
        },
      ]);
    }
  });

  it("speaks Russian and names the reason", () => {
    const [issue] = validateBBCode('[code="x"]пример[/code]');
    expect(issue.reason).toBe("value-after-name");
    expect(issue.message).toBe(
      "Строка 1: у [code] нет значения, пишется просто [code]",
    );
  });

  it("handles complex valid nested structures", () => {
    expect(validateBBCode("[quote][b][i]text[/i][/b][/quote]")).toEqual([]);
  });

  it("has nothing to say about a name in capitals", () => {
    expect(
      validateBBCode("[B]текст[/b] [IMG]https://example.com/a.png[/IMG]"),
    ).toEqual([]);
  });

  // A tag the surface does not have is text there, as on the page, and the
  // check cannot know whether the author meant markup.
  it("has nothing to say about a tag the surface does not have", () => {
    expect(validateBBCode("[mod]текст", "post")).toEqual([]);
    expect(validateBBCode("[mod=1]текст[/mod]", "direct")).toEqual([]);
    expect(
      validateBBCode("[mod]текст", "common").map((issue) => issue.reason),
    ).toEqual(["unclosed"]);
  });

  // [code] only changes the font: left open, it is a tag left open, and the
  // hidden block after it is a hidden block the save takes.
  it("names a [code] left open and nothing after it", () => {
    expect(
      validateBBCode(
        '[code][b]x[/b]\n[private="Вася"]секрет[/private]',
        "post",
      ),
    ).toEqual([
      {
        reason: "unclosed",
        line: 1,
        text: "[code] не закрыт, добавьте [/code]",
        message: "Строка 1: [code] не закрыт, добавьте [/code]",
      },
    ]);
  });

  // Without its closing tag a [noparse] is no example: the [private] after it
  // is a hidden block like any other, and the save refuses it, since whoever
  // wrote it almost always meant an example.
  it("names a hidden block under a [noparse] left open", () => {
    expect(
      validateBBCode(
        '[noparse][b]x[/b]\n[private="Вася"]секрет[/private]',
        "post",
      ),
    ).toEqual([
      {
        reason: "unclosed",
        line: 1,
        text: "[noparse] не закрыт, добавьте [/noparse]",
        message: "Строка 1: [noparse] не закрыт, добавьте [/noparse]",
      },
      {
        reason: "private-spelling",
        line: 2,
        text: '[private="Вася"] не станет примером: [noparse] выше не закрыт тегом [/noparse]',
        message:
          'Строка 2: [private="Вася"] не станет примером: [noparse] выше не закрыт тегом [/noparse]',
      },
    ]);
  });

  // The save path reads no tag but [private] and [noparse], so it cannot tell
  // where the block around a [noparse] left open ends, and refuses every
  // hidden block after that [noparse]. The check names what the save will
  // refuse.
  it("names a hidden block after the block a [noparse] was left open in", () => {
    expect(
      validateBBCode(
        "[quote][noparse]x[/quote] [private]секрет[/private]",
        "post",
      ).map((issue) => issue.reason),
    ).toEqual(["unclosed", "private-spelling"]);
  });

  // A [/private] written as part of an example would end the block early and
  // show the rest, so the closing tag of a block opened before the [noparse]
  // is named too; a block opened after it is named once, at its opening.
  it("names the closing tag of a block the [noparse] was left open in", () => {
    expect(
      validateBBCode(
        '[private="Вася"]секрет [noparse]x\n[/private] хвост',
        "post",
      ).map((issue) => [issue.line, issue.text]),
    ).toEqual([
      [1, "[noparse] не закрыт, добавьте [/noparse]"],
      [
        2,
        "[/private] не станет примером: [noparse] выше не закрыт тегом [/noparse]",
      ],
    ]);
  });

  it("names a hidden block inside another", () => {
    expect(
      validateBBCode(
        '[private="Вася"]раз\n[private]два[/private][/private]',
        "post",
      ),
    ).toEqual([
      {
        reason: "private-nested",
        line: 2,
        text: "скрытый блок внутри скрытого не работает, закройте первый тегом [/private]",
        message:
          "Строка 2: скрытый блок внутри скрытого не работает, закройте первый тегом [/private]",
      },
    ]);
  });

  it("names a broken spelling of a hidden block as typed", () => {
    expect(
      validateBBCode(
        '[private=Вася]секрет[/private="x"][/private]',
        "post",
      ).map((issue) => issue.text),
    ).toEqual([
      '[private=Вася] записан неверно, скрытый блок пишется [private="Имя"] или [private]',
      '[/private="x"] не закрывает скрытый блок, закрывает только [/private]',
    ]);
    expect(validateBBCode("текст [private]секрет", "post")[0].text).toBe(
      "скрытый блок не закрыт, добавьте [/private]",
    );
  });

  it("cuts a long spelling at 40 characters", () => {
    const typed = `[private=${"а".repeat(50)}]`;
    expect(validateBBCode(`${typed}x[/private]`, "post")[0].text).toBe(
      `${typed.slice(0, 40)}... записан неверно, скрытый блок пишется [private="Имя"] или [private]`,
    );
  });

  // The page prints the tag as typed and the address in its place: the
  // author is told why before the send, not after.
  it("names an address the page refuses", () => {
    expect(
      validateBBCode("[link]mailto:a@example.com[/link]").map(
        (issue) => issue.text,
      ),
    ).toEqual(["адрес в [link] должен начинаться с http:// или https://"]);
    expect(
      validateBBCode("[img]http://192.168.0.1/a.png[/img]").map(
        (issue) => issue.text,
      ),
    ).toEqual([
      "адрес в [img] указывает на локальную сеть или записан с ошибкой",
    ]);
  });

  // Only [/private] ends a hidden block, so a closing tag of an outer tag
  // written inside one closes nothing; the check says where it has to go.
  it("names a closing tag that stands inside a hidden block", () => {
    expect(
      validateBBCode("[b]раз [private]два[/b] три[/private][/b]", "post").map(
        (issue) => issue.text,
      ),
    ).toEqual([
      "[/b] не закрывает [b] снаружи скрытого блока, перенесите его за [/private]",
    ]);
  });

  // The save path of every surface but the game post refuses a text with a
  // hidden block in it (PrivateBlockMarkup.DescribeSurfaceRefusal), and the
  // page erases such a block for every reader. The check used to read every
  // surface as a game post and said nothing about it.
  it("names a hidden block on a surface that has none, once", () => {
    expect(
      validateBBCode(
        'раз\n[private="Вася"]секрет[/private] [private]x',
        "common",
      ).map((issue) => issue.message),
    ).toEqual([
      "Строка 2: скрытый блок работает только в игровых постах, для примера оберните его в [noparse]",
      "Строка 2: скрытый блок не закрыт, добавьте [/private]",
    ]);
  });

  it("names a lone closing tag of a hidden block off its surface too", () => {
    expect(
      validateBBCode("текст [/private]", "direct").map((issue) => issue.reason),
    ).toEqual(["private-outside-game", "stray-closing"]);
  });

  it("leaves a hidden block alone in a game post and in an example", () => {
    expect(validateBBCode('[private="Вася"]секрет[/private]', "post")).toEqual(
      [],
    );
    expect(
      validateBBCode('[noparse][private="Вася"]x[/private][/noparse]', "info"),
    ).toEqual([]);
  });
});

describe("the words of the markup check", () => {
  it("reads the table of the document", () => {
    expect(TEMPLATES.length).toBeGreaterThanOrEqual(30);
    const reasons = new Set(TEMPLATES.map((template) => template.reason));
    for (const reason of [
      "value-after-name",
      "unknown-param",
      "bad-address",
      "private-spelling",
      "private-nested",
      "private-outside-game",
      "unclosed",
      "stray-closing",
      "too-deep",
    ]) {
      expect(reasons, reason).toContain(reason);
    }
  });

  it("says every finding in the words of the document", () => {
    const astray = FINDINGS.filter(
      ({ issue }) =>
        !TEMPLATES.some(
          (template) =>
            template.reason === issue.reason &&
            template.pattern.test(issue.text),
        ),
    ).map(({ input, issue }) => `${issue.reason}: ${issue.text} <- ${input}`);
    expect(astray).toEqual([]);
  });

  it("says every text of the document for some input", () => {
    const unsaid = TEMPLATES.filter(
      (template) =>
        !FINDINGS.some(
          ({ issue }) =>
            issue.reason === template.reason &&
            template.pattern.test(issue.text),
        ),
    ).map((template) => `${template.reason}: ${template.text}`);
    expect(unsaid).toEqual([]);
  });

  it("puts the line before the text, and no period after it", () => {
    for (const { issue } of FINDINGS) {
      expect(issue.message).toBe(`Строка ${issue.line}: ${issue.text}`);
      expect(issue.text.endsWith("."), issue.text).toBe(false);
    }
  });
});
