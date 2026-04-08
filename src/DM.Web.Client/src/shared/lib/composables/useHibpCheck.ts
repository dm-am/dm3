import { ref } from "vue";

export interface HibpCheckOptions {
  /**
   * Custom fetch function for testing.
   * Defaults to global fetch.
   */
  fetchFn?: typeof fetch;
}

/**
 * Composable for checking passwords against HaveIBeenPwned database
 * Uses k-Anonymity model - only first 5 chars of SHA-1 hash are sent
 */
export function useHibpCheck(options: HibpCheckOptions = {}) {
  const { fetchFn = fetch } = options;
  const isCompromised = ref(false);
  const isChecking = ref(false);
  const error = ref<string | null>(null);

  /**
   * Compute SHA-1 hash of a string
   */
  async function sha1(text: string): Promise<string> {
    const encoder = new TextEncoder();
    const data = encoder.encode(text);
    const hashBuffer = await crypto.subtle.digest("SHA-1", data);
    const hashArray = Array.from(new Uint8Array(hashBuffer));
    return hashArray.map((b) => b.toString(16).padStart(2, "0")).join("");
  }

  /**
   * Check if password has been compromised in data breaches
   * Returns true if password was found in HIBP database
   */
  async function checkPassword(password: string): Promise<boolean> {
    if (!password || password.length < 4) {
      isCompromised.value = false;
      return false;
    }

    isChecking.value = true;
    error.value = null;

    try {
      const hash = await sha1(password);
      const prefix = hash.substring(0, 5).toUpperCase();
      const suffix = hash.substring(5).toUpperCase();

      const response = await fetchFn(
        `https://api.pwnedpasswords.com/range/${prefix}`,
        {
          headers: {
            "Add-Padding": "true", // Enhanced privacy
          },
        },
      );

      if (!response.ok) {
        throw new Error(`HIBP API error: ${response.status}`);
      }

      const text = await response.text();
      const lines = text.split("\n");

      // Check if our suffix is in the response
      const found = lines.some((line) => {
        const [hashSuffix] = line.split(":");
        return hashSuffix.trim() === suffix;
      });

      isCompromised.value = found;
      return found;
    } catch (e) {
      // Don't block registration if HIBP is unavailable
      console.warn("HIBP check failed:", e);
      error.value = "Не удалось проверить пароль";
      isCompromised.value = false;
      return false;
    } finally {
      isChecking.value = false;
    }
  }

  /**
   * Reset state
   */
  function reset() {
    isCompromised.value = false;
    isChecking.value = false;
    error.value = null;
  }

  return {
    isCompromised,
    isChecking,
    error,
    checkPassword,
    reset,
  };
}
