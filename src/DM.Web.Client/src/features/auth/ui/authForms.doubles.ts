import { vi } from "vitest";
import type { RegisterCredentials } from "@/shared/api/models/account";

export const signIn = vi.fn();
export const completeSecondFactor = vi.fn();
/** What the form is told about the registration: the failure, or null once the letter is on its way. */
export const register = vi.fn();
export const checkEmail = vi.fn();

export const recover = vi.fn();

// One instance, so "the card said it and nothing else did" can be asserted.
export const toast = { error: vi.fn(), warning: vi.fn(), success: vi.fn() };

export const userEntity = () => ({
  signIn,
  completeSecondFactor,
  accountApi: {
    recover,
    checkEmail,
    // An accepted registration names the hours of its link; not the default, so a kept number would show.
    register: async (credentials: RegisterCredentials) => {
      const error = await register(credentials);
      return {
        data: error ? null : { resource: { linkLifetimeHours: 30 } },
        error,
      };
    },
  },
});

export const toastComposable = () => ({ useToast: () => toast });
