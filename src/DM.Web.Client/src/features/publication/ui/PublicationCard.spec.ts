/**
 * @vitest-environment jsdom
 */

// The blog's list closes comments only: "Нравится" stays open to the reader on it.
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { defineComponent, h } from "vue";
import { mount, flushPromises } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { blogApi, useBlogDetailsStore } from "@/entities/blog";
import type { Blog, Publication } from "@/entities/blog";
import { useAuthStore } from "@/shared/stores";
import PublicationCard from "./PublicationCard.vue";

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
            { class: "like", onClick: () => emit("toggleLike") },
            String(props.likes.length),
          )
        : h("span", { class: "like" }, String(props.likes.length));
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
    viewCount: 0,
    commentCount: 0,
    likes: [{ id: "u-other", username: "Другой" }],
    ...overrides,
  }) as Publication;

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

describe("PublicationCard: the like on the blog's blacklist", () => {
  beforeEach(() => {
    localStorage.clear();
  });

  afterEach(() => vi.restoreAllMocks());

  it("lets a listed reader like as anybody does", async () => {
    const like = vi
      .spyOn(blogApi, "likePublication")
      .mockResolvedValue({ data: { resource: ME }, error: null } as never);
    const wrapper = mountCard(aPublication(), {
      id: "b-1",
      viewerBlacklisted: true,
    } as Partial<Blog>);

    await wrapper.get("button.like").trigger("click");
    await flushPromises();

    expect(like).toHaveBeenCalledWith("p-1");
  });

  // The profile: no blog is loaded around the card.
  it("keeps the like outside the blog too", () => {
    const wrapper = mountCard(aPublication(), null);

    expect(wrapper.find("button.like").exists()).toBe(true);
  });
});
