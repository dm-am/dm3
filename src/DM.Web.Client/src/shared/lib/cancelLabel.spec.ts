/**
 * @vitest-environment node
 */

/**
 * A button that only closes a form or a dialog is labelled "Отмена" — the
 * noun. The verb "Отменить" means the button undoes something already done,
 * and then it carries an object: "Отменить голос", "Отменить приглашение".
 * See docs/conventions/UI_STANDARDS.md.
 *
 * The two spellings drifted apart on their own: fourteen dialogs said
 * "Отмена" while the ban and the warning dialogs said "Отменить" next to
 * "Оформить бан", where a moderator can read the second button as undoing the
 * ban rather than closing the form. Nothing but reading caught that, so this
 * test reads for it.
 *
 * Scope is deliberately narrow — only a label that is the bare verb and
 * nothing else:
 *   - a quoted literal equal to the verb (prop value, ternary, constant);
 *   - a template text node whose whole content is the verb.
 * Comments are stripped first, so prose that quotes a label is not a
 * violation, and any verb with an object passes untouched.
 */
import { describe, it, expect } from "vitest";
import { readdirSync, readFileSync, statSync } from "fs";
import { dirname, join, relative, resolve } from "path";
import { fileURLToPath } from "url";

const HERE = dirname(fileURLToPath(import.meta.url));
// lib -> shared -> src
const SRC_ROOT = resolve(HERE, "..", "..");

/**
 * Bare "Отменить" that is not a violation, and why.
 *
 * Every entry is checked to still match something: an exception for code that
 * has since changed is an exception nobody will notice has stopped applying.
 */
const ALLOWED: Record<string, string> = {
  // Row action in the list of sent invitations: it revokes an invitation that
  // exists, and the row itself names the object.
  "pages/blog/settings/InvitationsSection.vue": "revokes a sent invitation",
  "pages/game/settings/InvitationsSection.vue": "revokes a sent invitation",
};

const SKIP_DIRS = new Set(["node_modules", "dist", "coverage"]);

function sourceFiles(dir: string, out: string[] = []): string[] {
  for (const name of readdirSync(dir)) {
    const full = join(dir, name);
    if (statSync(full).isDirectory()) {
      if (!SKIP_DIRS.has(name)) sourceFiles(full, out);
    } else if (
      (name.endsWith(".vue") || name.endsWith(".ts")) &&
      !name.endsWith(".spec.ts")
    ) {
      out.push(full);
    }
  }
  return out;
}

/**
 * HTML, block and line comments. The line-comment pattern wants whitespace or
 * a line start before the slashes, so a URL inside a string survives.
 */
const withoutComments = (text: string): string =>
  text
    .replace(/<!--[\s\S]*?-->/g, "")
    .replace(/\/\*[\s\S]*?\*\//g, "")
    .replace(/(^|\s)\/\/[^\n]*/g, "$1");

/** A quoted literal that is exactly the bare verb, or a text node that is. */
const BARE_VERB = /(["'`])Отменить\1|>\s*Отменить\s*</;

const rel = (file: string) => relative(SRC_ROOT, file).split("\\").join("/");

describe("cancel label", () => {
  const offenders = new Set<string>();
  for (const file of sourceFiles(SRC_ROOT)) {
    if (BARE_VERB.test(withoutComments(readFileSync(file, "utf8")))) {
      offenders.add(rel(file));
    }
  }

  it("says Отмена on buttons that only close a form or a dialog", () => {
    const violations = [...offenders].filter((file) => !(file in ALLOWED));

    expect(
      violations,
      'these files label a control with the bare verb "Отменить": a button that closes without doing anything is "Отмена", and a button that undoes takes an object ("Отменить голос")',
    ).toEqual([]);
  });

  it("keeps no exception for code that no longer has the label", () => {
    const stale = Object.keys(ALLOWED).filter((file) => !offenders.has(file));

    expect(
      stale,
      "these files are allowed the bare verb but no longer use it: drop them from ALLOWED",
    ).toEqual([]);
  });
});
