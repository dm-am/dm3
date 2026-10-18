/**
 * @vitest-environment jsdom
 */

/**
 * The header of a page whose browser holds nobody, while the session cookie
 * may still hold somebody.
 *
 * The page used to read the stored copy alone: empty, it drew "Вход |
 * Регистрация" over a live session, and the reader had to sign in again to a
 * site that had never signed him out. The start of the page now asks the
 * server once (restoreSession), and the header is read here the way the reader
 * reads it: the real header over the real store, with only the answer of the
 * server put in place.
 */
import { describe, it, expect, vi, beforeEach } from "vitest";
import { flushPromises, mount, RouterLinkStub } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";

/** "Кто я": the one request the page makes about it. */
const whoAmI = vi.hoisted(() => vi.fn());
vi.mock("@/entities/user/api/personalApi", () => ({
  default: { getMyProfile: whoAmI },
}));

// The counters and the sign-out the header carries have no part in who it
// greets; their stores and calls are left out so nothing else is asked.
vi.mock("@/entities/message", async () => {
  const { ref } = await import("vue");
  return { useMessagingStore: () => ({ totalUnreadCount: ref(0) }) };
});
vi.mock("@/entities/notification", async () => {
  const { ref } = await import("vue");
  return { useNotificationStore: () => ({ unreadCount: ref(0) }) };
});
vi.mock("@/features/auth", () => ({
  useSessionExit: () => ({ signOut: vi.fn(), signOutAll: vi.fn() }),
}));

import { restoreSession } from "@/entities/user";
import { useToast } from "@/shared/lib/composables/useToast";
import { Header } from "@/widgets/header";

const viewer = { id: "user-1", username: "SolohinLex" };

/** The page starting: the header drawn, the question asked. */
async function startThePage() {
  const header = mount(Header, {
    global: {
      stubs: {
        RouterLink: RouterLinkStub,
        GuestActions: { template: "<div data-testid='guest-actions' />" },
        SiteStatistics: true,
      },
    },
  });
  await restoreSession();
  await flushPromises();
  return header;
}

describe("the header of a page the browser remembers nobody on", () => {
  beforeEach(() => {
    localStorage.clear();
    setActivePinia(createPinia());
    whoAmI.mockReset();
    const { toasts, dismiss } = useToast();
    [...toasts.value].forEach((t) => dismiss(t.id));
  });

  it("greets the reader the session cookie still belongs to", async () => {
    whoAmI.mockResolvedValue({ data: viewer, error: null });

    const header = await startThePage();

    expect(header.find("[data-testid='user-menu']").text()).toBe("SolohinLex");
    expect(header.find("[data-testid='guest-actions']").exists()).toBe(false);
    expect(whoAmI).toHaveBeenCalledTimes(1);
  });

  it("stays a guest's header, without a word, when the server says nobody", async () => {
    whoAmI.mockResolvedValue({
      data: null,
      error: { type: "", title: "", status: 401, traceId: "" },
    });

    const header = await startThePage();

    expect(header.find("[data-testid='guest-actions']").exists()).toBe(true);
    expect(header.find("[data-testid='user-menu']").exists()).toBe(false);
    expect(useToast().toasts.value).toEqual([]);
    expect(whoAmI).toHaveBeenCalledTimes(1);
  });
});
