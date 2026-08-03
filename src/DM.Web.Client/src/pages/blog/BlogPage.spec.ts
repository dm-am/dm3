/**
 * @vitest-environment jsdom
 */

/**
 * The blog shell prints the blog's name and nothing else, the way the game
 * shell does. It used to print a status badge, a premoderation badge and a
 * meta line with the author, the assistants and the reader count, all of it
 * repeated by the info table immediately below and repeated in different
 * words: the header counted "Читатели" where the table counted "Подписчиков"
 * off the same field.
 *
 * The loading state is the header's twin. Without it the page drew nothing
 * at all until the blog landed, then pushed everything down by the height of
 * an h1.
 */
import { describe, it, expect, beforeEach, vi } from "vitest";
import { mount, flushPromises } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import BlogPage from "./BlogPage.vue";
import type { Blog } from "@/entities/blog";

const { mockGetBlog } = vi.hoisted(() => ({ mockGetBlog: vi.fn() }));

vi.mock("@/entities/blog/api/blogApi", () => ({
  default: { getBlog: mockGetBlog },
}));

// Only the route param is needed; the shell reads nothing else from routing.
vi.mock("vue-router", () => ({
  useRoute: () => ({ params: { id: "aaaab" }, query: {}, meta: {} }),
  useRouter: () => ({ push: vi.fn(), replace: vi.fn() }),
}));

const stubs = {
  "router-link": {
    template: '<a class="router-link"><slot /></a>',
    props: ["to"],
  },
  "router-view": true,
};

const blog = {
  id: "b-1",
  publicId: "aaaab",
  title: "Дневник приключенца",
  status: "Active",
  author: { id: "u-1", username: "TestSeniorMod", role: "SeniorModerator" },
  assistants: [],
  subscribersCount: 0,
  premoderationStatus: "AwaitingApproval",
} as unknown as Blog;

describe("BlogPage", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    mockGetBlog.mockReset();
  });

  it("prints the blog's name and nothing else", async () => {
    mockGetBlog.mockResolvedValue({ data: { resource: blog } });
    const wrapper = mount(BlogPage, { global: { stubs } });
    await flushPromises();

    expect(wrapper.find("h1").text()).toBe("Дневник приключенца");
    expect(wrapper.find(".blog-header").text()).toBe("Дневник приключенца");
  });

  it("holds the header's height while the blog is on the wire", () => {
    mockGetBlog.mockReturnValue(new Promise(() => {}));
    const wrapper = mount(BlogPage, { global: { stubs } });

    expect(wrapper.find("h1").exists()).toBe(false);
    expect(wrapper.find(".blog-header .skeleton-title").exists()).toBe(true);
  });
});
