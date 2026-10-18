/**
 * @vitest-environment node
 */

/**
 * The comment of a dice roll stands where the old site puts it: after the
 * dice, in the same line, its caption "Комментарий" and a field over the rest
 * of the width. Where the line has no room left for it (the old site keeps 350
 * for the pair), the pair goes under the dice whole, and the field is never
 * squeezed below what a word typed in it needs.
 *
 * The field used to share a line with the numbers of the roll and take what
 * they left, and the reader saw the first letters of what he had typed. These
 * hold the parts that keep that from coming back: the pair grows from 350 and
 * wraps as one, the caption keeps its width, and the field keeps 100.
 * Read off the stylesheet, which a document that lays nothing out cannot show.
 */
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { describe, expect, it } from "vitest";
import { componentCss, ruleMap } from "@/shared/lib/testing/styles";

const HERE = dirname(fileURLToPath(import.meta.url));
const COMPOSER = resolve(HERE, "../../widgets/game-post/DiceComposer.vue");

const rules = ruleMap(componentCss(COMPOSER));

describe("the comment field of a dice roll", () => {
  it("goes with its caption after the dice, under them where the line has no room", () => {
    expect(rules.get(".dice-line")).toMatchObject({
      display: "flex",
      "flex-wrap": "wrap",
    });
    expect(rules.get(".dice-comment-line")).toMatchObject({
      display: "flex",
      flex: "1 1 350px",
    });
  });

  it("keeps its caption whole and a field a word can be typed in", () => {
    expect(rules.get(".dice-comment-caption")).toMatchObject({ flex: "none" });
    expect(rules.get(".dice-comment")).toMatchObject({
      flex: "1",
      "min-width": "100px",
    });
  });
});
