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
 * every session of that account ends, and it signs in with the password alone
 * until the owner sets a factor up again.
 *
 * Afterwards the block itself says what the press did - the factor is off and
 * the sessions are over - in the one line that stands where the button was,
 * and says nothing out loud on top of it.
 */
import { describe, expect, it, vi, afterEach, beforeEach } from "vitest";
import { flushPromises, mount } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { accountApi } from "@/entities/user";
import { UserRole } from "@/entities/user";
import { useToast } from "@/shared/lib/composables/useToast";
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

const toast = useToast();
const saidOutLoud = () => toast.toasts.value.map((t) => t.message);

/** Presses the confirmation of the dialog this block has just opened. */
const confirmDialog = async () => {
  dialog()?.querySelector<HTMLButtonElement>(".dialog-btn-submit")?.click();
  await flushPromises();
};

describe("taking a colleague's factor off", () => {
  beforeEach(() => {
    localStorage.clear();
    setActivePinia(createPinia());
    vi.restoreAllMocks();
    [...toast.toasts.value].forEach((t) => toast.dismiss(t.id));
  });

  afterEach(() => {
    mounted.splice(0).forEach((wrapper) => wrapper.unmount());
  });

  // One button of the section "Аккаунт": the heading is the section's, and
  // the paragraph about when to use it is not moved into the confirmation,
  // which says one sentence, the consequence. The button names the action in
  // the full term, the one the confirmation and the glossary use, rather than
  // lose the half that says what is switched off.
  it("is offered to an administrator looking at somebody else, as one button", () => {
    const wrapper = mountFor(UserRole.Admin);

    expect(wrapper.text()).toBe("Отключить двухфакторную аутентификацию");
    expect(wrapper.findAll("button")).toHaveLength(1);
  });

  it.each([
    [UserRole.SeniorModerator],
    [UserRole.Moderator],
    [UserRole.RegularUser],
  ])("is not drawn for %s", (role) => {
    expect(mountFor(role).text()).toBe("");
  });

  // The server refuses this one with "Отключить свою двухфакторную
  // аутентификацию можно в настройках", and the settings page is where it
  // belongs. Drawing the button here would send the owner down a path that
  // ends in a refusal.
  it("is not drawn on the administrator's own profile", () => {
    expect(mountFor(UserRole.Admin, "admin", "Admin").text()).toBe("");
  });

  it("names the consequence before it is done", async () => {
    const wrapper = mountFor(UserRole.Admin);

    await wrapper.find(".mod-two-factor_actions button").trigger("click");

    // The heading asks with the full term the glossary keeps, and the sentence
    // under it names the one thing the reader weighs: the colleague is signed
    // out everywhere and signs in with the password alone until the factor is
    // set up again. The entry in the security journals is the site's own bookkeeping
    // and no longer takes a third of the card (confirmCopy.spec.ts).
    expect(dialog()?.textContent).toContain(
      "Отключить двухфакторную аутентификацию?",
    );
    expect(dialog()?.textContent).toContain("Сессии этого аккаунта завершатся");
    expect(dialog()?.textContent).toContain(
      "входить он будет по одному паролю",
    );
  });

  it("reports the change once the server confirms it", async () => {
    const clear = vi
      .spyOn(accountApi, "clearTwoFactorFor")
      .mockResolvedValue({ data: null, error: null } as never);

    const wrapper = mountFor(UserRole.Admin);
    await wrapper.find(".mod-two-factor_actions button").trigger("click");
    await confirmDialog();

    // The window prints a refusal in itself, so the interceptor keeps quiet.
    expect(clear).toHaveBeenCalledWith("colleague", {
      ownsRefusal: true,
      ownsRateLimit: true,
    });
    expect(wrapper.emitted("updated")).toHaveLength(1);
    expect(dialog()).toBeNull();
  });

  /**
   * The result on the screen the administrator is left looking at: the block
   * stops offering the action and says what the press did instead - both
   * halves of it, in the confirmation's own words. The ended sessions belong
   * in this line: no screen on the site lists somebody else's sessions, and
   * the reader is looking here, not at a corner.
   *
   * Breaks on: leaving the offer standing after the press, naming only the
   * factor in the line, or reporting either half only in a toast.
   */
  it("says the factor is off and the sessions over, where the button was", async () => {
    vi.spyOn(accountApi, "clearTwoFactorFor").mockResolvedValue({
      data: null,
      error: null,
    } as never);

    const wrapper = mountFor(UserRole.Admin);
    await wrapper.find(".mod-two-factor_actions button").trigger("click");
    await confirmDialog();

    const line = wrapper.find(".mod-two-factor_state");
    expect(line.text()).toBe(
      "Двухфакторная аутентификация отключена, сессии завершены",
    );
    // Whole on every width: where the line is short it breaks as text does,
    // and nothing of it goes under "…".
    expect(line.attributes("data-fit-shrink")).toBeUndefined();
    // The toast this line replaced was a live region; the line has to be one
    // too, or a screen reader loses the answer the sighted reader gets.
    expect(line.attributes("role")).toBe("status");
    expect(wrapper.find(".mod-two-factor_actions").exists()).toBe(false);
  });

  /**
   * And nothing out loud. The line in place already carries the half no screen
   * shows - the ended sessions - so a toast would be the same words from the
   * corner, gone in seconds while the line stays.
   *
   * Breaks on: bringing back "Сессии этого аккаунта завершены", or any other
   * success toast raised beside the line.
   */
  it("says nothing out loud that the line in place does not say", async () => {
    vi.spyOn(accountApi, "clearTwoFactorFor").mockResolvedValue({
      data: null,
      error: null,
    } as never);

    const wrapper = mountFor(UserRole.Admin);
    await wrapper.find(".mod-two-factor_actions button").trigger("click");
    await confirmDialog();

    expect(saidOutLoud()).toEqual([]);
  });

  it("says nothing happened when the server refused", async () => {
    vi.spyOn(accountApi, "clearTwoFactorFor").mockResolvedValue({
      data: null,
      error: { status: 409, title: "Двухфакторная аутентификация не включена" },
    } as never);

    const wrapper = mountFor(UserRole.Admin);
    await wrapper.find(".mod-two-factor_actions button").trigger("click");
    await confirmDialog();

    expect(wrapper.emitted("updated")).toBeUndefined();
    // Nothing was taken off, so the block must not claim a state it does not
    // have: the offer stays, and the server's own words are what the reader is
    // left with.
    expect(wrapper.find(".mod-two-factor_state").exists()).toBe(false);
    expect(wrapper.find(".mod-two-factor_actions button").exists()).toBe(true);
    // Said in the window that asked, which stays open, and nowhere else.
    expect(
      dialog()?.querySelector(".dialog-refusal")?.textContent?.trim(),
    ).toBe("Двухфакторная аутентификация не включена");
    expect(saidOutLoud()).toEqual([]);
  });
});
