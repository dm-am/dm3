/**
 * @vitest-environment node
 */

/**
 * The forms whose loss costs the writer real work mount UnsavedChangesGuard
 * with a `dirty` derived from their own state. The guard is tested in
 * isolation; whether a form mounts it is asserted here, over the source, where
 * the pairing lives. The list is the decision which forms those are: a long
 * form added to the site adds a line here.
 */
import { describe, it, expect } from "vitest";
import { readFileSync } from "fs";
import { dirname, join, resolve } from "path";
import { fileURLToPath } from "url";

const SRC = dirname(dirname(fileURLToPath(import.meta.url)));
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
});
