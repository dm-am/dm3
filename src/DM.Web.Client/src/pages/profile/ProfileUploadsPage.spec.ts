/**
 * @vitest-environment jsdom
 */

/**
 * Who has the page of somebody's uploaded files: the owner of the files and
 * the administrators. Anybody else, a guest included, gets the site's own "not
 * found" at its address, with no refusal drawn and no files asked for, and the
 * decision waits for the answer about the session the start of the page asks
 * for: a session the browser kept no copy of is named by that answer alone.
 */
import { beforeEach, describe, expect, it, vi } from "vitest";
import { computed, ref } from "vue";
import { flushPromises, mount } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { UserRole, type User } from "@/shared/api/models/common";
import { useAuthStore } from "@/shared/stores/auth";
import ProfileUploadsPage from "./ProfileUploadsPage.vue";

const { replace, getUploads, session } = vi.hoisted(() => ({
  replace: vi.fn(),
  getUploads: vi.fn(),
  session: { settled: Promise.resolve() as Promise<void> },
}));

const OWNER = "the-owner";

vi.mock("vue-router", () => ({
  useRoute: () => ({
    params: { username: OWNER },
    path: `/users/${OWNER}/uploads`,
    query: {},
    hash: "",
  }),
  useRouter: () => ({ replace }),
}));

vi.mock("@/entities/user", () => ({
  restoreSession: () => session.settled,
}));

// The owner of the page is resolved by the subpage helper; here he exists.
vi.mock("./useProfileSubpage", () => ({
  useProfileSubpage: () => ({
    username: computed(() => OWNER),
    canonicalUsername: ref(OWNER),
    notFound: ref(false),
    profileLink: computed(() => ({
      name: "profile",
      params: { username: OWNER },
    })),
  }),
}));

vi.mock("@/shared/api/uploadApi", () => ({
  default: { getUploads },
}));

const NOT_FOUND = {
  name: "not-found",
  params: { pathMatch: ["users", OWNER, "uploads"] },
  query: {},
  hash: "",
};

function render(viewer: { username: string; role: UserRole } | null) {
  setActivePinia(createPinia());
  useAuthStore().user = viewer as User | null;
  return mount(ProfileUploadsPage, {
    shallow: true,
    global: { stubs: { RouterLink: true } },
  });
}

describe("ProfileUploadsPage — who has the page", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    session.settled = Promise.resolve();
    getUploads.mockResolvedValue({
      data: { resources: [], paging: null },
      error: null,
    });
  });

  it("opens the site's not found for a guest, with nothing drawn", async () => {
    const wrapper = render(null);
    await flushPromises();

    expect(replace).toHaveBeenCalledWith(NOT_FOUND);
    expect(wrapper.find(".profile-uploads-page").exists()).toBe(false);
    expect(getUploads).not.toHaveBeenCalled();
  });

  it("opens it for another player", async () => {
    const wrapper = render({
      username: "somebody",
      role: UserRole.RegularUser,
    });
    await flushPromises();

    expect(replace).toHaveBeenCalledWith(NOT_FOUND);
    expect(wrapper.find(".profile-uploads-page").exists()).toBe(false);
  });

  it("keeps the owner of the files on his page", async () => {
    const wrapper = render({ username: OWNER, role: UserRole.RegularUser });
    await flushPromises();

    expect(replace).not.toHaveBeenCalled();
    expect(wrapper.find(".profile-uploads-page").exists()).toBe(true);
    expect(getUploads).toHaveBeenCalled();
  });

  it("keeps an administrator on it", async () => {
    render({ username: "an-admin", role: UserRole.Admin });
    await flushPromises();

    expect(replace).not.toHaveBeenCalled();
  });

  it("waits for the answer about the session before sending anybody away", async () => {
    let answer = () => {};
    session.settled = new Promise((resolve) => (answer = resolve));
    const wrapper = render(null);
    await flushPromises();

    expect(replace).not.toHaveBeenCalled();

    // The server names the owner, and the page opens for him.
    useAuthStore().user = { username: OWNER } as User;
    answer();
    await flushPromises();
    expect(replace).not.toHaveBeenCalled();
    expect(wrapper.find(".profile-uploads-page").exists()).toBe(true);
  });
});
