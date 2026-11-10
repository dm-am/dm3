/**
 * @vitest-environment jsdom
 */

/**
 * Who has the page of somebody's uploads: the owner and the administrators.
 * Anybody else gets the site's "not found", decided after the answer about the session.
 */
import { beforeEach, describe, expect, it, vi } from "vitest";
import { computed, ref } from "vue";
import { flushPromises } from "@vue/test-utils";
import { createPinia } from "pinia";
import { mountPage } from "@/shared/lib/testing/mountPage";
import { UserRole, type User } from "@/shared/api/models/common";
import { useAuthStore } from "@/shared/stores/auth";
import ProfileUploadsPage from "./ProfileUploadsPage.vue";

const { getUploads, session } = vi.hoisted(() => ({
  getUploads: vi.fn(),
  session: { settled: Promise.resolve() as Promise<void> },
}));

const OWNER = "the-owner";

vi.mock("@/entities/user", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/entities/user")>()),
  restoreSession: () => session.settled,
}));

// The owner of the page exists.
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

const UPLOADS = `/users/${OWNER}/uploads`;

async function render(viewer: { username: string; role: UserRole } | null) {
  const pinia = createPinia();
  useAuthStore(pinia).user = viewer as User | null;
  const { wrapper, router } = await mountPage(ProfileUploadsPage, UPLOADS, {
    routes: [
      ["profile", "/users/:username"],
      ["profile-uploads", "/users/:username/uploads"],
      ["not-found", "/:pathMatch(.*)*"],
    ],
    pinia,
    stubs: {
      ProfileSubpageHeader: true,
      ErrorState: true,
      UploadsTable: true,
      Paging: true,
      ConfirmDialog: true,
      ErrorPage: true,
    },
  });
  return { wrapper, route: () => router.currentRoute.value };
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
    const { wrapper, route } = await render(null);
    await flushPromises();

    expect(route().name).toBe("not-found");
    expect(route().params.pathMatch).toEqual(["users", OWNER, "uploads"]);
    expect(wrapper.find(".profile-uploads-page").exists()).toBe(false);
    expect(getUploads).not.toHaveBeenCalled();
  });

  it("opens it for another player", async () => {
    const { wrapper, route } = await render({
      username: "somebody",
      role: UserRole.RegularUser,
    });
    await flushPromises();

    expect(route().name).toBe("not-found");
    expect(wrapper.find(".profile-uploads-page").exists()).toBe(false);
  });

  it("keeps the owner of the files on his page", async () => {
    const { wrapper, route } = await render({
      username: OWNER,
      role: UserRole.RegularUser,
    });
    await flushPromises();

    expect(route().name).toBe("profile-uploads");
    expect(wrapper.find(".profile-uploads-page").exists()).toBe(true);
    expect(getUploads).toHaveBeenCalled();
  });

  it("pages as many files as the owner asked for lists", async () => {
    await render({
      username: OWNER,
      role: UserRole.RegularUser,
      settings: { pageSize: 50 },
    } as never);
    await flushPromises();

    expect(getUploads).toHaveBeenCalledWith(
      expect.objectContaining({ size: 50 }),
    );
  });

  it("keeps an administrator on it", async () => {
    const { route } = await render({
      username: "an-admin",
      role: UserRole.Admin,
    });
    await flushPromises();

    expect(route().name).toBe("profile-uploads");
  });

  it("waits for the answer about the session before sending anybody away", async () => {
    let answer = () => {};
    session.settled = new Promise((resolve) => (answer = resolve));
    const { wrapper, route } = await render(null);
    await flushPromises();

    expect(route().name).toBe("profile-uploads");

    // The server names the owner, and the page opens for him.
    useAuthStore().user = { username: OWNER } as User;
    answer();
    await flushPromises();
    expect(route().name).toBe("profile-uploads");
    expect(wrapper.find(".profile-uploads-page").exists()).toBe(true);
  });
});
