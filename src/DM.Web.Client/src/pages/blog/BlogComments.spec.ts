/**
 * @vitest-environment jsdom
 */

/**
 * The blog's part of its discussion: who may write, and what is said under it
 * to the one who may read and not write. The twin of
 * pages/game/GameComments.spec.ts.
 *
 * Who may write is the blog's answer about the viewer (viewer.canComment),
 * asked on the server by the code that refuses the comment: the blacklist and
 * the ban with its exemption for one's own blog. The page offers the composer
 * by it and by nothing it works out itself. Comments of a blog cannot be
 * turned off, so nothing closes the discussion to everybody. The blacklist
 * closes it to writing and not to reading: it stays, empty or not, with the
 * plaque of a refusal where the form would be. The blacklist flag still
 * reaches the comments, where it takes "Редактировать" off the viewer's own.
 */
import { describe, it, expect, vi, beforeEach } from "vitest";
import { defineComponent, h, nextTick } from "vue";
import { mount } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { useBlogDetailsStore, type Blog } from "@/entities/blog";
import { useAuthStore } from "@/entities/user";
import BlogComments from "./BlogComments.vue";

vi.mock("vue-router", () => ({
  useRoute: () => ({ params: { id: "the-blog" }, query: {} }),
}));

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

function render(blog: Partial<Record<string, unknown>> = {}) {
  setActivePinia(createPinia());
  const store = useBlogDetailsStore();
  store.blog = {
    id: "the-blog-guid",
    viewer: { canComment: true },
    ...blog,
  } as unknown as Blog;
  const loadBlog = vi
    .spyOn(store, "loadBlog")
    .mockResolvedValue({ ok: true } as never);
  return { wrapper: mount(BlogComments), loadBlog };
}

describe("BlogComments", () => {
  beforeEach(() => {
    localStorage.clear();
    localStorage.setItem("user", JSON.stringify({ username: "Reader" }));
    handed.props = {};
  });

  it("offers the composer to the reader the blog takes a comment from", () => {
    render();

    expect(handed.props).toEqual({
      canComment: true,
      closedHint: undefined,
      viewerBlacklisted: false,
    });
  });

  // A ban outside one's own blog is nothing the page can see in the blog: the
  // answer of the server is the only thing that says no, and it is enough.
  it("offers nothing where the blog takes no comment from the reader", () => {
    render({ viewer: { canComment: false } });

    expect(handed.props).toEqual({
      canComment: false,
      closedHint: undefined,
      viewerBlacklisted: false,
    });
  });

  // A list of blogs carries no answer, and an answer that did not come is no
  // "yes".
  it("offers nothing on a blog that says nothing about the viewer", () => {
    render({ viewer: undefined });

    expect(handed.props.canComment).toBe(false);
  });

  // The one on the list reads and never writes: the discussion stays, empty
  // or not, and the line under it says why there is no form.
  it("says the blacklist under the discussion and tells the comments", () => {
    render({ viewerBlacklisted: true, viewer: { canComment: false } });

    expect(handed.props).toEqual({
      canComment: false,
      closedHint: BLACKLISTED,
      viewerBlacklisted: true,
    });
  });

  it("offers no composer to a guest on an answer given to a session that ended", () => {
    localStorage.clear();
    render({ viewer: { canComment: true } });

    expect(handed.props.canComment).toBe(false);
    expect(handed.props.closedHint).toBeUndefined();
  });

  // The blog shell does not read the blog again on a sign-in, and the answer
  // is about whoever held the session then: the guest who signs in from the
  // prompt would be left with neither the prompt nor the form.
  it("reads the blog again when the viewer changes, and only then", async () => {
    localStorage.clear();
    const { loadBlog } = render({ viewer: { canComment: false } });
    await nextTick();
    expect(loadBlog).not.toHaveBeenCalled();

    useAuthStore().updateUser({ username: "Reader" } as never);
    await nextTick();

    expect(loadBlog).toHaveBeenCalledOnce();
    expect(loadBlog).toHaveBeenCalledWith("the-blog");
  });
});
