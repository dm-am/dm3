/**
 * @vitest-environment jsdom
 */

// The shell prints the blog's name only; its loading state is the header's
// twin; a refused blog is the error page its status chooses.
import { describe, it, expect, beforeEach, vi } from "vitest";
import { mount, flushPromises } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import BlogPage from "./BlogPage.vue";
import { useBlogDetailsStore, type Blog } from "@/entities/blog";

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

  describe("failure", () => {
    const withErrorPage = {
      ...stubs,
      ErrorPage: {
        props: ["code"],
        template: "<div class='error-page'>{{ code }}</div>",
      },
    };

    it.each([
      // Gone reads as "не найден": the endpoint answers Gone for a deleted, a
      // mistyped and a hidden blog alike.
      [410, "404"],
      [403, "403"],
      [404, "404"],
      [500, "500"],
      // client.ts fills status 0 for a request that never reached the API.
      [0, "500"],
    ])("answers %i with its own error page", async (status, code) => {
      mockGetBlog.mockResolvedValue({ error: { status } });
      const wrapper = mount(BlogPage, { global: { stubs: withErrorPage } });
      await flushPromises();

      expect(wrapper.find(".error-page").text()).toBe(code);
      expect(wrapper.find(".blog-header").exists()).toBe(false);
    });

    it("draws no error page while the blog is still on the wire", () => {
      mockGetBlog.mockReturnValue(new Promise(() => {}));
      const wrapper = mount(BlogPage, { global: { stubs: withErrorPage } });

      expect(wrapper.find(".error-page").exists()).toBe(false);
    });

    // A reload outside the shell that is refused nulls `blog`, and the shell must
    // draw the refusal rather than hold the title skeleton.
    it("answers a refusal from a reload it did not start", async () => {
      mockGetBlog.mockResolvedValue({ data: { resource: blog } });
      const wrapper = mount(BlogPage, { global: { stubs: withErrorPage } });
      await flushPromises();
      expect(wrapper.find("h1").exists()).toBe(true);

      mockGetBlog.mockResolvedValue({ error: { status: 403 } });
      await useBlogDetailsStore().loadBlog("aaaab");
      await flushPromises();

      expect(wrapper.find(".error-page").text()).toBe("403");
    });
  });
});
