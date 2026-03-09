import { ref, computed } from "vue";
import { useValidatedField } from "@/shared/lib/composables/useValidatedField";
import { AccountApi } from "@/shared/api";

// Forbidden characters pattern
// See: docs/architecture/USERNAME_POLICY.md
const forbiddenPattern = /[\x00-\x1F\x7F<>"'`\\/@?#%&\[\](){}=~!$^*+|;:\u200B-\u200F\u2028-\u202F\uFEFF]/;

/**
 * Validates username format (sync validation).
 * Returns error message or null if valid.
 */
function validateUsernameFormat(username: string): string | null {
  if (username.length === 0) return null;
  if (username.length < 2) return "Минимум 2 символа";
  if (username.length > 20) return "Максимум 20 символов";
  if (forbiddenPattern.test(username)) return "Недопустимый символ";
  if (/^\s/.test(username)) return "Не может начинаться с пробела";
  if (/\s$/.test(username)) return "Не может заканчиваться пробелом";
  if (/\s{2}/.test(username)) return "Пробелы не могут идти подряд";
  return null;
}

export interface UseUsernameValidationOptions {
  asyncDelay?: number;
}

/**
 * Composable for username validation using useValidatedField.
 * Provides sync format validation + async availability check.
 */
export function useUsernameValidation(options: UseUsernameValidationOptions = {}) {
  const { asyncDelay = 300 } = options;

  const reason = ref<string | null>(null);

  const field = useValidatedField({
    validate: (v) => validateUsernameFormat(v),
    asyncValidate: async (value) => {
      // Skip if too short
      if (value.length < 2) {
        reason.value = null;
        return null;
      }

      const { data, error } = await AccountApi.checkUsername(value);

      if (error || !data) {
        reason.value = null;
        return null; // Silent fail
      }

      if (data.isAvailable) {
        reason.value = null;
        return null;
      } else {
        reason.value = data.reason || null;
        switch (data.reason) {
          case "taken":
            return "Имя уже занято";
          case "reserved":
            return "Имя зарезервировано";
          case "invalid_format":
            return "Недопустимые символы";
          default:
            return "Имя недоступно";
        }
      }
    },
    asyncDelay,
  });

  const unavailableReason = computed(() => {
    switch (reason.value) {
      case "taken":
        return "Имя уже занято";
      case "reserved":
        return "Имя зарезервировано";
      case "invalid_format":
        return "Недопустимые символы";
      default:
        return "Имя недоступно";
    }
  });

  const isAvailable = computed(() => {
    return field.isReady.value && field.value.value.length >= 2;
  });

  return {
    ...field,
    reason,
    unavailableReason,
    isAvailable,
  };
}
