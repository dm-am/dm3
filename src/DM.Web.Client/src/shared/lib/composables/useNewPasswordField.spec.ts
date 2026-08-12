import { describe, it, expect, vi, beforeEach } from "vitest";
import { ref } from "vue";

/**
 * The breach lookup is replaced by one that never leaves the process: what this
 * spec asks is which rules hold the form, and the answer must not depend on
 * api.pwnedpasswords.com being reachable from the test runner. The stand-in
 * stays out until the test releases it, and answers "found" for a password
 * spelt with "breached".
 */
const { pendingLookups } = vi.hoisted(() => ({
  pendingLookups: [] as (() => void)[],
}));

vi.mock("./useHibpCheck", async () => {
  const vue = await import("vue");
  return {
    useHibpCheck: () => {
      const isCompromised = vue.ref(false);
      const isChecking = vue.ref(false);
      return {
        isCompromised,
        isChecking,
        error: vue.ref<string | null>(null),
        checkPassword: (password: string) =>
          new Promise<boolean>((resolve) => {
            isChecking.value = true;
            pendingLookups.push(() => {
              isCompromised.value = password.includes("breached");
              isChecking.value = false;
              resolve(isCompromised.value);
            });
          }),
        reset: () => {
          isCompromised.value = false;
          isChecking.value = false;
        },
      };
    },
  };
});

import { useNewPasswordField } from "./useNewPasswordField";

/** Lets the lookup that is currently out come back. */
const answerLookup = () => pendingLookups.shift()?.();

describe("useNewPasswordField", () => {
  beforeEach(() => {
    pendingLookups.length = 0;
  });

  it("does not hold the form while the breach lookup is out", async () => {
    const field = useNewPasswordField();
    field.password.value = "correct horse";

    const lookup = field.onBlur();

    expect(field.isChecking.value).toBe(true);
    // The state the indicator paints as "Проверяем по базе утечек...": that is
    // where the wait belongs. Held here instead, it was a submit button that
    // went dead with nothing on screen saying why.
    expect(field.hibpStatus.value).toBe("checking");
    expect(field.isValid.value).toBe(true);

    answerLookup();
    await lookup;

    expect(field.isValid.value).toBe(true);
  });

  it("holds the form once the lookup finds the password in a breach", async () => {
    const field = useNewPasswordField();
    field.password.value = "breached horse";

    const lookup = field.onBlur();
    answerLookup();
    await lookup;

    expect(field.hibpStatus.value).toBe("compromised");
    // The server refuses a breached password too, but its own check is
    // fail-open: when HIBP does not answer it, the password goes through. A
    // verdict the client already holds is the only one guaranteed to be there.
    expect(field.isValid.value).toBe(false);
  });

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
});
