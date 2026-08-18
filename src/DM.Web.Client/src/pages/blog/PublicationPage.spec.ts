/**
 * @vitest-environment jsdom
 */

/**
 * A publication had no page. The feed card named it in plain text, the profile
 * spotlight did the same, and a notification about a new publication, a like on
 * one or a comment under one opened the blog — the reader was handed the whole
 * feed and left to find the text the notification was about.
 *
 * What this holds: the page reads the publication named in the address, hands
 * it to the card the product decided a publication looks like, runs its
 * discussion against the publication's own endpoints, and offers the composer
 * to exactly whom the server would take a comment from
 * (PublicationIntentionResolver.CreateComment: signed in, published, comments
 * enabled). The blog's own commentsEnabled flag is not that rule and does not
 * reach here.
 */
import { describe, expect, it, vi, beforeEach } from "vitest";
import { defineComponent, h, watchEffect } from "vue";
import { flushPromises, mount, RouterLinkStub } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { blogApi, type Publication } from "@/entities/blog";
import { useAuthStore } from "@/entities/user";
import { provideZoneSection } from "@/shared/lib/composables/useZoneSection";
import PublicationPage from "./PublicationPage.vue";

vi.mock("vue-router", async (importOriginal) => ({
  ...(await importOriginal<typeof import("vue-router")>()),
  useRoute: () => ({
    params: { id: "the-blog", pubId: "pub-readable" },
    query: {},
    hash: "",
  }),
  useRouter: () => ({ replace: vi.fn(), push: vi.fn() }),
}));

// The card is the topic-copy bubble and has its own spec; here only the fact
// that the publication reaches it matters.
vi.mock("@/features/publication", () => ({
  PublicationCard: {
    name: "PublicationCard",
    props: { publication: Object, standalone: Boolean, truncatable: Boolean },
    template: "<div class='card-stub'>{{ publication.title }}</div>",
  },
}));

// The section itself is the site-wide one (its own spec covers it): what this
// page owes it is the wiring, which the stub exposes as props.
vi.mock("@/widgets/discussion", () => ({
  DiscussionSection: {
    name: "DiscussionSection",
    props: [
      "comments",
      "paging",
      "loading",
      "failed",
      "recordId",
      "load",
      "create",
      "submitEdit",
      "submitDelete",
      "like",
      "unlike",
      "fetchEditSource",
      "pagingTo",
      "draftKey",
      "canComment",
      "closedHint",
    ],
    template: "<div class='discussion-stub' />",
  },
}));

const PUBLICATION: Publication = {
  id: "pub-guid",
  blogId: "blog-guid",
  title: "Полет над гнездом",
  content: "<p>Текст</p>",
  preview: "",
  author: { username: "SolohinLex" },
  createdUtc: "2026-08-01T10:00:00Z",
  isPublished: true,
  commentsEnabled: true,
  viewCount: 0,
  commentCount: 2,
  likes: [],
} as unknown as Publication;

function render({
  publication = PUBLICATION,
  username = null as string | null,
} = {}) {
  const pinia = createPinia();
  setActivePinia(pinia);
  useAuthStore().user = username ? ({ username } as never) : null;

  vi.spyOn(blogApi, "getPublication").mockResolvedValue({
    data: { resource: publication },
    error: null,
  } as never);
  vi.spyOn(blogApi, "getPublicationComments").mockResolvedValue({
    data: { resources: [], paging: null },
    error: null,
  } as never);

  // The zone shell renders the heading, so the section this page announces is
  // what the test can see of it.
  const announced = { value: undefined as string | undefined };
  const host = defineComponent({
    setup() {
      const section = provideZoneSection();
      watchEffect(() => {
        announced.value = section.value;
      });
      return () => h(PublicationPage);
    },
  });

  const wrapper = mount(host, {
    global: { plugins: [pinia], stubs: { RouterLink: RouterLinkStub } },
  });
  return { wrapper, announced };
}

/** The discussion's props, as the page handed them over. */
const discussion = (wrapper: ReturnType<typeof render>["wrapper"]) =>
  wrapper.findComponent({ name: "DiscussionSection" }).props() as Record<
    string,
    unknown
  >;

describe("PublicationPage", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
  });

  it("reads the publication named in the address and draws its card", async () => {
    const { wrapper, announced } = render();
    await flushPromises();

    expect(blogApi.getPublication).toHaveBeenCalledWith("pub-readable");
    expect(wrapper.find(".card-stub").text()).toContain("Полет над гнездом");
    // The card is the page, so it draws no title of its own: the heading of
    // the zone reads "{блог} | {публикация}", and this is its second half.
    expect(announced.value).toBe("Полет над гнездом");
    expect(
      wrapper.findComponent({ name: "PublicationCard" }).props("standalone"),
    ).toBe(true);
  });

  it("runs the discussion against the publication, not the blog", async () => {
    const { wrapper } = render();
    await flushPromises();

    const props = discussion(wrapper);
    // The guid the server answered with, never the readable id from the URL:
    // both open the publication, and one of them is what the comments hang on.
    expect(props.recordId).toBe("pub-guid");

    await (props.load as (q: unknown) => unknown)({ number: 2 });
    expect(blogApi.getPublicationComments).toHaveBeenCalledWith("pub-guid", {
      number: 2,
    });

    const create = vi
      .spyOn(blogApi, "createPublicationComment")
      .mockResolvedValue({ data: null, error: null } as never);
    await (props.create as (text: string) => unknown)("Отличный текст");
    expect(create).toHaveBeenCalledWith("pub-guid", { text: "Отличный текст" });
  });

  it("offers no composer to a guest", async () => {
    const { wrapper } = render();
    await flushPromises();

    const props = discussion(wrapper);
    expect(props.canComment).toBe(false);
    // No hint either: a guest is told to sign in, and a hint would stand
    // instead of that invitation.
    expect(props.closedHint).toBeUndefined();
  });

  it("offers the composer to a signed-in reader of a published text", async () => {
    const { wrapper } = render({ username: "gamer" });
    await flushPromises();

    expect(discussion(wrapper).canComment).toBe(true);
  });

  it("refuses the composer when the publication has comments off", async () => {
    const { wrapper } = render({
      username: "gamer",
      publication: { ...PUBLICATION, commentsEnabled: false },
    });
    await flushPromises();

    const props = discussion(wrapper);
    expect(props.canComment).toBe(false);
    expect(props.closedHint).toBe("Комментарии к этой публикации отключены");
  });

  it("refuses the composer on a draft the author is reading", async () => {
    const { wrapper } = render({
      username: "SolohinLex",
      publication: { ...PUBLICATION, isPublished: false },
    });
    await flushPromises();

    const props = discussion(wrapper);
    expect(props.canComment).toBe(false);
    expect(props.closedHint).toContain("после публикации");
  });

  it("says a failed load failed instead of drawing an empty page", async () => {
    const pinia = createPinia();
    setActivePinia(pinia);
    vi.spyOn(blogApi, "getPublication").mockResolvedValue({
      data: null,
      error: { type: "", title: "Публикация не найдена", status: 404 },
    } as never);

    const wrapper = mount(PublicationPage, { global: { plugins: [pinia] } });
    await flushPromises();

    expect(wrapper.text()).toContain("Публикация не найдена");
    expect(wrapper.find(".discussion-stub").exists()).toBe(false);
  });
});
