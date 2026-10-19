/**
 * @vitest-environment jsdom
 */

import { describe, expect, it } from "vitest";
import { dirname, join } from "path";
import { fileURLToPath } from "url";
import { componentCss, ruleMap } from "@/shared/lib/testing/styles";

const SFC = join(dirname(fileURLToPath(import.meta.url)), "ButtonRow.vue");

describe("ButtonRow", () => {
  // jsdom lays nothing out, so the stylesheet is what is read: a row that
  // kept every button on one line whatever its room stood over the next
  // column of the page at 1024 and 1280.
  it("sends a button with no room in the line down to the next one", () => {
    const row = ruleMap(componentCss(SFC)).get(".button-row");
    expect(row?.["flex-wrap"]).toBe("wrap");
    expect(componentCss(SFC)).not.toMatch(/@media|@container/);
  });

  it("lets nothing in it be wider than the row", () => {
    const rules = ruleMap(componentCss(SFC));
    const item = [...rules].find(([selector]) =>
      /^.button-row > /.test(selector),
    )?.[1];
    expect(item?.["max-width"]).toBe("100%");
    const button = [...rules].find(([selector]) =>
      /^.button-row .*button/.test(selector),
    )?.[1];
    expect(button).toMatchObject({
      "max-width": "100%",
      "white-space": "normal",
    });
  });
});
