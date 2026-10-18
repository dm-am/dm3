/**
 * @vitest-environment jsdom
 */

/**
 * The menu of moderation is the only way between its sections now: the tab
 * strip over the content is gone, and with it the second list that offered
 * the templates of schemas twice and knew nothing of the rest. What each role
 * sees is written out below group by group and row by row rather than derived
 * from the table the panel reads, so a row moved to another group or opened
 * to another role fails here instead of passing along with the table.
 *
 * The rows stand in four groups in the order the owner set: what waits for
 * the moderator's decision (the tickets among them, one queue "Обращения"),
 * the people, the journals of what was done, and the settings of the site;
 * the administrator's fundraising stands apart after them. No heading stands
 * over a group: an empty line parts two groups, as on DM2 the groups of the
 * menu of a game are parted.
 *
 * A link to a page that answers the reader with the 403 page is worse than no
 * link: it reads as a section that broke rather than as one that was never his
 * to open. And a group the reader has no row in leaves no empty line behind.
 *
 * The open section is bold and named the page; a page inside a section (a
 * series of contests, a template, a ticket) marks nothing, its heading says
 * where the reader is.
 */
import { describe, it, expect, beforeEach } from "vitest";
import { mount, flushPromises } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { defineComponent } from "vue";
import { createMemoryHistory, createRouter } from "vue-router";
import { useAuthStore, UserRole, type User } from "@/entities/user";
import { MODERATION_SECTIONS } from "@/shared/config/moderationSections";
import { componentCss, styleRules } from "@/shared/lib/testing/styles";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import ModerationPanel from "./ModerationPanel.vue";

const HERE = dirname(fileURLToPath(import.meta.url));

const Empty = defineComponent({ render: () => null });

const LOGS = [
  "Последние оцененные посты",
  "Последние предупреждения",
  "Последние баны",
  "Все загруженные файлы",
];

/** The menu of a moderator: three groups, the settings of the site not his. */
const MODERATOR_MENU = [
  ["Премодерируемые игры", "Премодерируемые блоги", "Обращения"],
  ["Новые пользователи", "Нарушители", "Модераторы"],
  LOGS,
];

const SENIOR_MENU = [
  [
    "Премодерируемые игры",
    "Премодерируемые блоги",
    "Запросы на смену имени",
    "Обращения",
  ],
  ["Новые пользователи", "Удаленные пользователи", "Нарушители", "Модераторы"],
  LOGS,
  ["Игровые теги", "Шаблоны схем атрибутов", "Достижения", "Награды"],
];

/** The senior moderator's four groups, and the fundraising apart. */
const ADMIN_MENU = [...SENIOR_MENU, ["Сбор средств"]];

const ROUTES = [
  ...MODERATION_SECTIONS.map((section) => ({
    path: `/moderation/${section.name}`,
    name: section.name,
    component: Empty,
  })),
  {
    path: "/moderation/awards/series/:id",
    name: "moderation-awards-series",
    component: Empty,
  },
  {
    path: "/moderation/schema-templates/:id",
    name: "moderation-schema-template",
    component: Empty,
  },
  {
    path: "/moderation/tickets/:ticketId",
    name: "moderation-ticket",
    component: Empty,
  },
];

async function render(
  role: UserRole,
  location = "/moderation/moderation-bans",
) {
  useAuthStore().user = { username: "Модератор", role } as User;
  const router = createRouter({
    history: createMemoryHistory(),
    routes: ROUTES,
  });
  await router.push(location);
  await router.isReady();
  const wrapper = mount(ModerationPanel, {
    global: {
      plugins: [router],
      stubs: { SidebarBlock: { template: "<ul><slot /></ul>" } },
    },
  });
  await flushPromises();
  return wrapper;
}

/**
 * The menu as it reads: its rows, a new group begun by every row the empty
 * line stands over. Every item of the menu is a row with its link: there is
 * no heading among them.
 */
function menu(wrapper: Awaited<ReturnType<typeof render>>) {
  const groups: string[][] = [];
  for (const item of wrapper.findAll("ul > li")) {
    const link = item.find("a");
    expect(link.exists(), item.text()).toBe(true);
    if (!groups.length || item.classes("group-start")) groups.push([]);
    groups[groups.length - 1].push(link.text());
  }
  return groups;
}

/** Every element of the menu that says where the reader is. */
function marks(wrapper: Awaited<ReturnType<typeof render>>) {
  return wrapper
    .findAll("[aria-current]")
    .map((el) => `${el.text()}=${el.attributes("aria-current")}`);
}

describe("the moderation menu", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
  });

  it("gives a moderator three groups and no settings of the site", async () => {
    expect(menu(await render(UserRole.Moderator))).toEqual(MODERATOR_MENU);
  });

  it("gives a senior moderator four groups of four", async () => {
    expect(menu(await render(UserRole.SeniorModerator))).toEqual(SENIOR_MENU);
  });

  it("gives an administrator the four groups and the fundraising apart", async () => {
    expect(menu(await render(UserRole.Admin))).toEqual(ADMIN_MENU);
  });

  it("heads no group, and leaves no line for a group the reader has no row in", async () => {
    const wrapper = await render(UserRole.Moderator);

    expect(wrapper.find(".sidebar-section-title").exists()).toBe(false);
    expect(wrapper.findAll(".group-start")).toHaveLength(2);
  });

  // DM2 parts the groups of the menu of a game by an empty line: one line of
  // the menu's height over the first row of a group.
  it("parts two groups by one empty line", () => {
    const rules = styleRules(
      componentCss(resolve(HERE, "ModerationPanel.vue")),
    );

    expect(
      rules
        .filter((rule) => /\.group-start/.test(rule.selector))
        .map((rule) => rule.declarations),
    ).toEqual([{ "margin-top": "1lh" }]);
  });

  it("names the tickets one queue, and the templates of schemas once", async () => {
    const wrapper = await render(UserRole.Admin);
    const words = wrapper.findAll("a").map((a) => a.text());

    expect(words.filter((word) => word === "Обращения")).toHaveLength(1);
    expect(words).not.toContain("Жалобы");
    expect(words).not.toContain("Поддержка");
    expect(
      words.filter((word) => word === "Шаблоны схем атрибутов"),
    ).toHaveLength(1);
  });

  it("leads each row to its section's page", async () => {
    const wrapper = await render(UserRole.Admin);
    const tags = wrapper.findAll("a").find((a) => a.text() === "Игровые теги");
    const tickets = wrapper.findAll("a").find((a) => a.text() === "Обращения");

    expect(tags?.attributes("href")).toBe("/moderation/moderation-tags");
    expect(tickets?.attributes("href")).toBe("/moderation/moderation-tickets");
  });

  it("marks the open section bold as the page", async () => {
    const wrapper = await render(UserRole.Admin, "/moderation/moderation-tags");

    expect(marks(wrapper)).toEqual(["Игровые теги=page"]);
    expect(wrapper.findAll(".current").map((a) => a.text())).toEqual([
      "Игровые теги",
    ]);
  });

  it("marks nothing on a page inside a section", async () => {
    for (const location of [
      "/moderation/awards/series/1",
      "/moderation/schema-templates/1",
      "/moderation/tickets/1",
    ]) {
      const wrapper = await render(UserRole.Admin, location);

      expect(marks(wrapper), location).toEqual([]);
      expect(wrapper.findAll(".current"), location).toHaveLength(0);
    }
  });
});
