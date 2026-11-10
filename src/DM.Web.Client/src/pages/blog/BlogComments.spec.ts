/**
 * @vitest-environment jsdom
 */

// Who may write is the blog's answer about the viewer (viewer.canComment), and
// nothing the page works out itself; the blacklist closes writing, not reading,
// and takes "Редактировать" off the viewer's own comments.
import { describe, it, expect, vi, beforeEach } from "vitest";
import { defineComponent, h, nextTick } from "vue";
import { createPinia, setActivePinia } from "pinia";
import { useBlogDetailsStore, type Blog } from "@/entities/blog";
import { createMockBlog } from "@/entities/blog/model/store.fixtures";
import { mountPage } from "@/shared/lib/testing/mountPage";
import { useAuthStore } from "@/entities/user";
import BlogComments from "./BlogComments.vue";

/** The section, down to the props this page hands it. */
const handed = vi.hoisted(() => ({
  props: {} as Record<string, unknown>,
}));
vi.mock("@/widgets/discussion", () => ({
  DiscussionSection: defineComponent({
    props: ["canComment", "closedHint", "viewerBlacklisted"],
    setup(props) {
      return () => {
        handed.props = { ...props };
        return h("div", { class: "discussion-stub" });
      };
    },
  }),
}));

const BLACKLISTED = "Вы в черном списке этого блога: писать комментарии нельзя";

async function render(blog: Partial<Blog> = {}) {
  const pinia = createPinia();
  setActivePinia(pinia);
  const store = useBlogDetailsStore();
  store.blog = {
    ...createMockBlog("the-blog-guid", "Дневник"),
    viewer: { canComment: true },
    ...blog,
  };
  const loadBlog = vi
    .spyOn(store, "loadBlog")
    .mockResolvedValue({ ok: true } as never);
  const { wrapper } = await mountPage(
    BlogComments,
    "/blogs/the-blog/comments",
    {
      routes: [["blog-comments", "/blogs/:id/comments"]],
      pinia,
    },
  );
  return { wrapper, loadBlog };
}

describe("BlogComments", () => {
  beforeEach(() => {
    localStorage.clear();
    localStorage.setItem("user", JSON.stringify({ username: "Reader" }));
    handed.props = {};
  });

  it("offers the composer to the reader the blog takes a comment from", async () => {
    await render();

    expect(handed.props).toEqual({
      canComment: true,
      closedHint: undefined,
      viewerBlacklisted: false,
    });
  });

  // A ban outside one's own blog is only the server's answer, and it is enough.
  it("offers nothing where the blog takes no comment from the reader", async () => {
    await render({ viewer: { canComment: false } });

    expect(handed.props).toEqual({
      canComment: false,
      closedHint: undefined,
      viewerBlacklisted: false,
    });
  });

  // A list of blogs carries no answer, and no answer is no "yes".
  it("offers nothing on a blog that says nothing about the viewer", async () => {
    await render({ viewer: undefined });

    expect(handed.props.canComment).toBe(false);
  });

  // The one on the list reads and never writes; the line says why there is no form.
  it("says the blacklist under the discussion and tells the comments", async () => {
    await render({ viewerBlacklisted: true, viewer: { canComment: false } });

    expect(handed.props).toEqual({
      canComment: false,
      closedHint: BLACKLISTED,
      viewerBlacklisted: true,
    });
  });

  it("offers no composer to a guest on an answer given to a session that ended", async () => {
    localStorage.clear();
    await render({ viewer: { canComment: true } });

    expect(handed.props.canComment).toBe(false);
    expect(handed.props.closedHint).toBeUndefined();
  });

  // The shell does not read the blog again on a sign-in, so this page does.
  it("reads the blog again when the viewer changes, and only then", async () => {
    localStorage.clear();
    const { loadBlog } = await render({ viewer: { canComment: false } });
    await nextTick();
    expect(loadBlog).not.toHaveBeenCalled();

    useAuthStore().updateUser({ username: "Reader" } as never);
    await nextTick();

    expect(loadBlog).toHaveBeenCalledOnce();
    expect(loadBlog).toHaveBeenCalledWith("the-blog");
  });
});
