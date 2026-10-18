/**
 * @vitest-environment jsdom
 */

/**
 * The header at every width of the shell (useShellMode).
 *
 * On a desktop the menu stands between the logo and the statistics at every
 * width, from the left, and wraps there where it does not fit one line
 * (menuSteps.spec.ts). Up to the shell breakpoint, a phone included (its page
 * is laid out as wide as the narrowest tablet's, index.html), the header has
 * the logo, the greeting and one button at the left edge: the menu and the
 * statistics lead the two views of the one sliding column (App.vue), so
 * neither is drawn here, and each is on screen exactly once at any width. The
 * button's sign is the sign of the compact layout of the site settings with
 * one bar fewer (owner, 13 October 2026: one sign, made from that one, with
 * three bars).
 *
 * The greeting is the whole name at every width: a name too long for its line
 * wraps (headerLook.spec.ts, e2e common/phone-header.spec.ts), it is never cut.
 *
 * Nothing in jsdom lays a line out, so where a test needs the menu too wide
 * for one line the widths are put in place: the links as wide as a face of
 * the given size draws them, the two side columns at their 330.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { mount, RouterLinkStub, type VueWrapper } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { nextTick } from "vue";
import { useAuthStore } from "@/entities/user";
import { useUiStore } from "@/shared/stores/ui";
import { icons } from "@/shared/lib/utils/icons";
import { UserRole, type User } from "@/shared/api/models/common/user";

/** The width the header is told it has, as useShellMode would read it. */
const width = vi.hoisted(() => ({
  mode: null as unknown as { value: "desktop" | "tablet" },
}));

vi.mock("@/shared/lib/composables/useShellMode", async () => {
  const { ref, computed } = await import("vue");
  const mode = ref<"desktop" | "tablet">("desktop");
  width.mode = mode;
  return {
    useShellMode: () => ({
      narrow: computed(() => mode.value !== "desktop"),
    }),
  };
});

// The counts and the sign-out have no part in where the header puts things.
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

const { default: Header } = await import("./Header.vue");

const stubs = {
  RouterLink: RouterLinkStub,
  GuestActions: { template: "<div class='guest-stub' />" },
  SiteStatistics: { template: "<div class='stats-stub' />" },
  // The words a tooltip would show, kept where a test can read them.
  Tooltip: {
    props: ["text"],
    template: "<span class='tip' :data-tip='text'><slot /></span>",
  },
};

/** The reader signed in, as much of him as the header reads. */
const reader = (role: UserRole) =>
  ({ id: "u-1", username: "TestUser", role }) as unknown as User;

let wrapper: VueWrapper | null = null;

async function mountAs(
  mode: "desktop" | "tablet",
  options: { role?: UserRole | null } = {},
) {
  width.mode.value = mode;
  const role = options.role === undefined ? UserRole.Moderator : options.role;
  if (role) useAuthStore().updateUser(reader(role));
  wrapper = mount(Header, { global: { stubs } });
  await nextTick();
  return wrapper;
}

const button = (w: VueWrapper) => w.find("[data-testid='menu-button']");

/** The bars of a sign drawn with rects, as "x y width height" each. */
const bars = (markup: string) =>
  [...markup.matchAll(/<rect ([^>]*?)\/?>/g)].map((rect) =>
    ["x", "y", "width", "height"]
      .map((name) => new RegExp(`${name}="([^"]+)"`).exec(rect[1])?.[1])
      .join(" "),
  );

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

  describe("the button of the sliding column", () => {
    it("is not on a desktop, where the columns stand", async () => {
      const w = await mountAs("desktop");

      expect(button(w).exists()).toBe(false);
    });

    it("is one, at the left edge, and says whether the column is out", async () => {
      const w = await mountAs("tablet");
      const one = button(w);

      expect(w.findAll("button[aria-haspopup='dialog']")).toHaveLength(1);
      expect(one.attributes("aria-label")).toBe("Меню");
      expect(one.element.closest(".tip")?.getAttribute("data-tip")).toBe(
        "Меню",
      );
      expect(one.attributes("aria-haspopup")).toBe("dialog");
      expect(one.attributes("aria-expanded")).toBe("false");
      // Before the logo: the first thing of the header.
      expect(w.element.firstElementChild?.contains(one.element)).toBe(true);

      await one.trigger("click");
      // The column opens on its left view.
      expect(useUiStore().openedPanel).toBe("left");
      expect(one.attributes("aria-expanded")).toBe("true");

      // Its right view is the same column, still out.
      useUiStore().openPanel("right");
      await nextTick();
      expect(one.attributes("aria-expanded")).toBe("true");

      useUiStore().closePanel();
      await nextTick();
      expect(one.attributes("aria-expanded")).toBe("false");
    });

    it("carries the compact layout's sign with one bar fewer", async () => {
      const w = await mountAs("tablet");
      const sign = button(w).get("svg");

      expect(sign.attributes("aria-hidden")).toBe("true");
      expect(sign.attributes("viewBox")).toBe(icons.layoutCompact.viewBox);
      expect(icons.menu.viewBox).toBe(icons.layoutCompact.viewBox);
      // Three bars of the compact sign's own width and weight, the first and
      // the last where it has them and the middle one halfway between.
      const drawn = bars(sign.html());
      expect(drawn).toEqual(["0 0 16 2", "0 5 16 2", "0 10 16 2"]);
      const compact = bars(icons.layoutCompact.path);
      expect(compact).toHaveLength(4);
      expect(drawn[0]).toBe(compact[0]);
      expect(drawn[2]).toBe(compact[3]);
      // Nothing of the two signs it replaces: the bars of CSS and the ring.
      expect(w.find(".bar").exists()).toBe(false);
      expect(w.find(".info-sign").exists()).toBe(false);
    });
  });

  describe("the menu and the statistics", () => {
    it.each([
      ["desktop", 1],
      ["tablet", 0],
    ] as const)("are drawn on a %s %i time(s)", async (mode, times) => {
      const w = await mountAs(mode);

      // Up to the shell breakpoint both lead the sliding column instead
      // (App.vue).
      expect(w.findAll(".stats-stub")).toHaveLength(times);
      expect(w.findAll("nav.top-menu")).toHaveLength(times);
      expect(w.classes().includes("header--narrow")).toBe(mode === "tablet");
    });

    it("keep nine links between the logo and the statistics of 1366", async () => {
      const w = await mountAs("desktop");

      expect(w.findAll("a.link")).toHaveLength(9);
    });

    it("keep the menu between them when a ninth link comes", async () => {
      const w = await mountAs("desktop", { role: UserRole.RegularUser });
      expect(w.findAll("a.link")).toHaveLength(8);

      useAuthStore().updateUser(reader(UserRole.Moderator));
      await nextTick();
      await nextTick();

      expect(w.findAll("a.link")).toHaveLength(9);
      expect(w.get("nav.top-menu").findAll("a.link")).toHaveLength(9);
    });
  });

  describe("the greeting", () => {
    /** Every mark of a one-line row that cuts its parts (FitRow). */
    const FIT = "[data-fit-row], [data-fit-keep], [data-fit-shrink]";

    it.each(["desktop", "tablet"] as const)(
      "is never a row that cuts the name on a %s: the whole name is there",
      async (mode) => {
        const w = await mountAs(mode);

        // A name too long for the line wraps between the words of the
        // greeting (headerLook.spec.ts); nothing of it goes under "…".
        expect(w.findAll(FIT)).toHaveLength(0);
        expect(w.get("[data-testid='user-menu']").text()).toBe("TestUser");
        expect(w.get(".user-actions").text()).not.toContain("…");
      },
    );

    it("leaves a guest's line as it is, no row around it", async () => {
      const w = await mountAs("tablet", { role: null });

      expect(w.find(".user-actions .guest-stub").exists()).toBe(true);
      expect(w.findAll(FIT)).toHaveLength(0);
    });
  });
});
