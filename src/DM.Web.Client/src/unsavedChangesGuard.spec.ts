/**
 * @vitest-environment node
 */

/**
 * Half an hour of a character sheet, a link in the sidebar that is on every
 * page of a game, and a navigation that was instant and silent. The guard that
 * fixed it exists and is tested — in isolation, with `dirty` handed to it by
 * the test. Whether any form actually mounts it was checked by nothing: three
 * specs were named as the gate for that and none of them held it. One mocks
 * `onBeforeRouteLeave` away, one never mentions the guard, and deleting the tag
 * from CharacterForm left all nineteen of their tests green. The only thing
 * standing between the four forms and the original defect was a lint warning
 * about an unused import, which disappears with the import.
 *
 * So the mounting is asserted here, over the source, because that is where the
 * pairing lives. The list is the decision "these are the forms whose loss
 * costs the writer real work" — adding a long form to the site means adding a
 * line here, and that is the point of writing it down rather than deriving it.
 */
import { describe, it, expect } from "vitest";
import { readFileSync } from "fs";
import { dirname, join, resolve } from "path";
import { fileURLToPath } from "url";

const SRC = dirname(fileURLToPath(import.meta.url));
const CLIENT_ROOT = resolve(SRC, "..");

/** Forms a reader can lose more than a moment of work in. */
const GUARDED = [
  "src/features/edit-character/ui/CharacterForm.vue",
  "src/features/create-game/ui/CreateGameForm.vue",
  "src/features/create-blog/ui/CreateBlogForm.vue",
  "src/pages/blog/PublicationEdit.vue",
];

const read = (relative: string): string =>
  readFileSync(join(CLIENT_ROOT, relative), "utf8");

describe("the unsaved-work guard", () => {
  it.each(GUARDED)("is mounted by %s", (relative) => {
    const source = read(relative);

    expect(source).toContain("<UnsavedChangesGuard");
    // Bound, not literal: `:dirty="false"` would mount a guard that never asks.
    expect(source).toMatch(/<UnsavedChangesGuard\s+:dirty="(?!false)[\w.?]+"/);
  });

  /**
   * The prop has to be computed from the form's own state. A `dirty` that is
   * only ever set to true on submit, or never set at all, mounts a guard that
   * is silent for the same reason a missing one is.
   */
  it.each(GUARDED)("hands %s a dirty it derives from the form", (relative) => {
    const source = read(relative);
    const binding = /<UnsavedChangesGuard\s+:dirty="([\w.?]+)"/.exec(source);

    expect(binding).not.toBeNull();
    const name = binding![1].split(/[.?]/)[0];

    expect(source).toMatch(
      new RegExp(`(const|let)\\s+${name}\\s*=\\s*(computed|ref)\\(`),
    );
  });

  /**
   * A guard nobody can reach from the template is an import and nothing else,
   * which is the state this rule was written to make impossible to reach by
   * halves.
   */
  it("is imported by exactly the files that mount it", () => {
    const importers = GUARDED.filter((relative) =>
      read(relative).includes('from "@/shared/ui/UnsavedChangesGuard"'),
    );

    expect(importers).toEqual(GUARDED);
  });
});
