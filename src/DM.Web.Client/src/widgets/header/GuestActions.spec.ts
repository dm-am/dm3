/**
 * @vitest-environment jsdom
 */

/** The guest windows a page asks for by address, ?action=. */
import { describe, it, expect, vi, beforeEach } from "vitest";
import { flushPromises } from "@vue/test-utils";
import { mountPage } from "@/shared/lib/testing/mountPage";

// One spy per window, named by the component it would open; the rest of the
// module stays real, as the mount registers its plugin and container.
const { opened } = vi.hoisted(() => ({ opened: [] as string[] }));
vi.mock("vue-final-modal", async (actual) => ({
  ...(await actual<typeof import("vue-final-modal")>()),
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
  });

  it.each([
    ["login", "LoginForm"],
    ["recovery", "AccessRecoveryForm"],
    ["two-factor", "TwoFactorRemovalForm"],
  ])(
    "opens %s over the page and takes it out of the address",
    async (action, window) => {
      const { router } = await mountPage(
        GuestActions,
        `/forum?action=${action}&subtype=AccessRecovery`,
        { routes: [["forum-index", "/forum"]] },
      );
      await flushPromises();

      expect(opened).toEqual([window]);
      expect(router.currentRoute.value.fullPath).toBe(
        "/forum?subtype=AccessRecovery",
      );
    },
  );

  // The asked-for address is not kept: Back leads to the page before it.
  it("takes the place of the address that asked for the window", async () => {
    const { router } = await mountPage(GuestActions, "/forum", {
      routes: [["forum-index", "/forum"]],
    });
    await router.push("/forum?action=login");
    await flushPromises();
    expect(opened).toEqual(["LoginForm"]);

    router.back();
    await flushPromises();

    expect(router.currentRoute.value.fullPath).toBe("/forum");
    expect(opened).toEqual(["LoginForm"]);
  });
});
