import { ref, computed, type Ref } from "vue";
import { EMAIL_INVALID } from "@/shared/lib/errors/validationErrors";

type ValidationResult = string | null | undefined;
export type SyncValidator = (value: string) => ValidationResult;
type AsyncValidator = (value: string) => Promise<ValidationResult>;

export interface UseValidatedFieldOptions {
  initialValue?: string;
  /** Sync validation (format, required, etc.) */
  validate?: SyncValidator;
  /** Async validation (API check, etc.) */
  asyncValidate?: AsyncValidator;
}

export interface ValidatedField {
  value: Ref<string>;
  /** Shown only after blur. */
  error: Ref<string>;
  /** Whether sync validation passes */
  isValid: Ref<boolean>;
  isChecking: Ref<boolean>;
  /** Sync valid, async checked, no errors. */
  isReady: Ref<boolean>;
  onBlur: () => Promise<void>;
  onInput: () => void;
  reset: (newValue?: string) => void;
  /** E.g. from the server. */
  setError: (error: string) => void;
  validate: () => Promise<boolean>;
}

export function useValidatedField(
  options: UseValidatedFieldOptions = {},
): ValidatedField {
  const { initialValue = "", validate: syncValidate, asyncValidate } = options;

  const value = ref(initialValue);
  const error = ref("");
  const isChecking = ref(false);
  const asyncPassed = ref(false);
  const lastCheckedValue = ref<string | null>(null);

  // A message, possibly empty, or null for no error.
  const syncError = computed((): string | null => {
    if (!syncValidate) return null;
    const result = syncValidate(value.value.trim());
    return result ?? null;
  });

  // An empty message is still an error.
  const isValid = computed(() => syncError.value === null);

  const isReady = computed(() => {
    if (!isValid.value) return false;
    if (!asyncValidate) return true;
    return asyncPassed.value && !error.value;
  });

  const runAsyncValidation = async (): Promise<string | null> => {
    if (!asyncValidate) return null;

    const trimmed = value.value.trim();

    if (!trimmed || syncError.value) {
      return null;
    }

    if (lastCheckedValue.value === trimmed && asyncPassed.value) {
      return null;
    }

    isChecking.value = true;

    try {
      const result = await asyncValidate(trimmed);
      lastCheckedValue.value = trimmed;

      asyncPassed.value = !result;
      return result || null;
    } catch {
      // Silent fail on network errors
      return null;
    } finally {
      isChecking.value = false;
    }
  };

  const onBlur = async () => {
    if (syncError.value) {
      error.value = syncError.value;
      return;
    }

    const asyncError = await runAsyncValidation();
    if (asyncError) {
      error.value = asyncError;
    } else if (!syncError.value) {
      // The value may have changed while the check ran.
      error.value = "";
    }
  };

  const onInput = () => {
    // A changed value is checked anew.
    if (
      lastCheckedValue.value !== null &&
      lastCheckedValue.value !== value.value.trim()
    ) {
      asyncPassed.value = false;
      lastCheckedValue.value = null;
    }

    error.value = "";
  };

  const reset = (newValue?: string) => {
    value.value = newValue ?? initialValue;
    error.value = "";
    asyncPassed.value = false;
    lastCheckedValue.value = null;
    isChecking.value = false;
  };

  const setError = (err: string) => {
    error.value = err;
  };

  const validate = async (): Promise<boolean> => {
    if (syncError.value) {
      error.value = syncError.value;
      return false;
    }

    const asyncError = await runAsyncValidation();
    if (asyncError) {
      error.value = asyncError;
      return false;
    }

    error.value = "";
    return true;
  };

  return {
    value,
    error,
    isValid,
    isChecking,
    isReady,
    onBlur,
    onInput,
    reset,
    setError,
    validate,
  };
}

/**
 * Whether an <input type="email"> takes the text: the browser's own reading of
 * an address, so the form keeps no pattern of its own; the server has the last
 * word on the rest.
 */
function emailShaped(value: string): boolean {
  const probe = document.createElement("input");
  probe.type = "email";
  probe.value = value;
  return !probe.validity.typeMismatch;
}

export const validators = {
  required:
    (message = ""): SyncValidator =>
    (v) =>
      v.trim() ? null : message,

  email:
    (message = EMAIL_INVALID): SyncValidator =>
    (v) =>
      !v || emailShaped(v) ? null : message,

  combine:
    (...validators: SyncValidator[]): SyncValidator =>
    (v) => {
      for (const validator of validators) {
        const result = validator(v);
        // An empty message is still an error.
        if (result !== null && result !== undefined) return result;
      }
      return null;
    },
};
