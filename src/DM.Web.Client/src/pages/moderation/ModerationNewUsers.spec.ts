/**
 * @vitest-environment jsdom
 */

// The size of a page is the reader's own, from the account settings, and the
// default without one: it reaches the request as `take`, and `skip` counts in
// it. Who is drawn and how is not asked here.
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { enableAutoUnmount, flushPromises } from "@vue/test-utils";
import { createPinia } from "pinia";
import { useAuthStore } from "@/shared/stores";
import { DEFAULT_PAGE_SIZE } from "@/shared/lib/composables/usePaging";
import ModerationNewUsers from "./ModerationNewUsers.vue";
import { mountPage, type MountOptions } from "@/shared/lib/testing/mountPage";

const { getUsers } = vi.hoisted(() => ({ getUsers: vi.fn() }));

// The network only: the router, the store and the table are the real ones.
// The original as a plain object: its type would name a file inside another
// slice, which the layer rule refuses even as a type.
vi.mock("@/entities/user/api/userApi", async (importOriginal) => ({
  ...(await importOriginal<object>()),
  default: { getUsers },
}));

/** The page of a reader who chose no size. */
const PAGE_SIZE = DEFAULT_PAGE_SIZE;

enableAutoUnmount(afterEach);

beforeEach(() => {
  getUsers.mockReset();
  getUsers.mockResolvedValue({
    data: { resources: [], paging: null },
    error: null,
  });
});

async function rendered(
  address: string,
  options: Omit<MountOptions, "routes"> = {},
) {
  const mounted = await mountPage(ModerationNewUsers, address, {
    routes: [["moderation-new-users", "/moderation/new-users"]],
    ...options,
  });
  await flushPromises();
  return mounted.wrapper;
}

describe("the page size of the new users list", () => {
  it("asks for the page named in the address, at the size of the list", async () => {
    await rendered("/moderation/new-users?number=3");

    expect(getUsers).toHaveBeenCalledWith(
      expect.objectContaining({ skip: 2 * PAGE_SIZE, take: PAGE_SIZE }),
    );
  });

  it("asks for pages as long as the reader's lists", async () => {
    const pinia = createPinia();
    useAuthStore(pinia).user = {
      settings: { pageSize: 7 },
    } as never;

    await rendered("/moderation/new-users?number=3", { pinia });

    expect(getUsers).toHaveBeenCalledWith(
      expect.objectContaining({ skip: 14, take: 7 }),
    );
  });
});
