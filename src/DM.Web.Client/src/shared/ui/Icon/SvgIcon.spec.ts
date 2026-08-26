/**
 * @vitest-environment jsdom
 */

/**
 * Forty icons, one name each, and until now the name could be anything. The
 * registry was annotated `Record<string, IconDefinition>`, which erases the
 * literal keys, so `IconName = keyof typeof icons` unfolded into plain `string`
 * — the type read like a guarantee and checked nothing. `<SvgIcon name="inbox"
 * />` passed vue-tsc; at runtime `icons.inbox` was undefined and `icon.viewBox`
 * threw during render, which in Vue takes down the whole subtree: a white page,
 * not a missing icon.
 *
 * Two locks, because they fail differently. `satisfies` closes the type, so the
 * next typo is a compile error — that is the one that matters, and it is
 * asserted from the source, since a passing compile cannot be observed from
 * inside a running test. The fallback closes the runtime, so a name that gets
 * past the compiler anyway (a cast, a value from the API) costs one wrong glyph
 * instead of a page.
 */
import { describe, it, expect } from "vitest";
import { readFileSync } from "fs";
import { dirname, join, resolve } from "path";
import { fileURLToPath } from "url";
import { mount } from "@vue/test-utils";
import SvgIcon from "./SvgIcon.vue";
import { icons, type IconName } from "@/shared/lib/utils/icons";

const HERE = dirname(fileURLToPath(import.meta.url));
// Icon -> ui -> shared -> src
const CLIENT_SRC = resolve(HERE, "..", "..", "..");

describe("SvgIcon", () => {
  it("draws the icon it is named after", () => {
    const wrapper = mount(SvgIcon, { props: { name: "pencil" } });
    expect(wrapper.attributes("viewBox")).toBe(icons.pencil.viewBox);
  });

  it("draws a question mark instead of taking the page down", () => {
    // The cast is the point: this is the case where a name got past the type,
    // and the assertion is that the render survives it.
    const name = "inbox" as unknown as IconName;
    const wrapper = mount(SvgIcon, { props: { name } });
    expect(wrapper.attributes("viewBox")).toBe(icons.question.viewBox);
  });

  it("is named by a closed set of names", () => {
    const source = readFileSync(
      join(CLIENT_SRC, "shared/lib/utils/icons.ts"),
      "utf8",
    );
    // The annotation is the defect: it erases the keys the union is made of.
    expect(source).not.toMatch(
      /export const icons\s*:\s*Record<string,\s*IconDefinition>/,
    );
    expect(source).toMatch(/\}\s*satisfies Record<string, IconDefinition>;/);
  });

  it("is not widened again by a consumer", () => {
    // EmptyState used to take `icon?: string`, which handed the hole straight
    // back to its own callers. It carries no icon at all any more — the empty
    // state is a sentence now — so the guard moved to the consumer that still
    // names one: the address block picks its mark out of the same registry.
    const source = readFileSync(
      join(CLIENT_SRC, "shared/config/site.ts"),
      "utf8",
    );
    expect(source).toMatch(/icon:\s*IconName\s*\}/);
  });
});
