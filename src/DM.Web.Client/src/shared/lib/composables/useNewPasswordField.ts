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

  const isValid = computed(
    () =>
      meetsMinimum.value &&
      !isChecking.value &&
      !isCompromised.value &&
      !isSameAsOld.value,
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
