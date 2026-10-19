/**
 * @vitest-environment node
 */

/**
 * A button that only closes a form or a dialog is "Отмена"; the verb
 * "Отменить" undoes something and carries an object ("Отменить голос"), see
 * docs/conventions/UI_STANDARDS.md. Only a label that is the bare verb fails.
 */
import { describe, it, expect } from "vitest";
import { readFileSync } from "fs";
import { stripComments } from "@/shared/lib/testing/comments";
import { CLIENT_SRC, asPath, collect } from "@/shared/lib/testing/sourceTree";

/** Bare "Отменить" that is not a violation, each still matching something. */
const ALLOWED: Record<string, string> = {
  // Row action in the list of sent invitations: it revokes an invitation that
  // exists, and the row itself names the object.
  "pages/blog/settings/InvitationsSection.vue": "revokes a sent invitation",
  "pages/game/settings/InvitationsSection.vue": "revokes a sent invitation",
};

const isSource = (name: string): boolean =>
  (name.endsWith(".vue") || name.endsWith(".ts")) && !name.endsWith(".spec.ts");

/** A quoted literal that is exactly the bare verb, or a text node that is. */
const BARE_VERB = /(["'`])Отменить\1|>\s*Отменить\s*</;

describe("cancel label", () => {
  const offenders = new Set<string>();
  for (const file of collect(CLIENT_SRC, isSource)) {
    if (BARE_VERB.test(stripComments(readFileSync(file, "utf8")))) {
      offenders.add(asPath(file));
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
