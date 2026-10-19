/**
 * @vitest-environment node
 */

/**
 * One word for one meaning (CODE_STYLE.md) for lifecycle captions: the
 * captions that lost must not come back on a screen; comments are stripped
 * first.
 */
import { describe, it, expect } from "vitest";
import { readFileSync } from "fs";
import { buildStatusLines } from "./tooltipBuilders";
import { stripComments } from "@/shared/lib/testing/comments";
import { CLIENT_SRC, asPath, collect } from "@/shared/lib/testing/sourceTree";

/** Every source file that can render text. Tests are not text on a screen. */
const sourceFiles = (dir: string): string[] =>
  collect(
    dir,
    (name) =>
      !name.endsWith(".spec.ts") &&
      (name.endsWith(".ts") || name.endsWith(".vue")),
  );

/** A Cyrillic letter. */
const CYR = "[А-Яа-я]";

/** The banned caption as a whole word, so a plural of it does not match. */
const wholeWord = (caption: string): RegExp =>
  new RegExp(`(?<!${CYR})${caption}(?!${CYR})`);

/** A caption that lost, and the one that replaced it. */
type LostCaption = [banned: RegExp, winner: string];

/**
 * A character out of the game (Retired with isDead, isPlayerLeft,
 * isPlayerExiled): these phrases name nothing else. "Погиб" and "Выбыл" are
 * whole words, so "отметить погибшим" passes.
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
 * counts games and blogs ("Оформляется: 3 игры"), which is a different job
 * from captioning one module.
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
        `${asPath(file)}:${line} says "${hit[0]}", the caption is "${winner}"`,
      );
    }
  }
  return found;
}

describe("status captions", () => {
  const files = sourceFiles(CLIENT_SRC);

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
