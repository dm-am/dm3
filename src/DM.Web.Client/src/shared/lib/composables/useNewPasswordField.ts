import { ref, computed, type Ref } from "vue";
import { useHibpCheck } from "./useHibpCheck";
import type { HibpStatus } from "@/shared/ui/PasswordInput";

export interface UseNewPasswordFieldOptions {
  /** Ref to old password for comparison (password change form) */
  oldPassword?: Ref<string>;
}

/**
 * Composable for new password field with HIBP check and validation
 */
export function useNewPasswordField(options: UseNewPasswordFieldOptions = {}) {
  const password = ref("");
  const {
    isCompromised,
    isChecking,
    checkPassword,
    reset: resetHibp,
  } = useHibpCheck();

  const meetsMinimum = computed(() => password.value.length >= 8);

  const isSameAsOld = computed(
    () =>
      options.oldPassword !== undefined &&
      password.value.length > 0 &&
      password.value === options.oldPassword.value,
  );

  const hibpStatus = computed<HibpStatus>(() => {
    if (isChecking.value) return "checking";
    if (isCompromised.value) return "compromised";
    return "safe";
  });

  /**
   * Whether the form may be sent.
   *
   * The wait for the breach lookup is not part of it. An answer still on the
   * wire used to disable the submit button while nothing on screen said why,
   * which reads as a control that stopped working; the wait belongs under the
   * field, and the strength indicator names it there.
   *
   * The verdict is part of it, and stays. The server refuses a breached
   * password too (RefusalMessage.PasswordBreached), but both lookups are
   * fail-open: HibpPasswordChecker allows the password whenever its own call to
   * HIBP does not answer, so a verdict the client already holds is the only one
   * guaranteed to be there.
   */
  const isValid = computed(
    () => meetsMinimum.value && !isCompromised.value && !isSameAsOld.value,
  );

  const onInput = () => {
    resetHibp();
  };

  const onBlur = async () => {
    if (meetsMinimum.value && !isSameAsOld.value) {
      await checkPassword(password.value);
    }
  };

  const reset = () => {
    password.value = "";
    resetHibp();
  };

  return {
    password,
    hibpStatus,
    isSameAsOld,
    isValid,
    isChecking,
    onInput,
    onBlur,
    reset,
  };
}
