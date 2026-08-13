import { ref, computed, type Ref } from "vue";

export interface UseNewPasswordFieldOptions {
  /** Ref to old password for comparison (password change form) */
  oldPassword?: Ref<string>;
}

/**
 * Composable for a new password field: the rules the field itself can judge.
 */
export function useNewPasswordField(options: UseNewPasswordFieldOptions = {}) {
  const password = ref("");

  const meetsMinimum = computed(() => password.value.length >= 8);

  const isSameAsOld = computed(
    () =>
      options.oldPassword !== undefined &&
      password.value.length > 0 &&
      password.value === options.oldPassword.value,
  );

  /**
   * Whether the form may be sent.
   *
   * Breaches are not on this list, and there is nowhere on the client they could
   * be. The document's Content-Security-Policy allows connections to this origin
   * and no other, so the lookup that used to run here was refused before it left
   * the browser in every build except a developer's own — the field showed
   * "checking" for a few milliseconds and never reached a verdict.
   *
   * The server checks, on all three forms that set a password, and its refusal
   * arrives at the field on submit as RefusalMessage.PasswordBreached.
   */
  const isValid = computed(() => meetsMinimum.value && !isSameAsOld.value);

  const reset = () => {
    password.value = "";
  };

  return {
    password,
    isSameAsOld,
    isValid,
    reset,
  };
}
