/**
 * @vitest-environment jsdom
 */

/**
 * Removing an account from the "Модерация" tab of a profile: a button in the
 * section "Аккаунт" that opens the form window (AccountRemovalDialog), and the
 * way off the profile once the window reports the removal done.
 *
 * The reason used to be a field open on the page, next to the everyday actions
 * of the panel. What is held here is who sees the button, that pressing it
 * opens the window and sends nothing by itself, and what the screen does after
 * a removal. The window's own form has its own spec.
 */
import { describe, it, expect, vi, beforeEach } from "vitest";
import { mount } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import ModerationAccountRemoval from "./ModerationAccountRemoval.vue";
import AccountRemovalDialog from "./AccountRemovalDialog.vue";
import { useToast } from "@/shared/lib/composables/useToast";
import { useAuthStore } from "@/shared/stores";
import { UserRole } from "@/shared/api/models/common";

const removeAccount = vi.hoisted(() => vi.fn());

vi.mock("@/entities/moderation", () => ({
  moderationApi: { removeAccount },
}));

const { push } = vi.hoisted(() => ({ push: vi.fn() }));

// Only the navigation the removal ends with; nothing else here touches the
// router.
vi.mock("vue-router", async (importOriginal) => ({
  ...(await importOriginal<typeof import("vue-router")>()),
  useRouter: () => ({ push }),
}));

/** The window is registered with the library, not mounted: its attrs are read. */
type ModalRegistration = {
  component: unknown;
  attrs: Record<string, unknown>;
};

const modal = vi.hoisted(() => ({
  registered: [] as { component: unknown; attrs: Record<string, unknown> }[],
  open: vi.fn(),
  close: vi.fn(),
}));

vi.mock("vue-final-modal", () => ({
  useModal: (options: ModalRegistration) => {
    modal.registered.push(options);
    return { open: modal.open, close: modal.close };
  },
}));

const signedInAs = (role: UserRole, username = "senior") => {
  const auth = useAuthStore();
  auth.user = { username, role } as never;
};

const mountIt = (targetUsername = "reader") =>
  mount(ModerationAccountRemoval, { props: { targetUsername } });

const button = (wrapper: ReturnType<typeof mountIt>) =>
  wrapper.find(".mod-removal_actions button");

const removalWindow = () => {
  const found = modal.registered.at(-1);
  if (!found) throw new Error("the removal window was never registered");
  return found;
};

const toast = useToast();
const saidOutLoud = () => toast.toasts.value.map((t) => t.message);

describe("removing an account from the moderated profile", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    push.mockClear();
    modal.open.mockClear();
    modal.close.mockClear();
    modal.registered.length = 0;
    removeAccount.mockReset();
    [...toast.toasts.value].forEach((t) => toast.dismiss(t.id));
  });

  // The owner asked for removal to stay an exception that goes through
  // moderation. An ordinary moderator is not who decides it.
  it("is drawn for a senior moderator and for nobody below", () => {
    signedInAs(UserRole.SeniorModerator);
    expect(button(mountIt()).exists()).toBe(true);

    setActivePinia(createPinia());
    signedInAs(UserRole.Moderator);
    expect(button(mountIt()).exists()).toBe(false);
  });

  it("is not drawn on the viewer's own profile", () => {
    signedInAs(UserRole.Admin, "reader");
    expect(button(mountIt("reader")).exists()).toBe(false);
  });

  /**
   * The reason is asked in a window, and nothing leaves by the press on the
   * page: the field no longer stands open on the profile.
   */
  it("opens the window for the account and sends nothing by itself", async () => {
    signedInAs(UserRole.SeniorModerator);
    const wrapper = mountIt("reader");

    expect(wrapper.find("textarea").exists()).toBe(false);
    await button(wrapper).trigger("click");

    expect(removalWindow().component).toBe(AccountRemovalDialog);
    expect(removalWindow().attrs.targetUsername).toBe("reader");
    expect(modal.open).toHaveBeenCalledTimes(1);
    expect(removeAccount).not.toHaveBeenCalled();
  });

  /**
   * The removal has to be readable afterwards. It cannot be read on the
   * profile - a removed account is dropped by the user lookup, so this page
   * answers 404 from here on - and "Удаленные пользователи" is the one screen that
   * can see such an account. It takes the name out of the address, so the
   * moderator lands on the row he has just made. Nothing is said out loud on
   * top of it.
   *
   * Breaks on: dropping the navigation, sending the moderator to the list
   * unfiltered, or a success toast beside it.
   */
  it("takes the moderator to the removed account the window reports", () => {
    signedInAs(UserRole.SeniorModerator);
    const wrapper = mountIt("reader");

    (removalWindow().attrs.onSuccess as () => void)();

    expect(modal.close).toHaveBeenCalled();
    expect(wrapper.emitted("updated")).toHaveLength(1);
    expect(push).toHaveBeenCalledWith({
      name: "moderation-removed-accounts",
      query: { search: "reader" },
    });
    expect(saidOutLoud()).toEqual([]);
  });

  it("stays where it is when the window is left", () => {
    signedInAs(UserRole.SeniorModerator);
    mountIt("reader");

    (removalWindow().attrs.onCancel as () => void)();

    expect(modal.close).toHaveBeenCalled();
    expect(push).not.toHaveBeenCalled();
  });
});
