/**
 * @vitest-environment jsdom
 */

/** The header: the logo and the greeting, the menu and the statistics. */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { mount, RouterLinkStub, type VueWrapper } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { nextTick } from "vue";
import { useAuthStore } from "@/entities/user";
import { UserRole, type User } from "@/shared/api/models/common/user";
import { createMockUser } from "@/entities/forum/model/store.fixtures";
import Header from "./Header.vue";

const stubs = {
  RouterLink: RouterLinkStub,
  GuestActions: { template: "<div class='guest-stub' />" },
  SiteStatistics: { template: "<div class='stats-stub' />" },
};

/** The reader signed in, as much of him as the header reads. */
const reader = (role: UserRole): User => ({
  ...createMockUser("u-1", "TestUser"),
  role,
});

let wrapper: VueWrapper | null = null;

async function mountAs(options: { role?: UserRole | null } = {}) {
  const role = options.role === undefined ? UserRole.Moderator : options.role;
  if (role) useAuthStore().updateUser(reader(role));
  wrapper = mount(Header, { global: { stubs } });
  await nextTick();
  return wrapper;
}

describe("the header", () => {
  beforeEach(() => {
    localStorage.clear();
    setActivePinia(createPinia());
  });

  afterEach(() => {
    wrapper?.unmount();
    wrapper = null;
    vi.restoreAllMocks();
  });

  describe("the menu and the statistics", () => {
    it("are drawn once each", async () => {
      const w = await mountAs();

      expect(w.findAll(".stats-stub")).toHaveLength(1);
      expect(w.findAll("nav.top-menu")).toHaveLength(1);
    });

    it("keep nine links between the logo and the statistics of 1366", async () => {
      const w = await mountAs();

      expect(w.findAll("a.link")).toHaveLength(9);
    });

    it("keep the menu between them when a ninth link comes", async () => {
      const w = await mountAs({ role: UserRole.RegularUser });
      expect(w.findAll("a.link")).toHaveLength(8);

      useAuthStore().updateUser(reader(UserRole.Moderator));
      await nextTick();
      await nextTick();

      expect(w.findAll("a.link")).toHaveLength(9);
      expect(w.get("nav.top-menu").findAll("a.link")).toHaveLength(9);
    });
  });

  describe("the greeting", () => {
    it("is never a row that cuts the name: the whole name is there", async () => {
      const w = await mountAs();

      expect(w.get("[data-testid='user-menu']").text()).toBe("TestUser");
    });

    it("leaves a guest's line as it is, no row around it", async () => {
      const w = await mountAs({ role: null });

      expect(w.find(".user-actions .guest-stub").exists()).toBe(true);
    });
  });
});
