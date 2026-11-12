/**
 * @vitest-environment jsdom
 */

/** "Мои блоги" draws its skeleton as long as the block was at the viewer's last visit in this browser. */
import { describe, it, expect, beforeEach, vi } from "vitest";
import { mount, RouterLinkStub } from "@vue/test-utils";
import { nextTick } from "vue";
import { createPinia, setActivePinia } from "pinia";
import OwnedBlogs from "./OwnedBlogs.vue";
import { useBlogsStore, type BlogRef } from "@/entities/blog";
import { createMockBlogRef } from "@/entities/blog/model/store.fixtures";
import { useAuthStore } from "@/entities/user";
import { layOutRowsAs } from "./rememberedLines.fixtures";

const KEY = "dm_sidebar_lines:OwnedBlogs:viewer";

const stubs = {
  RouterLink: RouterLinkStub,
  BlogLink: { template: "<li class='blog-row' />", props: ["blog"] },
};

const blog = (id: string) => createMockBlogRef(id, `Блог ${id}`);

function mountBlock(blogs: BlogRef[] | null) {
  const store = useBlogsStore();
  useAuthStore().user = { username: "viewer" } as never;
  vi.spyOn(store, "fetchParticipatingBlogs").mockResolvedValue(
    undefined as never,
  );
  store.participatingBlogs = blogs;
  store.participatingBlogsLoading = blogs === null;
  return mount(OwnedBlogs, { global: { stubs } });
}

const rows = (wrapper: ReturnType<typeof mount>) =>
  wrapper.findAll(".sidebar-skeleton .skeleton-row").length;

describe("OwnedBlogs skeleton", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    localStorage.clear();
    vi.restoreAllMocks();
  });

  it("draws as many lines as the block had last time", () => {
    localStorage.setItem(KEY, "6");

    expect(rows(mountBlock(null))).toBe(6);
  });

  it("keeps the number of blogs the list drew", () => {
    mountBlock([blog("b-1"), blog("b-2"), blog("b-3"), blog("b-4")]);

    expect(localStorage.getItem(KEY)).toBe("4");
  });

  it("keeps the lines the rows took on screen, a wrapped title two", async () => {
    layOutRowsAs(3);
    mountBlock([blog("b-1"), blog("b-2")]);
    await nextTick();

    expect(localStorage.getItem(KEY)).toBe("3");
  });

  it("falls back to three lines on a broken value", () => {
    localStorage.setItem(KEY, "-1");

    expect(rows(mountBlock(null))).toBe(3);
  });
});
