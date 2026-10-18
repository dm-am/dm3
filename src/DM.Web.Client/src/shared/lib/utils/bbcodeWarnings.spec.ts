/**
 * @vitest-environment jsdom
 */

/**
 * The words of the markup check are the words of the document.
 *
 * BBCODE_RENDERING.md, section "Предупреждения редактора", is the list of what
 * the check names and the exact text for each case: one sentence without a
 * period, the tag as typed in place of "{запись}", a name in place of "{тег}"
 * and "{параметр}". This spec reads that table and holds the check to it both
 * ways. Every text the check says, for every row of the grammar table in every
 * context of its row and for the inputs below, is one of the texts of its
 * code; and every text of the table is said for some input, so a case the
 * check lost, or a text it words its own way, fails here and not in front of
 * an author.
 */

import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { describe, expect, it } from "vitest";
import {
  validateBBCode,
  type BBCodeContext,
  type BBCodeIssue,
  type BBCodeIssueReason,
} from "./bbcode";
import {
  BBCODE_GRAMMAR_CASES,
  type GrammarSurface,
} from "./bbcodeGrammar.cases";

const HERE = dirname(fileURLToPath(import.meta.url));
const DOCUMENT = resolve(
  HERE,
  "../../../../../../docs/architecture/BBCODE_RENDERING.md",
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
  const start = document.indexOf("### Предупреждения редактора");
  expect(start, "the section of the document").toBeGreaterThanOrEqual(0);
  const section = document.slice(start, document.indexOf("\n### ", start + 1));
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
