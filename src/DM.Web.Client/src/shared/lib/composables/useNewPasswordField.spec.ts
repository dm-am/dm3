import { describe, it, expect } from "vitest";
import { ref } from "vue";
import { useNewPasswordField } from "./useNewPasswordField";

/**
 * The rules a password field can judge on its own, and there are two.
 *
 * A third used to live here — a lookup against a public breach index — and it
 * could not work in any build but a developer's own: the document allows
 * connections to this origin and no other, so the request was refused before it
 * left the browser. What the reader saw was a line that appeared for a few
 * milliseconds and never reached a verdict. The server checks, on all three
 * forms that set a password, and its refusal arrives at the field on submit.
 */
describe("useNewPasswordField", () => {
  it("holds the form on the rules the field itself owns", () => {
    const oldPassword = ref("correct horse battery");
    const field = useNewPasswordField({ oldPassword });

    field.password.value = "short";
    expect(field.isValid.value).toBe(false);

    field.password.value = "correct horse battery";
    expect(field.isValid.value).toBe(false);

    field.password.value = "correct horse battery staple";
    expect(field.isValid.value).toBe(true);
  });

  it("says nothing about the old password where there is none", () => {
    const field = useNewPasswordField();

    field.password.value = "correct horse battery";

    expect(field.isSameAsOld.value).toBe(false);
    expect(field.isValid.value).toBe(true);
  });

  it("empties the field on reset", () => {
    const field = useNewPasswordField();
    field.password.value = "correct horse battery";

    field.reset();

    expect(field.password.value).toBe("");
  });
});
