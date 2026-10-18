/**
 * @vitest-environment jsdom
 */

/**
 * The heart of a publication on the blog's blacklist.
 *
 * The blog's list closes comments only (BlogRoleExtensions.IsBlacklisted). A
 * like is feedback and stays open, so the heart of a publication is the heart
 * everybody gets, inside the blog and outside it. It used to be a counter for
 * the reader on the list; what this catches is that rule coming back:
 *  - the heart closed inside a blog whose list holds the reader;
 *  - the heart closed outside it, where the publication itself used to carry
 *    its blog's answer.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { defineComponent, h } from "vue";
import { mount, flushPromises } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { blogApi, useBlogDetailsStore } from "@/entities/blog";
import type { Blog, Publication } from "@/entities/blog";
import { useAuthStore } from "@/shared/stores";
import PublicationCard from "./PublicationCard.vue";

/**
 * The topic card the publication is drawn with, down to the heart: whether it
 * is offered, and the press on it.
 */
const TopicCardStub = defineComponent({
  name: "TopicCard",
  props: {
    canLike: { type: Boolean, default: false },
    isLikedByMe: { type: Boolean, default: false },
    likes: { type: Array, default: () => [] },
  },
  emits: ["toggleLike"],
  setup(props, { emit }) {
    return () =>
      props.canLike
        ? h(
            "button",
            { class: "heart", onClick: () => emit("toggleLike") },
            String(props.likes.length),
          )
        : h("span", { class: "heart" }, String(props.likes.length));
  },
});

const ME = { id: "u-me", username: "Читатель" };

const aPublication = (overrides: Partial<Publication> = {}): Publication =>
  ({
    id: "p-1",
    blogId: "b-1",
    author: { id: "u-author", username: "Автор" },
    title: "Заметки",
    content: "<p>Текст</p>",
    preview: "Текст",
    createdUtc: "2026-09-01T10:00:00Z",
    isPublished: true,
    viewCount: 0,
    commentCount: 0,
    likes: [{ id: "u-other", username: "Другой" }],
    ...overrides,
  }) as Publication;

/** The card inside a blog zone whose blog says what the viewer is to it. */
function mountCard(publication: Publication, zoneBlog: Partial<Blog> | null) {
  const pinia = createPinia();
  setActivePinia(pinia);
  useAuthStore().updateUser(ME as never);
  useBlogDetailsStore().blog = zoneBlog as never;
  return mount(PublicationCard, {
    props: { publication },
    global: {
      plugins: [pinia],
      stubs: { TopicCard: TopicCardStub, ActionResult: true },
    },
  });
}

describe("PublicationCard: сердце у черного списка блога", () => {
  beforeEach(() => {
    localStorage.clear();
  });

  afterEach(() => vi.restoreAllMocks());

  it("дает внесенному в список поставить лайк, как всем", async () => {
    const like = vi
      .spyOn(blogApi, "likePublication")
      .mockResolvedValue({ data: { resource: ME }, error: null } as never);
    const wrapper = mountCard(aPublication(), {
      id: "b-1",
      viewerBlacklisted: true,
    } as Partial<Blog>);

    await wrapper.get("button.heart").trigger("click");
    await flushPromises();

    expect(like).toHaveBeenCalledWith("p-1");
  });

  // The profile: no blog is loaded around the card.
  it("вне блога тоже оставляет сердце", () => {
    const wrapper = mountCard(aPublication(), null);

    expect(wrapper.find("button.heart").exists()).toBe(true);
  });
});
