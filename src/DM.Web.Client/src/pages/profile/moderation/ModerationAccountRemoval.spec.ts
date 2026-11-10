/**
 * @vitest-environment jsdom
 */

/**
 * Removing an account from the "Модерация" tab: the button opens the form
 * window and sends nothing by itself, and a removal leads off the profile.
 */
import { describe, it, expect, vi, beforeEach } from "vitest";
import { flushPromises } from "@vue/test-utils";
import { mountPage } from "@/shared/lib/testing/mountPage";
import ModerationAccountRemoval from "./ModerationAccountRemoval.vue";
import AccountRemovalDialog from "./AccountRemovalDialog.vue";
import { useToast } from "@/shared/lib/composables/useToast";

const removeAccount = vi.hoisted(() => vi.fn());

vi.mock("@/entities/moderation", () => ({
  moderationApi: { removeAccount },
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

// The container and the plugin stay the library's own: mountPage draws them.
vi.mock("vue-final-modal", async (importOriginal) => ({
  ...(await importOriginal<typeof import("vue-final-modal")>()),
  useModal: (options: ModalRegistration) => {
    modal.registered.push(options);
    return { open: modal.open, close: modal.close };
  },
}));

const PROFILE = "/users/reader";

const mountIt = async (targetUsername = "reader") => {
  const { wrapper, router } = await mountPage(
    ModerationAccountRemoval,
    PROFILE,
    {
      routes: [
        ["profile", "/users/:username"],
        ["moderation-removed-accounts", "/moderation/removed-accounts"],
      ],
      props: { targetUsername },
    },
  );
  return { wrapper, address: () => router.currentRoute.value.fullPath };
};

const button = (wrapper: Awaited<ReturnType<typeof mountIt>>["wrapper"]) =>
  wrapper.get("button");

const removalWindow = () => {
  const found = modal.registered.at(-1);
  if (!found) throw new Error("the removal window was never registered");
  return found;
};

const toast = useToast();
const saidOutLoud = () => toast.toasts.value.map((t) => t.message);

describe("removing an account from the moderated profile", () => {
  beforeEach(() => {
    modal.open.mockClear();
    modal.close.mockClear();
    modal.registered.length = 0;
    removeAccount.mockReset();
    [...toast.toasts.value].forEach((t) => toast.dismiss(t.id));
  });

  /** The reason is asked in the window; the press on the page sends nothing. */
  it("opens the window for the account and sends nothing by itself", async () => {
    const { wrapper } = await mountIt("reader");

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
  it("takes the moderator to the removed account the window reports", async () => {
    const { wrapper, address } = await mountIt("reader");

    (removalWindow().attrs.onSuccess as () => void)();
    await flushPromises();

    expect(modal.close).toHaveBeenCalled();
    expect(
      wrapper.findComponent(ModerationAccountRemoval).emitted("updated"),
    ).toHaveLength(1);
    expect(address()).toBe("/moderation/removed-accounts?search=reader");
    expect(saidOutLoud()).toEqual([]);
  });

  it("stays where it is when the window is left", async () => {
    const { address } = await mountIt("reader");

    (removalWindow().attrs.onCancel as () => void)();
    await flushPromises();

    expect(modal.close).toHaveBeenCalled();
    expect(address()).toBe(PROFILE);
  });
});
