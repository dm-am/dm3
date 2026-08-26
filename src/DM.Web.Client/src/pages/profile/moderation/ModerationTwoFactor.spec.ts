/**
 * @vitest-environment jsdom
 */

/**
 * Taking a colleague's second factor off, from the moderation panel of their
 * profile.
 *
 * Who may see the control is the whole of what is worth gating here, because
 * the server sends no permission for it and the profile carries no "this
 * account has a factor" flag either. So the rule is the viewer's own rank, plus
 * the one case the server refuses by design: an administrator aiming the action
 * at himself, whose own factor is switched off in his settings.
 *
 * The consequences belong in the confirmation and not in a toast afterwards:
 * every session of that account ends, and its rank stays withheld until the
 * owner sets a factor up again.
 */
import { describe, expect, it, vi, afterEach, beforeEach } from "vitest";
import { flushPromises, mount } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { accountApi } from "@/entities/user";
import { UserRole } from "@/entities/user";
import { useAuthStore } from "@/shared/stores";
import ModerationTwoFactor from "./ModerationTwoFactor.vue";

/** Every wrapper mounted by a test, so the teleported dialogs go away with it. */
const mounted: { unmount: () => void }[] = [];

function mountFor(role: UserRole, viewer = "admin", target = "colleague") {
  const pinia = createPinia();
  setActivePinia(pinia);
  useAuthStore().updateUser({ username: viewer, role } as never);

  const wrapper = mount(ModerationTwoFactor, {
    props: { targetUsername: target },
    global: { plugins: [pinia] },
    attachTo: document.body,
  });
  mounted.push(wrapper);
  return wrapper;
}

/**
 * The confirmation, which is teleported to the body. Left mounted, one test's
 * dialog is the first one the next test's query finds - and the assertions
 * then run against a dialog nobody is looking at.
 */
const dialog = () =>
  [...document.querySelectorAll(".dialog-container")].at(-1) ?? null;

describe("taking a colleague's factor off", () => {
  beforeEach(() => {
    localStorage.clear();
    setActivePinia(createPinia());
    vi.restoreAllMocks();
  });

  afterEach(() => {
    mounted.splice(0).forEach((wrapper) => wrapper.unmount());
  });

  it("is offered to an administrator looking at somebody else", () => {
    const wrapper = mountFor(UserRole.Admin);

    expect(wrapper.text()).toContain("Снять второй фактор");
  });

  it.each([
    [UserRole.SeniorModerator],
    [UserRole.Moderator],
    [UserRole.RegularUser],
  ])("is not drawn for %s", (role) => {
    expect(mountFor(role).text()).toBe("");
  });

  // The server refuses this one with "Свой второй фактор снимают в настройках",
  // and the settings page is where it belongs. Drawing the button here would
  // send the owner down a path that ends in a refusal.
  it("is not drawn on the administrator's own profile", () => {
    expect(mountFor(UserRole.Admin, "admin", "Admin").text()).toBe("");
  });

  it("names every consequence before it is done", async () => {
    const wrapper = mountFor(UserRole.Admin);

    await wrapper.find(".mod-two-factor_actions button").trigger("click");

    expect(dialog()?.textContent).toContain("все сессии этого аккаунта");
    expect(dialog()?.textContent).toContain("не заработают");
    expect(dialog()?.textContent).toContain("журналы безопасности обоих");
  });

  it("reports the change once the server confirms it", async () => {
    const clear = vi
      .spyOn(accountApi, "clearTwoFactorFor")
      .mockResolvedValue({ data: null, error: null } as never);

    const wrapper = mountFor(UserRole.Admin);
    await wrapper.find(".mod-two-factor_actions button").trigger("click");

    dialog()?.querySelector<HTMLButtonElement>(".dialog-btn-submit")?.click();
    await flushPromises();

    expect(clear).toHaveBeenCalledWith("colleague");
    expect(wrapper.emitted("updated")).toHaveLength(1);
  });

  it("says nothing happened when the server refused", async () => {
    vi.spyOn(accountApi, "clearTwoFactorFor").mockResolvedValue({
      data: null,
      error: { status: 409, title: "Второй фактор не включен" },
    } as never);

    const wrapper = mountFor(UserRole.Admin);
    await wrapper.find(".mod-two-factor_actions button").trigger("click");

    dialog()?.querySelector<HTMLButtonElement>(".dialog-btn-submit")?.click();
    await flushPromises();

    expect(wrapper.emitted("updated")).toBeUndefined();
  });
});
