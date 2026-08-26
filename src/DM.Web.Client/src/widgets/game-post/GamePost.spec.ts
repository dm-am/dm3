/**
 * @vitest-environment jsdom
 */

/**
 * A review has no page of its own, and the number beside it is its address.
 * That address used to be the current `window.location.pathname` plus a
 * `#review-` hash nothing in the client reads: on the home page it pointed at
 * the home page, and anywhere it landed the reader on a post whose reviews
 * block is collapsed and unfetched, so the review it was made for was not on
 * the screen at all.
 *
 * Where it leads now is the room the post lives in, anchored at the post and
 * naming the review. What the widget does when it is handed that address back
 * is open the block and mark the review named. The two halves are one
 * contract, so they are checked through one router: what the link addresses
 * is what the other half is asked to honour.
 */
import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { mount, flushPromises } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { createMemoryHistory, createRouter } from "vue-router";
import { gameApi, PostReviewItem } from "@/entities/game";
import type { Post, PostAttachment, PostReview } from "@/entities/game";
import { useAuthStore } from "@/shared/stores";
import GamePost from "./GamePost.vue";

// The composer behind the inline edit is tiptap; nothing here composes text.
vi.mock("@/shared/ui/BBCodeEditor", () => ({
  BBCodeEditor: {
    template: "<textarea class='editor-stub' :value='modelValue' />",
    props: ["modelValue"],
  },
}));

// The post sizes its meta column with a ResizeObserver, which jsdom has not.
// Geometry is not what this spec reads.
vi.stubGlobal(
  "ResizeObserver",
  class {
    observe() {}
    disconnect() {}
  },
);

/** Newest first, the order the reviews endpoint answers in. */
const REVIEWS = [
  {
    id: "r-new",
    sign: "Positive",
    text: "<p>Свежий отзыв</p>",
    createdUtc: "2026-07-26T16:00:00",
    author: { username: "Vasya", role: "Player" },
  },
  {
    id: "r-old",
    sign: "Neutral",
    text: "<p>Старый отзыв</p>",
    createdUtc: "2026-07-25T16:00:00",
    author: { username: "Petya", role: "Player" },
  },
] as unknown as PostReview[];

const post = {
  id: "p-1",
  createdUtc: "2026-07-26T15:00:00",
  gameText: "<p>Текст поста</p>",
  reviewCount: 2,
  rating: 1,
  author: { username: "Author", role: "Player" },
  room: { id: "room-guid", roomNumber: 2, game: { publicId: "abcde" } },
} as unknown as Post;

/** The same post as the room page answers with: no `room`, that IS the page. */
const roomPost = { ...post, room: undefined } as unknown as Post;

const ROUTES = [
  { path: "/", name: "home", component: { template: "<div />" } },
  // The author's name inside the post and inside every review is a link.
  {
    path: "/users/:username",
    name: "profile",
    component: { template: "<div />" },
  },
  {
    path: "/game/:id/rooms/:num",
    name: "game-room",
    component: { template: "<div />" },
  },
];

/** Mounts the post as it stands on `location`, with its reviews answerable. */
async function render(location: string, subject: Post = post) {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: ROUTES,
  });
  await router.push(location);
  await router.isReady();

  const wrapper = mount(GamePost, {
    props: { post: subject, number: 7 },
    global: { plugins: [createPinia(), router] },
  });
  await flushPromises();
  return wrapper;
}

const reviewsOf = (wrapper: Awaited<ReturnType<typeof render>>) =>
  wrapper.findAllComponents(PostReviewItem);

describe("GamePost review permalinks", () => {
  beforeEach(() => {
    localStorage.clear();
    vi.spyOn(gameApi, "getPostReviews").mockResolvedValue({
      data: { resources: REVIEWS },
      error: undefined,
    } as never);
  });

  afterEach(() => {
    // Since vitest 4, respying an already-spied method returns the same spy
    // with its call history intact, so without a restore the "not called"
    // assertion below would read the previous tests' calls.
    vi.restoreAllMocks();
  });

  it("points every review at the room, not at the page the post is drawn on", async () => {
    const wrapper = await render("/");

    await wrapper.get(".rating-value").trigger("click");
    await flushPromises();

    // Read off the rendered href rather than the prop: the route object and
    // the address it resolves to are the same claim only if the router agrees.
    expect(
      wrapper.findAll(".review-anchor").map((a) => a.attributes("href")),
    ).toEqual([
      "/game/abcde/rooms/2?review=r-new#post-p-1",
      "/game/abcde/rooms/2?review=r-old#post-p-1",
    ]);
  });

  it("keeps the page of the room in the address when drawn on that page", async () => {
    // On the room page `post.room` is redundant and not sent, so the address
    // is built from the current route. Its query carries the page number, and
    // a link that dropped it would point at page one of the room.
    const wrapper = await render("/game/abcde/rooms/2?number=3", roomPost);

    await wrapper.get(".rating-value").trigger("click");
    await flushPromises();

    expect(wrapper.get(".review-anchor").attributes("href")).toBe(
      "/game/abcde/rooms/2?number=3&review=r-new#post-p-1",
    );
  });

  it("opens the reviews on arrival and marks the one the address names", async () => {
    const wrapper = await render("/game/abcde/rooms/2?review=r-old#post-p-1");

    expect(wrapper.get(".reviews-collapse").classes()).toContain("expanded");
    expect(reviewsOf(wrapper).map((item) => item.props("highlight"))).toEqual([
      false,
      true,
    ]);
  });

  it("leaves the posts the address does not name collapsed", async () => {
    // Every post of the room reads the same query; the anchor is what tells
    // them apart, so a link into a neighbour must not open this one.
    const wrapper = await render("/game/abcde/rooms/2?review=r-old#post-p-9");

    expect(wrapper.get(".reviews-collapse").classes()).not.toContain(
      "expanded",
    );
    expect(gameApi.getPostReviews).not.toHaveBeenCalled();
  });
});

/**
 * A published roll used to render as "dundefined: undefined +7 = NaN": the
 * client type claimed `{ dice, result }`, a shape the API has never sent. What
 * it does send is how many dice of how many edges fell, every die that fell,
 * and the bonus; the total is arithmetic over those, not a field.
 */
describe("GamePost dice rolls", () => {
  const withRolls = (rolls: unknown[]) =>
    ({ ...post, diceRolls: rolls }) as unknown as Post;

  it("names the throw and sums it, for a single die", async () => {
    const wrapper = await render(
      "/",
      withRolls([
        {
          rolls: 1,
          edges: 20,
          bonus: 7,
          results: [{ value: 18 }],
          comment: "Восприятие",
        },
      ]),
    );

    const line = wrapper.find(".dice-roll").text();
    expect(line).toContain("1d20: 18 +7");
    expect(line).toContain("= 25");
    expect(line).not.toContain("undefined");
    expect(line).not.toContain("NaN");
  });

  it("shows every die of a multi-dice throw", async () => {
    const wrapper = await render(
      "/",
      withRolls([
        {
          rolls: 3,
          edges: 6,
          bonus: 0,
          results: [4, 2, 6].map((value) => ({ value })),
        },
      ]),
    );

    const line = wrapper.find(".dice-roll").text();
    expect(line).toContain("3d6: 4 2 6");
    expect(line).toContain("= 12");
  });
});

/**
 * An attachment that is a picture used to be a file name and a byte count, and
 * nothing else: to let anyone see the map they had attached, an author had to
 * copy its address back into an image tag by hand.
 *
 * What decides that it is drawn is the type the server read off the file
 * itself, and what keeps the text below it still while it loads is the measured
 * pair — the same one a picture standing in the post's own text is given.
 */
describe("GamePost attachments", () => {
  const MAP_ID = "571c0beb-1890-9ba6-2170-4bb531bc51f6";

  const attachment = (over: Partial<PostAttachment> = {}): PostAttachment => ({
    id: MAP_ID,
    fileName: "карта-подземелья.jpg",
    contentType: "image/jpeg",
    sizeBytes: 6707,
    width: 125,
    height: 138,
    url: `/v1/uploads/${MAP_ID}/content`,
    createdUtc: "2026-07-26T15:00:00",
    ...over,
  });

  const carrying = (...files: PostAttachment[]) =>
    ({ ...post, attachments: files }) as unknown as Post;

  it("draws an attachment that is a picture, in its measured box", async () => {
    const wrapper = await render("/", carrying(attachment()));

    const image = wrapper.get(".attachment-image");
    // The content endpoint, which decides the right on every request — not an
    // address out of the bucket.
    expect(image.attributes("src")).toContain(`/v1/uploads/${MAP_ID}/content`);
    expect(image.attributes("alt")).toBe("карта-подземелья.jpg");
    expect([image.attributes("width"), image.attributes("height")]).toEqual([
      "125",
      "138",
    ]);
    // The link to the file stays: it is what names the file and opens it.
    expect(wrapper.get(".attachment-name").text()).toBe("карта-подземелья.jpg");
  });

  it("leaves a file that is not a picture as its name and size", async () => {
    const wrapper = await render(
      "/",
      carrying(
        attachment({
          fileName: "правила.pdf",
          contentType: "application/pdf",
          width: null,
          height: null,
        }),
      ),
    );

    expect(wrapper.find(".attachment-image").exists()).toBe(false);
    expect(wrapper.get(".attachment-name").text()).toBe("правила.pdf");
    expect(wrapper.get(".attachment-size").text()).toBe("6.5 КБ");
  });

  it("draws a picture stored before its size was recorded, declaring no box", async () => {
    // A guessed pair would be a wrong box rather than a reserved one, so the
    // picture goes without and behaves as any undeclared image does.
    const wrapper = await render(
      "/",
      carrying(attachment({ width: null, height: null })),
    );

    const image = wrapper.get(".attachment-image");
    expect(image.attributes("width")).toBeUndefined();
    expect(image.attributes("height")).toBeUndefined();
    expect(wrapper.get(".attachment-name").text()).toBe("карта-подземелья.jpg");
  });
});

/**
 * The editor is seeded from a rendering the page never shows: the author's own,
 * the only one that carries [private] in a form the editor can round-trip.
 *
 * That answer is an envelope - the post sits under `resource` - and the seeding
 * read the field off the top level, where it was undefined every time. The
 * editor then silently kept what it had been given a moment earlier, the
 * Display rendering, and handed server-built HTML to an editor that takes
 * BBCode. Saving that published the [private] block to the whole room as
 * ordinary text, which is the one harm the fetch exists to prevent.
 *
 * So the assertion is on both halves at once: the seed has to be the source of
 * the private block, and it has to not be the display rendering. A shallow read
 * fails both.
 */
describe("GamePost seeds the editor from the author's own rendering", () => {
  // What the page shows: the block is a div with a class and nothing the
  // reverse conversion can read, and the addressees are a separate line of
  // prose. Nothing here can produce a [private] tag.
  const DISPLAY_HTML =
    '<strong>Жирно</strong> <div class="private-message">Секрет</div>' +
    '<div class="private-message-header">Получатели: Чак</div> хвост';

  // What the author's own rendering carries: the marked form, which is where
  // [private="Чак"] comes back from.
  const AUTHOR_EDIT_HTML =
    '<strong data-bb-tag="b">Жирно</strong> <div class="private-message" ' +
    'data-bb-tag="private" data-bb-addressees="Чак">Секрет</div> хвост';

  const ownPost = {
    ...post,
    gameText: DISPLAY_HTML,
    // Inside the fifteen minutes, so the button is drawn for its author.
    createdUtc: new Date().toISOString(),
  } as unknown as Post;

  beforeEach(() => {
    localStorage.clear();
    vi.spyOn(gameApi, "getPostReviews").mockResolvedValue({
      data: { resources: [] },
      error: undefined,
    } as never);
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  /** Mounts the post as its own author sees it on the room page. */
  async function renderOwn() {
    const router = createRouter({
      history: createMemoryHistory(),
      routes: ROUTES,
    });
    await router.push("/game/abcde/rooms/2");
    await router.isReady();

    const pinia = createPinia();
    setActivePinia(pinia);
    useAuthStore().user = { username: "Author", role: "Player" } as never;

    const wrapper = mount(GamePost, {
      props: { post: ownPost, number: 7, editable: true },
      global: { plugins: [pinia, router] },
    });
    await flushPromises();
    return wrapper;
  }

  const editButton = (wrapper: Awaited<ReturnType<typeof renderOwn>>) =>
    wrapper
      .findAll("button")
      .find((b) => b.text() === "Редактировать") as ReturnType<
      typeof wrapper.get
    >;

  const seededText = (wrapper: Awaited<ReturnType<typeof renderOwn>>) =>
    wrapper.get(".post-edit .editor-stub").attributes("value") ?? "";

  it("takes the seed out of the envelope, not off its lid", async () => {
    vi.spyOn(gameApi, "getPostForEdit").mockResolvedValue({
      data: { resource: { ...ownPost, gameText: AUTHOR_EDIT_HTML } },
      error: undefined,
    } as never);

    const wrapper = await renderOwn();
    await editButton(wrapper).trigger("click");
    await flushPromises();

    const seed = seededText(wrapper);

    // Came from the author's rendering: only the marked form yields the tag.
    expect(seed).toContain("[private=Чак]");
    // And is not the display rendering, whose addressee line is prose.
    expect(seed).not.toContain("Получатели");
    expect(seed).not.toContain('class="private-message"');
  });

  it("keeps the displayed text when the answer carries no post", async () => {
    // An empty envelope is not a reason to refuse to open the editor: the
    // author is better off editing without the private block than staring at
    // a button that does nothing.
    vi.spyOn(gameApi, "getPostForEdit").mockResolvedValue({
      data: { resource: null },
      error: undefined,
    } as never);

    const wrapper = await renderOwn();
    await editButton(wrapper).trigger("click");
    await flushPromises();

    expect(wrapper.find(".post-edit").exists()).toBe(true);
    expect(seededText(wrapper)).toBe(DISPLAY_HTML);
  });
});
