/**
 * @vitest-environment node
 */

/**
 * The decisions under a moderation record stand as the reviews under a post.
 *
 * The owner (28.09): "предупреждения, как и изменения баллов, должны быть
 * оформлены, как оценки постов". The list and its item cannot borrow the
 * review's component, which belongs to the game and carries its edit form, so
 * the record keeps the same numbers in its own styles, and this holds the two
 * equal. A copy with only a comment pointing at the original drifts the first
 * time the reviews move, and nothing on the moderation screens would say so.
 *
 * Read from the sources rather than from a mounted screen: scoped SASS is not
 * computed in a test environment, and what drifts is a number someone edits.
 */
import { describe, it, expect } from "vitest";
import { readFileSync } from "fs";
import { dirname, join } from "path";
import { fileURLToPath } from "url";

const HERE = dirname(fileURLToPath(import.meta.url));
const SRC = join(HERE, "..", "..", "..");

const read = (path: string) => readFileSync(join(SRC, path), "utf8");

/** The style block of a single-file component. */
function styleOf(source: string): string {
  const start = source.indexOf("<style");
  const end = source.indexOf("</style>", start);
  if (start < 0 || end < 0) throw new Error("no style block");
  return source.slice(source.indexOf(">", start) + 1, end);
}

/**
 * The declarations of one rule of an indented SASS block: the rule at the top
 * level, or a nested one under it (`& + &`).
 */
function declarations(
  style: string,
  selector: string,
  nested?: string,
): Record<string, string> {
  const lines = style.split(/\r?\n/);
  const top = lines.findIndex((l) => l === selector);
  if (top < 0) throw new Error(`no rule ${selector}`);

  const body: string[] = [];
  for (const l of lines.slice(top + 1)) {
    if (l.trim() && !/^\s/.test(l)) break;
    body.push(l);
  }

  let scope = body.filter((l) => /^ {2}\S/.test(l));
  if (nested) {
    const at = body.findIndex((l) => l === `  ${nested}`);
    if (at < 0) throw new Error(`no nested rule ${nested} in ${selector}`);
    scope = [];
    for (const l of body.slice(at + 1)) {
      if (!/^ {4}\S/.test(l)) {
        if (l.trim()) break;
        continue;
      }
      scope.push(l);
    }
  }

  const found: Record<string, string> = {};
  for (const l of scope) {
    const match = /^\s+([a-z-]+):\s*(.+)$/.exec(l);
    if (match) found[match[1]] = match[2].trim();
  }
  return found;
}

const pick = (all: Record<string, string>, keys: string[]) =>
  Object.fromEntries(keys.map((key) => [key, all[key]]));

const gamePost = styleOf(read("widgets/game-post/GamePost.vue"));
const reviewItem = styleOf(read("entities/game/ui/PostReviewItem.vue"));
const verdictList = styleOf(
  read("entities/moderation/ui/RecordVerdictList.vue"),
);
const verdict = styleOf(read("entities/moderation/ui/RecordVerdict.vue"));

describe("the decisions of a moderation record", () => {
  it("stand in the list the reviews of a post stand in", () => {
    const keys = ["list-style", "margin", "padding-top", "padding-left"];

    expect(
      pick(declarations(verdictList, ".record-verdict-list"), keys),
    ).toEqual(pick(declarations(gamePost, ".reviews-section"), keys));
  });

  it("are items of the review's size and indent", () => {
    const keys = ["margin", "padding-left", "line-height"];

    expect(pick(declarations(verdict, ".record-verdict"), keys)).toEqual(
      pick(declarations(reviewItem, ".review-item"), keys),
    );
  });

  it("keep the review's step between two of them", () => {
    expect(declarations(verdict, ".record-verdict", "& + &")).toEqual(
      declarations(reviewItem, ".review-item", "& + &"),
    );
  });

  it("set their words as the review sets its text", () => {
    const keys = ["color", "font-weight", "margin-bottom"];

    expect(pick(declarations(verdict, ".record-verdict__text"), keys)).toEqual(
      pick(declarations(reviewItem, ".review-text"), keys),
    );
  });
});
