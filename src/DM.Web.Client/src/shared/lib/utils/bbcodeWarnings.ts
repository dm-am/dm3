// The markup check of the BBCode mode: what the server will not read as meant,
// with its line, before the author sends.

import {
  ALL_TAGS,
  CONTEXT_TAGS,
  MAX_PIXELS,
  type BBCodeContext,
} from "./bbcodeTagSet";
import { asTheServerReads, type BBCodeBrokenReason } from "./bbcodeSpelling";
import { readText } from "./bbcodeReader";

/** The findings, worded as BBCODE_RENDERING.md, "Предупреждения редактора". */
export type BBCodeIssueReason =
  | BBCodeBrokenReason
  | "bad-address"
  | "private-nested"
  | "unclosed"
  | "stray-closing"
  | "too-deep"
  | "private-outside-game";

/** One finding of the markup check. */
export interface BBCodeIssue {
  reason: BBCodeIssueReason;
  /** The line of the text it is on, from 1. */
  line: number;
  /** What is wrong and how to fix it, one sentence without the line. */
  text: string;
  /** What the author reads: the line, then the text. */
  message: string;
}

/**
 * Which of the texts of one reason a finding gets, when the reason has more
 * than one.
 */
type IssueCase =
  /** private-spelling: a closing tag that carries a value. */
  | "closing-value"
  /** private-spelling: a block its closing tag never ended. */
  | "unclosed"
  /** private-spelling: a token after a [noparse] left open. */
  | "after-left-open"
  /** bad-address: an address on neither http nor https. */
  | "scheme"
  /** bad-content: a [tip] inside another. */
  | "nested"
  /** stray-closing: the open tag of that name is outside a hidden block. */
  | "sealed";

/** A finding as the reader records it, before it is worded. */
export interface RawIssue {
  reason: BBCodeIssueReason;
  at: number;
  tag?: string;
  /** The name of a parameter, as typed. */
  param?: string;
  /** A tag as typed. */
  typed?: string;
  case?: IssueCase;
}

/**
 * How a tag that takes a value is written, for the finding of an "=" after its
 * name. A tag missing here takes no value at all.
 */
const WRITTEN_WITHOUT_EQUALS: Record<string, string> = {
  img: '[img width="300"]адрес[/img]',
  link: '[link text="Текст"]адрес[/link]',
  spoiler: '[spoiler text="Подпись"]',
  mention: "[mention]Логин[/mention]",
  tip: '[tip text="Текст"]слово[/tip]',
};

/** The parameters of a tag, as a sentence names them. */
const PARAMS_IN_WORDS: Record<string, string> = {
  img: "width и height",
  link: "text",
  spoiler: "text",
  tip: "text и img",
};

/** One parameter of a tag, written right. */
const PARAM_EXAMPLE: Record<string, string> = {
  img: 'width="300"',
  link: 'text="Текст"',
  spoiler: 'text="Подпись"',
  tip: 'text="Текст"',
};

/** A tag as typed, at most 40 characters of it, and three dots when cut. */
function shown(typed: string | undefined): string {
  const text = (typed ?? "").replace(/\s+/g, " ");
  return text.length > 40 ? `${text.slice(0, 40)}...` : text;
}

/** One sentence without a period, word for word from BBCODE_RENDERING.md. */
function wordIssue(issue: RawIssue): string {
  const tag = issue.tag ?? "";
  const param = issue.param ?? "";
  const takesParams = tag in PARAMS_IN_WORDS;
  switch (issue.reason) {
    case "value-after-name":
      return tag in WRITTEN_WITHOUT_EQUALS
        ? `[${tag}] пишется без = после имени: ${WRITTEN_WITHOUT_EQUALS[tag]}`
        : `у [${tag}] нет значения, пишется просто [${tag}]`;
    case "unknown-param":
      if (!takesParams)
        return `у [${tag}] нет параметров, пишется просто [${tag}]`;
      return param
        ? `у [${tag}] нет параметра ${param}, есть только ${PARAMS_IN_WORDS[tag]}`
        : `в [${tag}] параметр пишется как имя="значение", например ${PARAM_EXAMPLE[tag]}`;
    case "unquoted-value":
      return `значение ${param} пишется в прямых кавычках: ${param}="..."`;
    case "repeated-param":
      return `параметр ${param} указан дважды, оставьте один`;
    case "bracket-in-value":
      return `символ [ в значении ${param} не допускается, уберите его`;
    case "newline-in-value":
      return `значение ${param} пишется в одну строку`;
    case "unterminated-value":
      return `у значения ${param} нет закрывающей кавычки перед ]`;
    case "stray-space":
      return takesParams
        ? `лишний пробел в [${tag}]: пробел ставится только перед именем параметра`
        : `лишний пробел в [${tag}], пишется просто [${tag}]`;
    case "bad-number":
      return `значение ${param} должно быть целым числом от 1 до ${MAX_PIXELS}`;
    case "bad-content":
      if (tag === "tip") {
        return issue.case === "nested"
          ? "[tip] внутри [tip] не работает"
          : "между [tip] и [/tip] пишутся слова в одну строку, без тегов";
      }
      return tag === "mention"
        ? "между [mention] и [/mention] пишется только логин"
        : `между [${tag}] и [/${tag}] пишется только адрес`;
    case "missing-param":
      return 'у [tip] нужен text или img: [tip text="Текст"]слово[/tip]';
    case "bad-address":
      if (issue.case === "scheme") {
        return tag === "tip"
          ? "адрес в [tip] должен начинаться с http://, https:// или /"
          : `адрес в [${tag}] должен начинаться с http:// или https://`;
      }
      return `адрес в [${tag}] указывает на локальную сеть или записан с ошибкой`;
    case "value-on-closing-tag":
      return `закрывающий тег пишется без значения: [/${tag}]`;
    case "private-spelling":
      switch (issue.case) {
        case "unclosed":
          return "скрытый блок не закрыт, добавьте [/private]";
        case "after-left-open":
          return `${shown(issue.typed)} не станет примером: [noparse] выше не закрыт тегом [/noparse]`;
        case "closing-value":
          return `${shown(issue.typed)} не закрывает скрытый блок, закрывает только [/private]`;
        default:
          return `${shown(issue.typed)} записан неверно, скрытый блок пишется [private="Имя"] или [private]`;
      }
    case "private-nested":
      return "скрытый блок внутри скрытого не работает, закройте первый тегом [/private]";
    case "private-outside-game":
      return "скрытый блок работает только в игровых постах, для примера оберните его в [noparse]";
    case "unclosed":
      return `[${tag}] не закрыт, добавьте [/${tag}]`;
    case "stray-closing":
      return issue.case === "sealed"
        ? `[/${tag}] не закрывает [${tag}] снаружи скрытого блока, перенесите его за [/private]`
        : `[/${tag}] ничего не закрывает, уберите его или откройте [${tag}] выше`;
    case "too-deep":
      return "теги вложены глубже 512 уровней, такой текст покажется без оформления";
  }
}

/**
 * Checks BBCode as the server reads it: silent on correct spellings, on what
 * the editor writes and inside a closed [noparse]. Given a context it reads the
 * tags of that surface; a hidden block is read everywhere, as the save path
 * reads it.
 *
 * @example
 * validateBBCode("[b]текст[/b]")           // => []
 * validateBBCode('[img width=300]...[/img]') // => [{ reason: "unquoted-value", ... }]
 * validateBBCode("[private]x[/private]", "common") // => [{ reason: "private-outside-game", ... }]
 */
export function validateBBCode(
  bbcode: string,
  context?: BBCodeContext,
): BBCodeIssue[] {
  const text = asTheServerReads(bbcode ?? "");
  const raw: RawIssue[] = [];
  const privateOutside =
    context !== undefined && !CONTEXT_TAGS[context].includes("private");
  const tags =
    context === undefined
      ? ALL_TAGS
      : new Set([...CONTEXT_TAGS[context], "private"]);
  readText(text, tags, raw, privateOutside);
  const lineStarts = [0];
  for (let i = text.indexOf("\n"); i >= 0; i = text.indexOf("\n", i + 1)) {
    lineStarts.push(i + 1);
  }
  const lineOf = (at: number): number => {
    let low = 0;
    let high = lineStarts.length - 1;
    while (low < high) {
      const mid = (low + high + 1) >> 1;
      if (lineStarts[mid] <= at) low = mid;
      else high = mid - 1;
    }
    return low + 1;
  };
  return raw
    .sort((a, b) => a.at - b.at)
    .map((issue) => {
      const line = lineOf(issue.at);
      const words = wordIssue(issue);
      return {
        reason: issue.reason,
        line,
        text: words,
        message: `Строка ${line}: ${words}`,
      };
    });
}
