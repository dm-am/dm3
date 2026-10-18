/**
 * @vitest-environment jsdom
 */

/**
 * The door of the moderation zone.
 *
 * A viewer below a section's role used to get the section's address opened and
 * a line "Страница доступна только ..." where its content would be, under a
 * heading of a page that was not his. What a reader may not open is not drawn:
 * the shell answers such an address with the 403 page of the site, whole, and
 * mounts the section only for those its row of the table admits.
 *
 * The routes here are the zone's own names over stand-in pages, so the role
 * each one asks for is the table's and not a copy of it.
 */
import { describe, it, expect, afterEach, beforeEach } from "vitest";
import { mount, flushPromises, enableAutoUnmount } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { createRouter, createMemoryHistory } from "vue-router";
import { defineComponent, h } from "vue";
import ModerationPage from "./ModerationPage.vue";
import { UserRole } from "@/shared/api/models/common/user";
import { useAuthStore } from "@/shared/stores";
import { formatDocumentTitle } from "@/shared/lib/composables/useDocumentTitle";

enableAutoUnmount(afterEach);

const Section = defineComponent({
  setup: () => () => h("div", { class: "section" }),
});

/** A page titled by its data, as one ticket is. */
const Ticket = defineComponent({
  setup() {
    document.title = formatDocumentTitle("Спам в чате | Обращение");
    return () => h("div", { class: "section" });
  },
});

function zoneRouter() {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      {
        path: "/moderation",
        component: ModerationPage,
        children: [
          {
            name: "moderation-bans",
            path: "bans",
            meta: { title: "Последние баны" },
            component: Section,
          },
          {
            name: "moderation-tags",
            path: "tags",
            meta: { title: "Игровые теги" },
            component: Section,
          },
          {
            name: "moderation-awards",
            path: "awards",
            meta: { title: "Награды" },
            component: Section,
          },
          {
            name: "moderation-awards-series",
            path: "awards/series/:id",
            meta: { title: "Серия конкурсов" },
            component: Section,
          },
          {
            name: "moderation-fundraising",
            path: "fundraising",
            meta: { title: "Сбор средств" },
            component: Section,
          },
          {
            name: "moderation-ticket",
            path: "tickets/:ticketId",
            meta: { dynamicTitle: true },
            component: Ticket,
          },
        ],
      },
    ],
  });
  // What the app's router does after every navigation (router.ts).
  router.afterEach((to, from) => {
    if (to.name !== from.name) {
      document.title = formatDocumentTitle(to.meta.title);
    }
  });
  return router;
}

async function open(role: UserRole | null, path: string) {
  useAuthStore().user = role
    ? ({ id: "u-1", username: "Viewer", role } as never)
    : null;
  const router = zoneRouter();
  await router.push(path);
  await router.isReady();
  const wrapper = mount(ModerationPage, {
    global: { plugins: [router], stubs: { ErrorPage: true } },
  });
  await flushPromises();
  return { wrapper, router };
}

const ERROR_TITLE = formatDocumentTitle("Доступ запрещен");

beforeEach(() => {
  localStorage.clear();
  setActivePinia(createPinia());
  document.title = "";
});

describe("the door of the moderation zone", () => {
  const CASES: Array<[string, UserRole | null, string, boolean]> = [
    ["a player", UserRole.RegularUser, "/moderation/bans", false],
    ["a mentor", UserRole.Mentor, "/moderation/bans", false],
    ["a moderator", UserRole.Moderator, "/moderation/bans", true],
    ["a moderator", UserRole.Moderator, "/moderation/tags", false],
    ["a senior moderator", UserRole.SeniorModerator, "/moderation/tags", true],
    ["a moderator", UserRole.Moderator, "/moderation/awards/series/7", false],
    [
      "a senior moderator",
      UserRole.SeniorModerator,
      "/moderation/awards/series/7",
      true,
    ],
    [
      "a senior moderator",
      UserRole.SeniorModerator,
      "/moderation/fundraising",
      false,
    ],
    ["an administrator", UserRole.Admin, "/moderation/fundraising", true],
    ["a moderator", UserRole.Moderator, "/moderation/tickets/42", true],
    ["a player", UserRole.RegularUser, "/moderation/tickets/42", false],
  ];

  it.each(CASES)("lets %s through to %s: %s", async (_, role, path, opens) => {
    const { wrapper } = await open(role, path);

    expect(wrapper.find(".section").exists(), "the section").toBe(opens);
    const error = wrapper.findComponent({ name: "ErrorPage" });
    expect(error.exists(), "the 403 page").toBe(!opens);
    if (!opens) expect(error.props("code")).toBe(403);
  });

  it("names the tab after the error while the 403 page stands", async () => {
    await open(UserRole.Moderator, "/moderation/tags");

    expect(document.title).toBe(ERROR_TITLE);
  });

  it("keeps the error in the tab on a step to another closed section", async () => {
    // The router writes the new section's own title on the way in; the shell
    // stays mounted and the 403 page stays, so the tab must not say "Награды".
    const { router } = await open(UserRole.Moderator, "/moderation/tags");
    await router.push("/moderation/awards");
    await flushPromises();

    expect(document.title).toBe(ERROR_TITLE);
  });

  it("gives the tab back to the section that opens", async () => {
    const { router } = await open(UserRole.Moderator, "/moderation/tags");
    await router.push("/moderation/bans");
    await flushPromises();

    expect(document.title).toBe(formatDocumentTitle("Последние баны"));
  });

  it("leaves the title a page composed from its data alone", async () => {
    const { router } = await open(UserRole.Moderator, "/moderation/tickets/42");
    const composed = formatDocumentTitle("Спам в чате | Обращение");
    expect(document.title).toBe(composed);

    await router.push({ path: "/moderation/tickets/42", query: { at: "2" } });
    await flushPromises();

    expect(document.title).toBe(composed);
  });
});
