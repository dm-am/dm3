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
 * by the publication's answer about the viewer (viewer.canComment), which the
 * server asks with the code that refuses the comment: published, the blog's
 * blacklist and the ban. A draft has no discussion at all: nothing to read and
 * nothing to write. Comments of a publication cannot be turned off. The blog's
 * blacklist closes a published one to writing and not to reading: it stays,
 * empty or not, with the plaque of a refusal where the form would be.
 */
import { describe, expect, it, vi, beforeEach } from "vitest";
import { defineComponent, h, watchEffect } from "vue";
import { flushPromises, mount, RouterLinkStub } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import {
  blogApi,
  useBlogDetailsStore,
  type Blog,
  type Publication,
} from "@/entities/blog";
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
      "viewerBlacklisted",
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
  viewCount: 0,
  commentCount: 2,
  likes: [],
  // As a signed-in reader is answered: a guest is told no by the server, and
  // the page must not take this "yes" for his.
  viewer: { canComment: true },
} as unknown as Publication;

function render({
  publication = PUBLICATION,
  username = null as string | null,
  blacklisted = false,
} = {}) {
  const pinia = createPinia();
  setActivePinia(pinia);
  useAuthStore().user = username ? ({ username } as never) : null;
  // The blog the shell has loaded around the page: its blacklist is the
  // publication's too.
  useBlogDetailsStore().blog = {
    id: "blog-guid",
    viewerBlacklisted: blacklisted,
  } as unknown as Blog;

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

    // The address slot alone: what travels beside it is how the refusal is
    // carried, and that is the subject of its own check below.
    const [askedFor] =
      vi.mocked(blogApi.getPublication).mock.calls.at(-1) ?? [];
    expect(askedFor).toBe("pub-readable");
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
    // Not closed either: a guest is told to sign in, and signing in opens the
    // composer here.
    expect(props.closedHint).toBeUndefined();
  });

  it("offers the composer to a signed-in reader of a published text", async () => {
    const { wrapper } = render({ username: "gamer" });
    await flushPromises();

    expect(discussion(wrapper).canComment).toBe(true);
  });

  // A ban is nothing the page can see in the publication: the answer of the
  // server is the only thing that says no, and it is enough.
  it("offers nothing where the publication takes no comment from the reader", async () => {
    const { wrapper } = render({
      username: "gamer",
      publication: { ...PUBLICATION, viewer: { canComment: false } },
    });
    await flushPromises();

    const props = discussion(wrapper);
    expect(props.canComment).toBe(false);
    expect(props.closedHint).toBeUndefined();
  });

  // A feed carries no answer, and an answer that did not come is no "yes".
  it("offers nothing on a publication that says nothing about the viewer", async () => {
    const { wrapper } = render({
      username: "gamer",
      publication: { ...PUBLICATION, viewer: undefined },
    });
    await flushPromises();

    expect(discussion(wrapper).canComment).toBe(false);
  });

  // The answer is about whoever held the session when the publication was
  // read: the guest who signs in from the prompt under the comments is asked
  // about again, and gets the form by the new answer.
  it("reads the publication again when the viewer changes", async () => {
    const { wrapper } = render({
      publication: { ...PUBLICATION, viewer: { canComment: false } },
    });
    await flushPromises();
    expect(blogApi.getPublication).toHaveBeenCalledOnce();

    vi.mocked(blogApi.getPublication).mockResolvedValue({
      data: { resource: PUBLICATION },
      error: null,
    } as never);
    useAuthStore().updateUser({ username: "gamer" } as never);
    await flushPromises();

    expect(blogApi.getPublication).toHaveBeenCalledTimes(2);
    expect(discussion(wrapper).canComment).toBe(true);
  });

  // A draft is not discussed, so it has no discussion at all: no composer for
  // the author reading it, and no sentence promising comments later.
  it("draws no discussion under a draft", async () => {
    const { wrapper } = render({
      username: "SolohinLex",
      publication: { ...PUBLICATION, isPublished: false },
    });
    await flushPromises();

    expect(wrapper.find(".card-stub").exists()).toBe(true);
    expect(wrapper.find(".discussion-stub").exists()).toBe(false);
    expect(wrapper.text()).not.toContain("после публикации");
  });

  // The blog's blacklist reaches its publications: the discussion is read and
  // not written by the one on the list, and the line in the composer's place
  // says why.
  it("says the blog's blacklist under the discussion", async () => {
    const { wrapper } = render({
      username: "gamer",
      blacklisted: true,
      publication: { ...PUBLICATION, viewer: { canComment: false } },
    });
    await flushPromises();

    const props = discussion(wrapper);
    expect(props.canComment).toBe(false);
    expect(props.closedHint).toBe(
      "Вы в черном списке этого блога: писать комментарии нельзя",
    );
    expect(props.viewerBlacklisted).toBe(true);
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

  /**
   * A broken server was announced twice on this page: the card said the API's
   * sentence where the text should have been, and the response interceptor put
   * the same sentence in a plate over it. The interceptor keeps quiet for a
   * request that says it draws the refusal itself, which this one is — the card
   * is the whole page when the read fails, and a plate in the corner explains
   * an empty screen to nobody.
   *
   * The flags are what is asserted, not the absent toast: the interceptor lives
   * in the HTTP client, and the client is behind the mocked service here.
   */
  it("takes a 5xx over from the interceptor and says it in the card", async () => {
    const pinia = createPinia();
    setActivePinia(pinia);
    const read = vi.spyOn(blogApi, "getPublication").mockResolvedValue({
      data: null,
      error: { type: "", title: "Ошибка сервера", status: 500, traceId: "t" },
    } as never);

    const wrapper = mount(PublicationPage, { global: { plugins: [pinia] } });
    await flushPromises();

    expect(read).toHaveBeenCalledWith(
      "pub-readable",
      expect.objectContaining({ ownsRefusal: true, ownsRateLimit: true }),
    );
    expect(wrapper.text()).toContain("Ошибка сервера");
  });

  it("says the wait in the card, with the seconds the server asked for", async () => {
    // The limiter's title names the refusal without the number, and the number
    // is the part a reader acts on. Taking the refusal over means the card has
    // to carry the sentence that holds it, or the wait is lost with the plate.
    const pinia = createPinia();
    setActivePinia(pinia);
    vi.spyOn(blogApi, "getPublication").mockResolvedValue({
      data: null,
      error: {
        type: "",
        title: "Слишком много запросов",
        status: 429,
        traceId: "t",
        retryAfterSeconds: 30,
      },
    } as never);

    const wrapper = mount(PublicationPage, { global: { plugins: [pinia] } });
    await flushPromises();

    expect(wrapper.text()).toContain(
      "Слишком много попыток, попробуйте через 30 секунд",
    );
  });
});
