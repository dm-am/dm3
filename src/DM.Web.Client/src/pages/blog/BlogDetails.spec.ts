/**
 * @vitest-environment jsdom
 */

// The info page is the game's: the borderless fact table, the roster under it,
// the description after a dash separator, the status through the shared badge.
import { describe, it, expect, beforeEach } from "vitest";
import { mount } from "@vue/test-utils";
import { UserRole } from "@/shared/api/models/common";
import { createPinia, setActivePinia } from "pinia";
import BlogDetails from "./BlogDetails.vue";
import { useBlogDetailsStore, type Blog } from "@/entities/blog";
import { createMockBlog } from "@/entities/blog/model/store.fixtures";
import { createMockUser } from "@/entities/forum/model/store.fixtures";

const stubs = {
  "router-link": {
    template: '<a class="router-link"><slot /></a>',
    props: ["to"],
  },
  Tooltip: { template: "<span><slot /></span>", props: ["text"] },
};

const blog: Blog = {
  ...createMockBlog("b-1", "Дневник приключенца"),
  publicId: "aaaab",
  author: {
    ...createMockUser("u-1", "TestSeniorMod"),
    role: UserRole.SeniorModerator,
  },
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
};

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

  it("opens the description with a separator", () => {
    const wrapper = mountDetails();
    expect(wrapper.find(".description .dash-separator").exists()).toBe(true);
  });
});
