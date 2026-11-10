/**
 * @vitest-environment jsdom
 */

// The page reads the publication in the address, hands it to the card, runs
// its discussion on the publication's endpoints and offers the composer by the
// publication's answer about the viewer. The blacklist closes writing, not
// reading.
import { describe, expect, it, vi, beforeEach } from "vitest";
import { defineComponent, h, watchEffect } from "vue";
import { flushPromises } from "@vue/test-utils";
import { createPinia, setActivePinia, type Pinia } from "pinia";
import {
  blogApi,
  useBlogDetailsStore,
  type Publication,
} from "@/entities/blog";
import { createMockBlog } from "@/entities/blog/model/store.fixtures";
import { mountPage } from "@/shared/lib/testing/mountPage";
import { useAuthStore } from "@/entities/user";
import { provideZoneSection } from "@/shared/lib/composables/useZoneSection";
import PublicationPage from "./PublicationPage.vue";

/** The publication's own address, which names the one the page reads. */
const at = (page: object, pinia?: Pinia) =>
  mountPage(page, "/blogs/the-blog/feed/pub-readable", {
    routes: [["blog-publication", "/blogs/:id/feed/:pubId"]],
    pinia,
  });

// The card has its own spec; here only the publication reaching it matters.
vi.mock("@/features/publication", () => ({
  PublicationCard: {
    name: "PublicationCard",
    props: { publication: Object, standalone: Boolean, truncatable: Boolean },
    template: "<div class='card-stub'>{{ publication.title }}</div>",
  },
}));

// The site-wide section has its own spec; the stub exposes the wiring as props.
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
  viewCount: 0,
  commentCount: 2,
  likes: [],
  // As a signed-in reader is answered; a guest is told no by the server.
  viewer: { canComment: true },
} as unknown as Publication;

async function render({
  publication = PUBLICATION,
  username = null as string | null,
  blacklisted = false,
} = {}) {
  const pinia = createPinia();
  setActivePinia(pinia);
  useAuthStore().user = username ? ({ username } as never) : null;
  // The blog the shell has loaded: its blacklist is the publication's too.
  useBlogDetailsStore().blog = {
    ...createMockBlog("blog-guid", "Записки"),
    viewerBlacklisted: blacklisted,
  };

  vi.spyOn(blogApi, "getPublication").mockResolvedValue({
    data: { resource: publication },
    error: null,
  } as never);
  vi.spyOn(blogApi, "getPublicationComments").mockResolvedValue({
    data: { resources: [], paging: null },
    error: null,
  } as never);

  // The zone shell renders the heading, so the announced section is what the test sees.
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

  const { wrapper } = await at(host, pinia);
  return { wrapper, announced };
}

/** The discussion's props, as the page handed them over. */
const discussion = (wrapper: Awaited<ReturnType<typeof render>>["wrapper"]) =>
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
    const { wrapper, announced } = await render();
    await flushPromises();

    // The address alone; how the refusal is carried is checked below.
    const [askedFor] =
      vi.mocked(blogApi.getPublication).mock.calls.at(-1) ?? [];
    expect(askedFor).toBe("pub-readable");
    expect(wrapper.find(".card-stub").text()).toContain("Полет над гнездом");
    // The card draws no title: the zone heading reads "{блог} | {публикация}".
    expect(announced.value).toBe("Полет над гнездом");
    expect(
      wrapper.findComponent({ name: "PublicationCard" }).props("standalone"),
    ).toBe(true);
  });

  it("runs the discussion against the publication, not the blog", async () => {
    const { wrapper } = await render();
    await flushPromises();

    const props = discussion(wrapper);
    // The guid the server answered with, which the comments hang on.
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
    const { wrapper } = await render();
    await flushPromises();

    const props = discussion(wrapper);
    expect(props.canComment).toBe(false);
    // Not closed: a guest is told to sign in, and signing in opens the composer.
    expect(props.closedHint).toBeUndefined();
  });

  it("offers the composer to a signed-in reader of a published text", async () => {
    const { wrapper } = await render({ username: "gamer" });
    await flushPromises();

    expect(discussion(wrapper).canComment).toBe(true);
  });

  // A ban is only the server's answer, and it is enough.
  it("offers nothing where the publication takes no comment from the reader", async () => {
    const { wrapper } = await render({
      username: "gamer",
      publication: { ...PUBLICATION, viewer: { canComment: false } },
    });
    await flushPromises();

    const props = discussion(wrapper);
    expect(props.canComment).toBe(false);
    expect(props.closedHint).toBeUndefined();
  });

  // A feed carries no answer, and no answer is no "yes".
  it("offers nothing on a publication that says nothing about the viewer", async () => {
    const { wrapper } = await render({
      username: "gamer",
      publication: { ...PUBLICATION, viewer: undefined },
    });
    await flushPromises();

    expect(discussion(wrapper).canComment).toBe(false);
  });

  // The answer is about whoever held the session; a sign-in asks again.
  it("reads the publication again when the viewer changes", async () => {
    const { wrapper } = await render({
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

  // The one on the blacklist reads the discussion and the line says why he cannot write.
  it("says the blog's blacklist under the discussion", async () => {
    const { wrapper } = await render({
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
    vi.spyOn(blogApi, "getPublication").mockResolvedValue({
      data: null,
      error: { type: "", title: "Публикация не найдена", status: 404 },
    } as never);

    const { wrapper } = await at(PublicationPage);
    await flushPromises();

    expect(wrapper.text()).toContain("Публикация не найдена");
    expect(wrapper.find(".discussion-stub").exists()).toBe(false);
  });

  // The card is the whole page when the read fails, so the read owns the 5xx;
  // asserted on the flags, since the client is behind the mocked service.
  it("takes a 5xx over from the interceptor and says it in the card", async () => {
    const read = vi.spyOn(blogApi, "getPublication").mockResolvedValue({
      data: null,
      error: { type: "", title: "Ошибка сервера", status: 500, traceId: "t" },
    } as never);

    const { wrapper } = await at(PublicationPage);
    await flushPromises();

    expect(read).toHaveBeenCalledWith(
      "pub-readable",
      expect.objectContaining({ ownsRefusal: true, ownsRateLimit: true }),
    );
    expect(wrapper.text()).toContain("Ошибка сервера");
  });

  it("says the wait in the card, with the seconds the server asked for", async () => {
    // The 429 title carries no number, so the card says the seconds itself.
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

    const { wrapper } = await at(PublicationPage);
    await flushPromises();

    expect(wrapper.text()).toContain(
      "Слишком много попыток, попробуйте через 30 секунд",
    );
  });
});
