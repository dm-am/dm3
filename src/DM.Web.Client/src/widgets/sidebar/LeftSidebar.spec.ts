/**
 * @vitest-environment node
 */

/**
 * The rule is written in LeftSidebar itself: "Conditional context panels:
 * lazy-loaded so their entity stores/features stay out of the main bundle
 * (LeftSidebar itself is statically imported by the router)". Three of the four
 * panels followed it and the largest one, GamePanel, did not - 484 lines,
 * shown only on a game route, pulling the game details store and the
 * game-actions feature into the chunk every guest downloads on the home page.
 *
 * Read from the source rather than from a mount: what is asserted is how the
 * component is imported, and a mounted component has already resolved that.
 */
import { describe, it, expect } from "vitest";
import { readFileSync } from "fs";
import { dirname, join } from "path";
import { fileURLToPath } from "url";

const HERE = dirname(fileURLToPath(import.meta.url));
const SOURCE = readFileSync(join(HERE, "LeftSidebar.vue"), "utf8");

/** The panels that mount only on their own zone or for their own role. */
const CONTEXT_PANELS = [
  "GamePanel",
  "BlogPanel",
  "ModerationPanel",
  "MentorPanel",
];

describe("LeftSidebar", () => {
  for (const panel of CONTEXT_PANELS) {
    it(`loads ${panel} lazily`, () => {
      expect(SOURCE).toContain(
        `const ${panel} = defineAsyncComponent(() => import("./${panel}.vue"));`,
      );
      expect(SOURCE).not.toContain(`import ${panel} from "./${panel}.vue";`);
    });
  }

  it("renders every panel it lazy-loads", () => {
    // A panel dropped from the template but left in the imports would keep
    // passing the check above while carrying no weight at all.
    for (const panel of CONTEXT_PANELS) {
      expect(SOURCE).toContain(`<${panel}`);
    }
  });
});
