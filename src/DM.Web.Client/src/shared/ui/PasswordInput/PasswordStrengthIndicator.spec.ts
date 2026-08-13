import { describe, it, expect } from "vitest";
import { mount } from "@vue/test-utils";
import PasswordStrengthIndicator from "./PasswordStrengthIndicator.vue";

/**
 * The single line under the bar, and what wins it.
 *
 * A rule the field itself broke comes first, because it names what to correct;
 * the strength comes after, because it is advice rather than a refusal. There
 * used to be a third state between them, a lookup against a public breach index,
 * and it could not reach a verdict in any build but a developer's own — the
 * document allows connections to this origin only.
 */
const lineUnderTheBar = (password: string, isSameAsOld = false) =>
  mount(PasswordStrengthIndicator, { props: { password, isSameAsOld } }).text();

describe("PasswordStrengthIndicator", () => {
  it("names the rule the field broke before anything else", () => {
    expect(lineUnderTheBar("short")).toBe("Минимум 8 символов");
  });

  it("names the strength of a password that breaks no rule", () => {
    expect(lineUnderTheBar("correct horse")).toBe("Надежный");
  });

  it("keeps the old password above the strength", () => {
    expect(lineUnderTheBar("correct horse", true)).toBe(
      "Новый пароль совпадает с текущим",
    );
  });

  it("says nothing at all about an empty field", () => {
    expect(lineUnderTheBar("")).toBe("");
  });
});
