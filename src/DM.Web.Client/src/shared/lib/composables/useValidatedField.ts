import { ref, computed, type Ref } from "vue";
import { pluralize } from "@/shared/lib/utils/pluralize";

export type ValidationResult = string | null | undefined;
export type SyncValidator = (value: string) => ValidationResult;
export type AsyncValidator = (value: string) => Promise<ValidationResult>;

export interface UseValidatedFieldOptions {
  /** Initial value */
  initialValue?: string;
  /** Sync validation (format, required, etc.) */
  validate?: SyncValidator;
  /** Async validation (API check, etc.) */
  asyncValidate?: AsyncValidator;
  /** Delay before async validation (debounce) */
  asyncDelay?: number;
}

export interface ValidatedField {
  /** Current value */
  value: Ref<string>;
  /** Current error message (only shown after blur) */
  error: Ref<string>;
  /** Whether sync validation passes */
  isValid: Ref<boolean>;
  /** Whether async validation is in progress */
  isChecking: Ref<boolean>;
  /** Whether field is ready (sync valid + async checked + no errors) */
  isReady: Ref<boolean>;
  /** Call on input blur */
  onBlur: () => Promise<void>;
  /** Call on input */
  onInput: () => void;
  /** Reset field to initial state */
  reset: (newValue?: string) => void;
  /** Set error manually (e.g., from server) */
  setError: (error: string) => void;
  /** Trigger validation manually */
  validate: () => Promise<boolean>;
}

export function useValidatedField(
  options: UseValidatedFieldOptions = {},
): ValidatedField {
  const { initialValue = "", validate: syncValidate, asyncValidate } = options;

  // State
  const value = ref(initialValue);
  const error = ref("");
  const isChecking = ref(false);
  const touched = ref(false);
  const asyncPassed = ref(false);
  const lastCheckedValue = ref<string | null>(null);

  // Sync validation result (computed, always up-to-date)
  // Returns string (error message, can be empty) or null (no error)
  const syncError = computed((): string | null => {
    if (!syncValidate) return null;
    const result = syncValidate(value.value.trim());
    return result === null || result === undefined ? null : result;
  });

  // Field is valid if syncError is null (not just falsy - empty string is still an error)
  const isValid = computed(() => syncError.value === null);

  // Field is ready when: sync valid + (no async OR async passed)
  const isReady = computed(() => {
    if (!isValid.value) return false;
    if (!asyncValidate) return true;
    return asyncPassed.value && !error.value;
  });

  // Run async validation
  const runAsyncValidation = async (): Promise<string | null> => {
    if (!asyncValidate) return null;

    const trimmed = value.value.trim();

    // Skip if empty or sync invalid
    if (!trimmed || syncError.value) {
      return null;
    }

    // Skip if already checked this exact value
    if (lastCheckedValue.value === trimmed && asyncPassed.value) {
      return null;
    }

    isChecking.value = true;

    try {
      const result = await asyncValidate(trimmed);
      lastCheckedValue.value = trimmed;

      if (result) {
        asyncPassed.value = false;
        return result;
      } else {
        asyncPassed.value = true;
        return null;
      }
    } catch {
      // Silent fail on network errors
      return null;
    } finally {
      isChecking.value = false;
    }
  };

  // Blur handler
  const onBlur = async () => {
    touched.value = true;

    // Check sync validation first
    if (syncError.value) {
      if (error.value !== syncError.value) {
        error.value = syncError.value;
      }
      return;
    }

    // Run async validation
    const asyncError = await runAsyncValidation();
    if (asyncError && error.value !== asyncError) {
      error.value = asyncError;
    } else if (!asyncError && error.value && !syncError.value) {
      // Clear error if async passed and no sync error
      error.value = "";
    }
  };

  // Input handler
  const onInput = () => {
    // Reset async state when value changes
    if (
      lastCheckedValue.value !== null &&
      lastCheckedValue.value !== value.value.trim()
    ) {
      asyncPassed.value = false;
      lastCheckedValue.value = null;
    }

    // Clear error when typing (if there was one)
    if (error.value) {
      error.value = "";
    }
  };

  // Reset field
  const reset = (newValue?: string) => {
    value.value = newValue ?? initialValue;
    error.value = "";
    touched.value = false;
    asyncPassed.value = false;
    lastCheckedValue.value = null;
    isChecking.value = false;
  };

  // Set error manually
  const setError = (err: string) => {
    error.value = err;
    touched.value = true;
  };

  // Manual validation trigger
  const validate = async (): Promise<boolean> => {
    touched.value = true;

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

// Common validators
export const validators = {
  required:
    (message = ""): SyncValidator =>
    (v) =>
      v.trim() ? null : message,

  email:
    (message = "Неверный формат почты"): SyncValidator =>
    (v) =>
      !v || /^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-z]{2,}$/i.test(v)
        ? null
        : message,

  minLength:
    (min: number, message?: string): SyncValidator =>
    (v) =>
      !v || v.length >= min
        ? null
        : message ||
          `Минимум ${min} ${pluralize(min, "символ", "символа", "символов")}`,

  combine:
    (...validators: SyncValidator[]): SyncValidator =>
    (v) => {
      for (const validator of validators) {
        const result = validator(v);
        // Check for non-null result (empty string is still an error)
        if (result !== null && result !== undefined) return result;
      }
      return null;
    },
};
