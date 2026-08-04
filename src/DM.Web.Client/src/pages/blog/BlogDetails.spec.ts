/**
 * @vitest-environment jsdom
 */

/**
 * The blog's info page is the game's info page with blog facts in it: the
 * same borderless key-value table, the same roster table under it, and the
 * description introduced by a dash separator instead of by a caption.
 *
 * It had drifted on the parts a reader takes in at a glance. The status cell
 * spelled the three statuses itself rather than asking the badge every other
 * blog surface asks. The people in the table carried the role badges the
 * game's table hides. The description sat under a "Описание блога" caption
 * over text that announces itself. And a "Все публикации" line under the
 * rubric table did the navigating the menu does, which is why the game page
 * has no "Все комнаты" line.
 */
import { describe, it, expect, beforeEach } from "vitest";
import { mount } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import BlogDetails from "./BlogDetails.vue";
import { useBlogDetailsStore, type Blog } from "@/entities/blog";

const stubs = {
  "router-link": {
    template: '<a class="router-link"><slot /></a>',
    props: ["to"],
  },
  Tooltip: { template: "<span><slot /></span>", props: ["text"] },
};

const blog = {
  id: "b-1",
  publicId: "aaaab",
  title: "Дневник приключенца",
  status: "Active",
  author: { id: "u-1", username: "TestSeniorMod", role: "SeniorModerator" },
  assistants: [],
  createdUtc: "2025-12-08T22:54:58Z",
  publicationCount: 6,
  subscribersCount: 0,
  description: "<p>Истории из игр глазами игрока</p>",
  rubrics: [
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

function mountDetails() {
  useBlogDetailsStore().blog = blog;
  return mount(BlogDetails, { global: { stubs } });
}

describe("BlogDetails", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
  });

  it("keeps the blog's facts in one table", () => {
    const labels = mountDetails()
      .findAll(".info-table th")
      .map((th) => th.text());
    expect(labels).toEqual([
      "Статус",
      "Автор",
      "Ассистент",
      "Дата создания",
      "Публикаций",
      "Подписчиков",
    ]);
  });

  it("spells the status through the shared badge", () => {
    expect(mountDetails().find(".info-table td").text()).toBe("Открыт");
  });

  it("keeps role badges out of the fact table", () => {
    expect(mountDetails().find(".role-badge").exists()).toBe(false);
  });

  it("opens the description with a separator, not a caption", () => {
    const wrapper = mountDetails();
    expect(wrapper.find(".description .dash-separator").exists()).toBe(true);
    expect(wrapper.text()).not.toContain("Описание блога");
  });

  it("leaves navigation to the menu", () => {
    expect(mountDetails().text()).not.toContain("Все публикации");
  });
});
