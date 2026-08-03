/**
 * @vitest-environment jsdom
 */

/**
 * The blog menu is built to the same plan as the game menu, and the plan is
 * the requirement: one fixed heading, the rubric group with its rubrics
 * nested under it, then the blog's pages, each with a counter and no
 * exception at zero.
 *
 * It had drifted on every one of those at once. The heading was the blog's
 * own title, and a link. "Рубрики" and "Обсуждение" were bold muted capitals
 * rather than menu rows. "Информация" and the feed were not in the menu at
 * all: the heading link stood in for one, a line under the rubric table for
 * the other. The discussion counter vanished at zero. And the panel repeated
 * the status that the info table prints two lines further down.
 */
import { describe, it, expect, beforeEach } from "vitest";
import { mount } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import BlogPanel from "./BlogPanel.vue";
import SidebarSectionTitle from "./SidebarSectionTitle.vue";
import { useBlogDetailsStore, type Blog } from "@/entities/blog";
import { useAuthStore } from "@/entities/user";

const stubs = {
  "router-link": {
    template: '<a class="router-link"><slot /></a>',
    props: ["to"],
  },
  Tooltip: { template: "<span><slot /></span>", props: ["text"] },
  ConfirmDialog: true,
};

const blog = {
  id: "b-1",
  publicId: "aaaab",
  title: "Дневник приключенца",
  status: "Active",
  author: { id: "u-1", username: "Автор" },
  assistants: [],
  unreadPublicationsCount: 6,
  unreadCommentsCount: 0,
  rubrics: [
    {
      id: "r-2",
      title: "Для избранных",
      sortOrder: 2,
      publicationCount: 0,
      unreadPublicationsCount: 0,
      unreadCommentsCount: 0,
    },
    {
      id: "r-1",
      title: "Общее",
      sortOrder: 1,
      publicationCount: 6,
      unreadPublicationsCount: 6,
      unreadCommentsCount: 3,
    },
  ],
} as unknown as Blog;

function mountPanel() {
  useBlogDetailsStore().blog = blog;
  return mount(BlogPanel, { props: { blogId: "aaaab" }, global: { stubs } });
}

function signedInAs(username: string) {
  const auth = useAuthStore();
  auth.user = { username } as unknown as NonNullable<typeof auth.user>;
}

/** What a row copies as: a non-breaking space is still a space. */
const copied = (text: string) => text.replace(/\u00a0/g, " ").trim();

/** Every menu row, in the order the menu renders them. */
function rows(wrapper: ReturnType<typeof mountPanel>) {
  return wrapper.findAll("li.link").map((li) => copied(li.text()));
}

describe("BlogPanel", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
  });

  it("heads the block with the menu's own name, not the blog's", () => {
    const wrapper = mountPanel();
    expect(wrapper.find(".sidebar-title").text()).toBe("Меню блога");
    expect(wrapper.find(".sidebar-title a").exists()).toBe(false);
    expect(wrapper.find(".toggle").attributes("aria-label")).toBe(
      'Свернуть раздел "Меню блога"',
    );
  });

  it("lists the menu in one order, counters and all", () => {
    expect(rows(mountPanel())).toEqual([
      "- Рубрики",
      "Общее (6/3)",
      "Для избранных (0/0)",
      "- Информация",
      "- Лента публикаций (6)",
      "- Обсуждение (0)",
    ]);
  });

  it("nests the rubrics under their group row", () => {
    const wrapper = mountPanel();
    const nested = wrapper.findAll("ul.rubric-list");
    expect(nested).toHaveLength(1);
    expect(nested[0].findAll("li.link")).toHaveLength(2);
  });

  it("leaves the blog's status to the info table", () => {
    expect(mountPanel().text()).not.toContain("Статус");
  });

  it("heads its sections the way the game panel does", () => {
    signedInAs("Автор");
    const headings = mountPanel().findAllComponents(SidebarSectionTitle);
    expect(headings.map((heading) => heading.text())).toEqual([
      "Управление блогом",
    ]);
  });
});
