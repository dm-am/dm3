/**
 * @vitest-environment node
 */

/**
 * "Одно слово на одно значение" (docs/conventions/CODE_STYLE.md), applied to
 * lifecycle captions.
 *
 * Blog status had four spellings on the client and two of them met on one
 * screen: the badge in the blog header said "Оформляется"/"Открыт" while the
 * info table two lines below said "Черновик"/"Активен". A character out of the
 * game had three sets of words — the card badge, the game roster and the
 * profile table each invented their own.
 *
 * Wording is not held by a type, so it is held here: the captions that lost
 * must not come back. The scan strips comments first, so a banned phrase quoted
 * in a comment (a transition map, a note about what changed) is not a failure —
 * only text that can reach a screen is.
 */
import { describe, it, expect } from "vitest";
import { readdirSync, readFileSync, statSync } from "fs";
import { dirname, join, relative, resolve } from "path";
import { fileURLToPath } from "url";
import { buildStatusLines } from "./tooltipBuilders";

const HERE = dirname(fileURLToPath(import.meta.url));
// utils -> lib -> shared -> src
const SRC_ROOT = resolve(HERE, "..", "..", "..");
const SKIP_DIRS = new Set(["node_modules", "dist"]);

/** Every source file that can render text. Tests are not text on a screen. */
function sourceFiles(dir: string): string[] {
  const out: string[] = [];
  for (const name of readdirSync(dir)) {
    if (SKIP_DIRS.has(name)) continue;
    const full = join(dir, name);
    if (statSync(full).isDirectory()) {
      out.push(...sourceFiles(full));
      continue;
    }
    if (name.endsWith(".spec.ts")) continue;
    if (name.endsWith(".ts") || name.endsWith(".vue")) out.push(full);
  }
  return out;
}

/**
 * Drop line, block and HTML comments; keep string literals whole and keep the
 * newlines, so the offset of a match still maps to a line of the file.
 *
 * Only `"` and a backtick open a literal. The codebase writes strings in double
 * quotes, and treating an apostrophe as a delimiter would swallow Russian text
 * after the first one.
 */
function stripComments(text: string): string {
  let out = "";
  let state: "code" | "line" | "block" | "html" | '"' | "`" = "code";
  let i = 0;
  while (i < text.length) {
    const c = text[i];
    if (state === "code") {
      if (c === "/" && text[i + 1] === "/") {
        state = "line";
        i += 2;
      } else if (c === "/" && text[i + 1] === "*") {
        state = "block";
        i += 2;
      } else if (text.startsWith("<!--", i)) {
        state = "html";
        i += 4;
      } else {
        if (c === '"' || c === "`") state = c;
        out += c;
        i += 1;
      }
      continue;
    }
    if (state === "line" || state === "block" || state === "html") {
      const ends =
        state === "line"
          ? c === "\n"
          : state === "block"
            ? c === "*" && text[i + 1] === "/"
            : text.startsWith("-->", i);
      if (ends) {
        if (state === "line") out += c;
        i += state === "line" ? 1 : state === "block" ? 2 : 3;
        state = "code";
        continue;
      }
      if (c === "\n") out += c;
      i += 1;
      continue;
    }
    if (c === "\\") {
      out += text.slice(i, i + 2);
      i += 2;
      continue;
    }
    if (c === state) state = "code";
    out += c;
    i += 1;
  }
  return out;
}

/** A Cyrillic letter. */
const CYR = "[А-Яа-я]";

/** The banned caption as a whole word, so a plural of it does not match. */
const wholeWord = (caption: string): RegExp =>
  new RegExp(`(?<!${CYR})${caption}(?!${CYR})`);

/** A caption that lost, and the one that replaced it. */
type LostCaption = [banned: RegExp, winner: string];

/**
 * A character out of the game: CharacterStatus.Retired refined by the isDead /
 * isPlayerLeft / isPlayerExiled flags. These phrases name nothing else in the
 * product, so they are banned everywhere. "Погиб" and "Выбыл" are whole words:
 * the verb "отметить погибшим" on the transition button is a different job.
 */
const RETIRED_CAPTIONS: LostCaption[] = [
  [wholeWord("Погиб"), "Персонаж мертв"],
  [wholeWord("Выбыл"), "Вне игры"],
  [/Игрок изгнан/, "Выведен из игры"],
  [/Игрок выведен из игры/, "Выведен из игры"],
  [/Игрок покинул/, "Покинул игру"],
];

/**
 * A file that spells the ModuleStatus keys is a file that captions them. The
 * scope matters: a ban is active for a poll and a moderator is "Активен" too,
 * and those are their own words for their own values.
 */
const MODULE_STATUS_FILE = /["']Draft["']|Draft\s*:/;

/**
 * Game and blog lifecycle. Whole words again: the count summary in a tooltip
 * groups games and blogs in the plural ("Черновики: 3 игры"), which is a
 * different job from captioning one module.
 */
const MODULE_CAPTIONS: LostCaption[] = [
  [wholeWord("Черновик"), "Оформляется"],
  [wholeWord("Активен"), "Открыт"],
];

function offenders(
  files: string[],
  captions: LostCaption[],
  scope?: RegExp,
): string[] {
  const found: string[] = [];
  for (const file of files) {
    const text = stripComments(readFileSync(file, "utf8"));
    if (scope && !scope.test(text)) continue;
    for (const [banned, winner] of captions) {
      const hit = banned.exec(text);
      if (!hit) continue;
      const line = text.slice(0, hit.index).split("\n").length;
      found.push(
        `${relative(SRC_ROOT, file)}:${line} says "${hit[0]}", the caption is "${winner}"`,
      );
    }
  }
  return found;
}

describe("status captions", () => {
  const files = sourceFiles(SRC_ROOT);

  it("reads the client sources", () => {
    // Guards the walk itself: a wrong root would pass every check below.
    expect(files.length).toBeGreaterThan(100);
  });

  it("names a character out of the game one way", () => {
    expect(offenders(files, RETIRED_CAPTIONS)).toEqual([]);
  });

  it("names a game or blog status one way", () => {
    expect(offenders(files, MODULE_CAPTIONS, MODULE_STATUS_FILE)).toEqual([]);
  });
});

describe("buildStatusLines", () => {
  it("calls the Closed group closed, not finished", () => {
    // The server counts every ModuleStatus.Closed into one number without
    // looking at ClosedReason, so a frozen game lands in this group as well:
    // "Завершенные" would state something the number does not say.
    const lines = buildStatusLines({ draft: 1, active: 2, closed: 3 }, [
      "игра",
      "игры",
      "игр",
    ]);

    expect(lines).toHaveLength(3);
    expect(lines[2]).toContain("Закрытые: 3 игры");
    expect(lines.join("\n")).not.toMatch(/Завершенн/);
  });
});
