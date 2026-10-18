/**
 * @vitest-environment jsdom
 */

/**
 * The guest windows a page asks for by address.
 *
 * Any page may open one of the header's guest windows with ?action=, and the
 * header opens it over the page and takes the word out of the address. The
 * support page names two of them under "Восстановление доступа": the recovery
 * of access, and the request to switch the second factor off for a reader who
 * lost the phone and the codes. The second was reachable from the code step of
 * the sign-in card alone.
 */
import { describe, it, expect, vi, beforeEach } from "vitest";
import { mount, flushPromises } from "@vue/test-utils";

const { route, replace } = vi.hoisted(() => ({
  route: { query: {} as Record<string, string> },
  replace: vi.fn(),
}));
vi.mock("vue-router", () => ({
  useRoute: () => route,
  useRouter: () => ({ replace }),
}));

// One spy per window, named by the component it would open.
const { opened } = vi.hoisted(() => ({ opened: [] as string[] }));
vi.mock("vue-final-modal", () => ({
  useModal: (options: { component: { name: string } }) => ({
    open: () => opened.push(options.component.name),
    close: vi.fn(),
  }),
}));

vi.mock("@/features/auth", () => ({
  LoginForm: { name: "LoginForm" },
  RegistrationForm: { name: "RegistrationForm" },
  RegistrationSuccess: { name: "RegistrationSuccess" },
  AccessRecoveryForm: { name: "AccessRecoveryForm" },
  TwoFactorRemovalForm: { name: "TwoFactorRemovalForm" },
}));

const { default: GuestActions } = await import("./GuestActions.vue");

describe("the guest windows asked for by address", () => {
  beforeEach(() => {
    opened.length = 0;
    replace.mockReset();
  });

  it.each([
    ["login", "LoginForm"],
    ["recovery", "AccessRecoveryForm"],
    ["two-factor", "TwoFactorRemovalForm"],
  ])(
    "opens %s over the page and takes it out of the address",
    async (action, window) => {
      route.query = { action, subtype: "AccessRecovery" };

      mount(GuestActions);
      await flushPromises();

      expect(opened).toEqual([window]);
      expect(replace).toHaveBeenCalledWith({
        query: { subtype: "AccessRecovery" },
      });
    },
  );
});
