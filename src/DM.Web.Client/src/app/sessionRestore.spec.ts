/**
 * @vitest-environment jsdom
 */

/**
 * The header over a live session the browser has no stored copy of: the real
 * header over the real store, with only the server's answer in place.
 */
import { describe, it, expect, vi, beforeEach } from "vitest";
import { flushPromises, mount, RouterLinkStub } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";

/** "Кто я": the one request the page makes about it. */
const whoAmI = vi.hoisted(() => vi.fn());
vi.mock("@/entities/user/api/personalApi", () => ({
  // The page sizes read along with it have no part in who the header greets.
  default: {
    getMyProfile: whoAmI,
    getMyPreferences: async () => ({ data: null, error: null }),
  },
}));

// The counters and the sign-out have no part in who the header greets.
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
