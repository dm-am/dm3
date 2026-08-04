/**
 * @vitest-environment node
 */

/**
 * Contacts are the one list on the profile a reader builds by hand, and neither
 * of a contact's two fields is an identity. `contactType` is free text and
 * nothing stops two rows from both being "Telegram" — the display list keyed by
 * it, which is a duplicate key, a warning in the console and a patch that can
 * leave one of the two rows stale. The editor keyed by index while offering a
 * splice: delete a row from the middle and the caret stays on an input that now
 * holds the NEXT contact's data, so the reader goes on typing into someone
 * else's record.
 *
 * The fix is an identity per draft row, minted locally and stripped before the
 * value is sent. Read from the template, because the key expression IS the
 * defect and the template is the only place it can come back.
 */
import { describe, it, expect } from "vitest";
import { readFileSync } from "fs";
import { dirname, join } from "path";
import { fileURLToPath } from "url";

const HERE = dirname(fileURLToPath(import.meta.url));
const source = readFileSync(join(HERE, "ProfilePersonalInfo.vue"), "utf8");

describe("the contacts of a profile", () => {
  it("never keys a row by the contact type", () => {
    expect(source).not.toContain(':key="contact.contactType"');
  });

  it("gives an editable row an identity that survives a delete", () => {
    expect(source).toContain(':key="contact.uid"');
  });

  it("keeps that identity out of what the server is told", () => {
    // The draft carries a field the DTO does not have; the emit must not.
    expect(source).toMatch(/JSON\.stringify\(asContacts\(/);
    expect(source).toMatch(/function asContacts\(/);
  });
});
