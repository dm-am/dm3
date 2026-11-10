/**
 * @vitest-environment jsdom
 */

/** The form of a review: offered by the server's answer and the page, refusals in itself. */
import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { enableAutoUnmount, mount, flushPromises } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { postApi, PostReviewItem } from "@/entities/game";
import type { Post } from "@/entities/game";
import { useAuthStore } from "@/shared/stores";
import { useToast } from "@/shared/lib/composables/useToast";
import GamePost from "./GamePost.vue";
import {
  REVIEWS,
  ROOM,
  answered,
  render,
  roomPost,
  routerAt,
  stubResizeObserver,
} from "./GamePost.fixtures";

// A post left mounted keeps its reviews in the registry of "Развернуть все".
enableAutoUnmount(afterEach);

// The composer behind the inline edit is tiptap; nothing here composes text.
vi.mock("@/shared/ui/BBCodeEditor", async () =>
  (await import("@/shared/lib/testing/bbcodeEditorStub")).bbcodeEditor(),
);

stubResizeObserver();

/** A refused review answers in the form, not in the corner. */
describe("GamePost answers a refused review in the form", () => {
  const toast = useToast();
  const saidOutLoud = () => toast.toasts.value.map((t) => t.message);

  beforeEach(() => {
    localStorage.clear();
    vi.spyOn(postApi, "getPostReviews").mockResolvedValue({
      data: { resources: REVIEWS },
      error: undefined,
    } as never);
  });

  afterEach(() => {
    vi.restoreAllMocks();
    // Toasts are module state and an error toast never fades.
    for (const standing of toast.toasts.value) toast.dismiss(standing.id);
  });

  /** A signed-in reader who has not rated the post, with the reviews open. */
  async function renderReader() {
    const wrapper = await render(ROOM, answered(roomPost), {
      viewer: "Reader",
    });
    await wrapper.get(".rating-value").trigger("click");
    await flushPromises();
    return wrapper;
  }

  type Reader = Awaited<ReturnType<typeof renderReader>>;

  const refuseWith = (error: Record<string, unknown>) =>
    vi.spyOn(postApi, "createPostReview").mockResolvedValue({
      data: null,
      error,
    } as never);

  async function send(wrapper: Reader) {
    await wrapper.get(".review-input").setValue("Хороший ход");
    await wrapper.get(".submit-btn").trigger("click");
    await flushPromises();
  }

  const lineOf = (wrapper: Reader) => wrapper.find(".review-form .form-error");

  /** The sign is a toggle of segments; "+" is pressed when the form opens. */
  it("sends the sign pressed among the segments", async () => {
    const create = vi.spyOn(postApi, "createPostReview").mockResolvedValue({
      data: null,
      error: { title: "нет" },
    } as never);
    const wrapper = await renderReader();
    const signs = () => wrapper.findAll(".review-form .segment");

    expect(signs().map((s) => s.text())).toEqual(["+", "=", "−"]);
    expect(signs()[0].attributes("aria-pressed")).toBe("true");

    await signs()[2].trigger("click");
    expect(signs()[2].attributes("aria-pressed")).toBe("true");
    await send(wrapper);

    expect(create).toHaveBeenCalledWith(expect.anything(), {
      sign: -1,
      text: "Хороший ход",
    });
    wrapper.unmount();
  });

  // A newbie is offered "=" alone, and that is the sign sent.
  it("sends the neutral sign for a newbie", async () => {
    const create = refuseWith({ title: "нет" });
    const wrapper = await render(ROOM, answered(roomPost), {
      viewer: { username: "Reader", isNewbie: true },
    });
    await wrapper.get(".rating-value").trigger("click");
    await flushPromises();

    await send(wrapper);

    expect(create).toHaveBeenCalledWith(expect.anything(), {
      sign: 0,
      text: "Хороший ход",
    });
  });

  it("prints the reason the server named under the draft, and raises no toast", async () => {
    refuseWith({
      status: 409,
      type: "",
      title: "Вы уже оценили этот пост",
      traceId: "t",
    });
    const wrapper = await renderReader();

    await send(wrapper);

    expect(lineOf(wrapper).text()).toBe("Вы уже оценили этот пост");
    expect(saidOutLoud()).toEqual([]);
    // The draft stays and the button takes the next press.
    expect(
      (wrapper.get(".review-input").element as HTMLTextAreaElement).value,
    ).toBe("Хороший ход");
    expect(wrapper.get(".submit-btn").attributes("disabled")).toBeUndefined();
  });

  it("names the action when the server named nothing", async () => {
    refuseWith({ status: 404, type: "", title: "", traceId: "t" });
    const wrapper = await renderReader();

    await send(wrapper);

    expect(lineOf(wrapper).text()).toBe("Не удалось отправить оценку");
    expect(saidOutLoud()).toEqual([]);
  });

  // A 5xx stands in the form in the server's words, and the plate is taken down.
  it("says a failure the interceptor used to plate in the form", async () => {
    refuseWith({
      status: 500,
      type: "",
      title: "Ошибка сервера",
      traceId: "t",
    });
    const wrapper = await renderReader();

    await send(wrapper);

    expect(lineOf(wrapper).text()).toBe("Ошибка сервера");
    expect(saidOutLoud()).toEqual([]);
  });

  // A dead connection is the plate's to say.
  it("stays quiet about a request that never arrived", async () => {
    refuseWith({ status: 0, type: "", title: "", traceId: "" });
    const wrapper = await renderReader();

    await send(wrapper);

    expect(lineOf(wrapper).exists()).toBe(false);
  });
});

/** The rating is offered by the server's answer, narrowed by what happened on the page. */
describe("GamePost offers the rating by the server's answer", () => {
  const unrated: Post = { ...roomPost, reviewCount: 0, rating: 0 };

  beforeEach(() => {
    localStorage.clear();
    vi.spyOn(postApi, "getPostReviews").mockResolvedValue({
      // A copy: a rating sent in one test would stand in the list of the next.
      data: { resources: [...REVIEWS] },
      error: undefined,
    } as never);
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  /** Posts on one page, as a room shows them, for `viewer`. */
  async function renderPage(viewer: string | null, posts: Post[]) {
    const router = await routerAt(ROOM);
    const pinia = createPinia();
    setActivePinia(pinia);
    if (viewer) {
      useAuthStore().user = { username: viewer, role: "Player" } as never;
    }
    const wrappers = posts.map((subject) =>
      mount(GamePost, {
        props: { post: subject, number: 7 },
        global: { plugins: [pinia, router] },
      }),
    );
    await flushPromises();
    return wrappers;
  }

  // The rating closes its game; the other posts of it came with an older yes.
  it("closes the other posts of the game on the page once a rating is sent", async () => {
    vi.spyOn(postApi, "createPostReview").mockResolvedValue({
      data: {
        resource: {
          id: "r-mine",
          sign: "Neutral",
          text: "<p>Хороший ход</p>",
          createdUtc: "2026-07-27T10:00:00",
          author: { username: "Reader", role: "Player" },
        },
      },
      error: undefined,
    } as never);
    const [rated, sameGame, otherGame] = await renderPage("Reader", [
      answered({ ...unrated, id: "p-a" } as Post, true, "game-rated-here"),
      answered({ ...unrated, id: "p-b" } as Post, true, "game-rated-here"),
      answered({ ...unrated, id: "p-c" } as Post, true, "another-game"),
    ]);

    await rated.get("button.rating-value").trigger("click");
    await flushPromises();
    await rated.get(".review-input").setValue("Хороший ход");
    await rated.get(".submit-btn").trigger("click");
    await flushPromises();

    expect(rated.find(".review-form").exists()).toBe(false);
    expect(sameGame.find("button.rating-value").exists()).toBe(false);
    expect(otherGame.find("button.rating-value").exists()).toBe(true);
  });

  // The newest rating first, under the number the server gives it.
  it("puts the rating just sent first, under the number the server gives it", async () => {
    vi.spyOn(postApi, "createPostReview").mockResolvedValue({
      data: {
        resource: {
          id: "r-mine",
          sign: "Neutral",
          text: "<p>Хороший ход</p>",
          createdUtc: "2026-07-27T10:00:00",
          author: { username: "Reader", role: "Player" },
        },
      },
      error: undefined,
    } as never);
    // A game of its own: a rating sent above closed "game-guid" to the rater.
    const [rated] = await renderPage("Reader", [
      answered(roomPost, true, "game-rated-first"),
    ]);

    await rated.get("button.rating-value").trigger("click");
    await flushPromises();
    await rated.get(".review-input").setValue("Хороший ход");
    await rated.get(".submit-btn").trigger("click");
    await flushPromises();

    const items = rated.findAll(".reviews-section > :not(.review-form)");
    expect(items).toHaveLength(3);
    expect(items[0].text()).toContain("Хороший ход");
    expect(items[0].text()).toContain("#1");
    expect(items[1].text()).toContain("Свежий отзыв");
  });

  // A guest who signs in is asked about anew, and offered nothing before it.
  it("asks the server again for a viewer who signed in", async () => {
    let answer: (value: unknown) => void = () => undefined;
    const getPost = vi.spyOn(postApi, "getPost").mockReturnValue(
      new Promise((resolve) => {
        answer = resolve;
      }) as never,
    );
    const [wrapper] = await renderPage(null, [answered(unrated, false)]);
    expect(wrapper.find("button.rating-value").exists()).toBe(false);

    useAuthStore().user = { username: "Reader", role: "Player" } as never;
    await flushPromises();
    expect(getPost).toHaveBeenCalledWith("p-1", {
      ownsRefusal: true,
      ownsRateLimit: true,
    });
    expect(wrapper.find("button.rating-value").exists()).toBe(false);

    answer({
      data: { resource: answered(unrated, true) },
      error: undefined,
    });
    await flushPromises();
    expect(wrapper.find("button.rating-value").exists()).toBe(true);
  });

  // A yes for one viewer is not a yes for the next.
  it("offers the next viewer nothing until the server answers for them", async () => {
    vi.spyOn(postApi, "getPost").mockReturnValue(
      new Promise(() => undefined) as never,
    );
    const [wrapper] = await renderPage("Reader", [answered(unrated, true)]);
    expect(wrapper.find("button.rating-value").exists()).toBe(true);

    useAuthStore().user = { username: "Another", role: "Player" } as never;
    await flushPromises();

    expect(wrapper.find("button.rating-value").exists()).toBe(false);
  });

  it("offers nothing to a viewer who signed out", async () => {
    const [wrapper] = await renderPage("Reader", [answered(unrated, true)]);
    expect(wrapper.find("button.rating-value").exists()).toBe(true);

    useAuthStore().user = null;
    await flushPromises();

    expect(wrapper.find("button.rating-value").exists()).toBe(false);
  });

  // Whether a rating taken back may be given again is the server's to say.
  it("asks again when the viewer takes their rating back", async () => {
    const getPost = vi.spyOn(postApi, "getPost").mockResolvedValue({
      data: { resource: answered(roomPost, true) },
      error: undefined,
    } as never);
    const [wrapper] = await renderPage("Vasya", [answered(roomPost, false)]);
    await wrapper.get("button.rating-value").trigger("click");
    await flushPromises();
    expect(wrapper.find(".review-form").exists()).toBe(false);

    wrapper
      .findAllComponents(PostReviewItem)[0]
      .vm.$emit("deleted", { id: "r-new", sign: 1 });
    await flushPromises();

    expect(getPost).toHaveBeenCalledWith("p-1", {
      ownsRefusal: true,
      ownsRateLimit: true,
    });
    expect(wrapper.find(".review-form").exists()).toBe(true);
  });
});
