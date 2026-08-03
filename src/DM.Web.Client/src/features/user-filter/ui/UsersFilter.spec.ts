/**
 * @vitest-environment node
 */

/**
 * The hint under a filter name is the list the filter opens. Written by hand it
 * drifted from the list: the role hint named three roles of six, lost "Старший
 * модератор" and "Пользователь", and invented the form "Админ", which exists
 * nowhere else on the site. The list is derived now, and this keeps it derived:
 * a hint typed back in by hand fails the second check.
 */
import { describe, it, expect } from "vitest";
import { readFileSync } from "fs";
import { dirname, join } from "path";
import { fileURLToPath } from "url";
import { optionsHint, ROLE_OPTIONS } from "../model";

const HERE = dirname(fileURLToPath(import.meta.url));
const SOURCE = readFileSync(join(HERE, "UsersFilter.vue"), "utf8");

describe("users filter hints", () => {
  it("names every role the filter offers", () => {
    expect(optionsHint(ROLE_OPTIONS)).toBe(
      "Пользователь, Наставник, Модератор, Старший модератор, Администратор",
    );
  });

  it("derives the root hints instead of spelling them", () => {
    const derived = [
      "ACTIVITY_OPTIONS",
      "ROLE_OPTIONS",
      "EXPERIENCE_OPTIONS",
    ].filter((list) => !SOURCE.includes(`optionsHint(${list})`));

    expect(derived).toEqual([]);
  });
});
