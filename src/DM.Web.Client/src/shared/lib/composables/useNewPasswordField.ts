import { ref, computed, type Ref } from "vue";
import { PASSWORD_MIN_LENGTH } from "@/shared/lib/constants/user";

export interface UseNewPasswordFieldOptions {
  /** Ref to old password for comparison (password change form) */
  oldPassword?: Ref<string>;
}

/** The length rule (PASSWORD_MIN_LENGTH), for the form that sends and the scale that shows it. */
export const meetsPasswordMinimum = (password: string) =>
  password.length >= PASSWORD_MIN_LENGTH;

/** A new password field: the rules the field itself can judge. */
export function useNewPasswordField(options: UseNewPasswordFieldOptions = {}) {
  const password = ref("");

  const isSameAsOld = computed(
    () =>
      options.oldPassword !== undefined &&
      password.value.length > 0 &&
      password.value === options.oldPassword.value,
  );

  /**
   * Whether the form may be sent. Breaches are the server's to check (the CSP
   * allows no lookup elsewhere); its refusal arrives as PasswordBreached.
   */
  const isValid = computed(
    () => meetsPasswordMinimum(password.value) && !isSameAsOld.value,
  );

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
