import { describe, it, expect } from "vitest";
import { ref } from "vue";
import { useNewPasswordField } from "./useNewPasswordField";

// The two rules a password field judges itself; breaches are the server's.
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
