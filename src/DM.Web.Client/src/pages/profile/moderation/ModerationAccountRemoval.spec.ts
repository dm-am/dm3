/**
 * @vitest-environment jsdom
 */

/**
 * Removing an account from the "Модерация" tab: the button opens the form
 * window and sends nothing by itself, and a removal leads off the profile.
 */
import { describe, it, expect, vi, beforeEach } from "vitest";
import { mount } from "@vue/test-utils";
import ModerationAccountRemoval from "./ModerationAccountRemoval.vue";
import AccountRemovalDialog from "./AccountRemovalDialog.vue";
import { useToast } from "@/shared/lib/composables/useToast";

const removeAccount = vi.hoisted(() => vi.fn());

vi.mock("@/entities/moderation", () => ({
  moderationApi: { removeAccount },
}));

const { push } = vi.hoisted(() => ({ push: vi.fn() }));

// Only the navigation the removal ends with.
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

const mountIt = (targetUsername = "reader") =>
  mount(ModerationAccountRemoval, { props: { targetUsername } });

const button = (wrapper: ReturnType<typeof mountIt>) => wrapper.get("button");

const removalWindow = () => {
  const found = modal.registered.at(-1);
  if (!found) throw new Error("the removal window was never registered");
  return found;
};

const toast = useToast();
const saidOutLoud = () => toast.toasts.value.map((t) => t.message);

describe("removing an account from the moderated profile", () => {
  beforeEach(() => {
    push.mockClear();
    modal.open.mockClear();
    modal.close.mockClear();
    modal.registered.length = 0;
    removeAccount.mockReset();
    [...toast.toasts.value].forEach((t) => toast.dismiss(t.id));
  });

  /** The reason is asked in the window; the press on the page sends nothing. */
  it("opens the window for the account and sends nothing by itself", async () => {
    const wrapper = mountIt("reader");

    expect(wrapper.find("textarea").exists()).toBe(false);
    await button(wrapper).trigger("click");

    expect(removalWindow().component).toBe(AccountRemovalDialog);
    expect(removalWindow().attrs.targetUsername).toBe("reader");
    expect(modal.open).toHaveBeenCalledTimes(1);
    expect(removeAccount).not.toHaveBeenCalled();
  });

  /**
   * The profile answers 404 after a removal, so the moderator lands on
   * "Удаленные пользователи" narrowed to the name, with no toast.
   */
  it("takes the moderator to the removed account the window reports", () => {
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
    mountIt("reader");

    (removalWindow().attrs.onCancel as () => void)();

    expect(modal.close).toHaveBeenCalled();
    expect(push).not.toHaveBeenCalled();
  });
});
