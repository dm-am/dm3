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
import { enableAutoUnmount, mount, flushPromises } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { createMemoryHistory, createRouter } from "vue-router";
import { nextTick } from "vue";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { gameApi, PostReviewItem } from "@/entities/game";
import type { Post, PostAttachment, PostReview } from "@/entities/game";
import { useAuthStore, useUiStore } from "@/shared/stores";
import { useToast } from "@/shared/lib/composables/useToast";
import {
  clearRegistry,
  expandAll,
  hasAny,
} from "@/shared/lib/composables/useExpandableRegistry";
import { componentCss, styleRules } from "@/shared/lib/testing/styles";
import { TruncatedContent } from "@/shared/ui/TruncatedContent";
import GamePost from "./GamePost.vue";

const HERE = dirname(fileURLToPath(import.meta.url));

// Every post leaves the page with its test. A post left mounted keeps its
// reviews in the registry of "Развернуть все", and the next post that
// registers there wakes it over a document already emptied.
enableAutoUnmount(afterEach);

// The composer behind the inline edit is tiptap; nothing here composes text.
vi.mock("@/shared/ui/BBCodeEditor", () => ({
  BBCodeEditor: {
    name: "BBCodeEditor",
    template: "<textarea class='editor-stub' :value='modelValue' />",
    props: ["modelValue", "context", "checkContext"],
  },
}));

// The post sizes its meta column with a ResizeObserver, which jsdom has not.
// Geometry is not what this spec reads.
vi.stubGlobal(
  "ResizeObserver",
  class {
    observe() {}
    unobserve() {}
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

/**
 * A post naming a game that says the viewer is on its blacklist, in the shape
 * the server used to send (viewerBlacklisted of the game reference). The list
 * closes the discussion and the application, so nothing on a post may read it.
 */
const onAListedGame = (subject: Post) =>
  ({
    ...subject,
    room: {
      ...post.room,
      game: { id: "game-guid", publicId: "abcde", viewerBlacklisted: true },
    },
  }) as unknown as Post;

/**
 * The post as the server answers it to a viewer: whether they may rate it
 * (post.viewer.canReview), and the game it was written in.
 */
const answered = (subject: Post, canReview = true, gameId = "game-guid") =>
  ({ ...subject, gameId, viewer: { canReview } }) as unknown as Post;

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
 * The rolls of a post stand on a grey strip between the game text and the
 * metagame text in the old site's words since 4 October 2026: the heading
 * "Результаты бросков:", then one numbered line per roll; the words of a line
 * are diceLine.spec.ts. What the post adds is the strip's place and the
 * heading, and every roll there is: the old site never folds the rolls of a
 * post, however many.
 */
describe("GamePost dice rolls", () => {
  /** The line as a reader sees it: the joins that hold it together taken out. */
  const seen = (text: string) =>
    text
      .split(String.fromCharCode(0x2060))
      .join("")
      .replace(/\s+/g, " ")
      .trim();

  const rollOf = (value: number, comment?: string) => ({
    rolls: 1,
    edges: 20,
    bonus: 0,
    results: [{ value }],
    comment,
  });

  const withRolls = (count: number) =>
    ({
      ...post,
      metagameText: "<p>Метаигровой текст</p>",
      diceRolls: Array.from({ length: count }, (_, i) =>
        rollOf(i + 1, `Бросок ${i + 1}`),
      ),
    }) as unknown as Post;

  const rows = (wrapper: Awaited<ReturnType<typeof render>>) =>
    wrapper.findAll(".dice-rolls > div").map((row) => seen(row.text()));

  beforeEach(() => {
    localStorage.clear();
    clearRegistry();
  });

  afterEach(() => {
    vi.restoreAllMocks();
    clearRegistry();
  });

  it("writes every roll on a line of its own, between the game text and the metagame text", async () => {
    const wrapper = await render("/", withRolls(1) as unknown as Post);

    expect(rows(wrapper)).toEqual([
      "Результаты бросков:",
      '1) 1D20: 1 - "Бросок 1"',
    ]);
    const order = [...wrapper.get(".truncated-content").element.children].map(
      (block) => block.className.split(" ")[0],
    );
    expect(order).toEqual(["game-text", "dice-rolls", "metagame-text"]);
    wrapper.unmount();
  });

  it("shows every roll of a long list, in order, with nothing to press", async () => {
    const wrapper = await render("/", withRolls(10));

    expect(rows(wrapper)).toEqual([
      "Результаты бросков:",
      ...Array.from(
        { length: 10 },
        (_, i) => `${i + 1}) 1D20: ${i + 1} - "Бросок ${i + 1}"`,
      ),
    ]);
    expect(wrapper.get(".dice-rolls").find("button").exists()).toBe(false);
    expect(wrapper.get(".dice-rolls").text()).not.toMatch(/\.\.\.|Показать/);
    wrapper.unmount();
  });

  it("leaves the page's Развернуть все nothing to open in the rolls", async () => {
    const wrapper = await render("/", {
      ...withRolls(8),
      reviewCount: 0,
    } as unknown as Post);

    // The reviews are the only fold of a post with nothing rated and no file.
    expect(hasAny.value).toBe(false);
    wrapper.unmount();
  });

  it("draws the strip the old site drew: flat grey, from the text to the column's edge, 7px in, 22px from its neighbours", () => {
    const rules = styleRules(componentCss(resolve(HERE, "GamePost.vue")));
    const first = (selector: string) =>
      rules.find((rule) => rule.selector === selector)?.declarations;

    expect(first(".dice-rolls")).toMatchObject({
      margin: "22px 0 22px 10px",
      padding: "2px 7px",
      "background-color": "var(--bg-element-accent)",
      "font-size": "16px",
      "line-height": "1.25",
      "text-align": "justify",
      hyphens: "manual",
    });
    expect(first(".dice-rolls")).not.toHaveProperty("border");
    expect(first(".dice-rolls")).not.toHaveProperty("border-radius");
  });
});

/**
 * The files of a post are not drawn in it any more. A grey paper clip in the
 * card's top right corner, the mirror of the number in the bottom right one,
 * says there are files, with their count when there are several, and opens a
 * strip over the text: one file per line, its name, a link to the file, and
 * its size. No "Скопировать ссылку": the name is the link to copy. One file
 * opens the same strip, so the clip does one thing. The strip is content, so
 * it opens with the page's "Развернуть все".
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

  const three = () =>
    carrying(
      attachment(),
      attachment({
        id: "f-2",
        fileName: "план-таверны.png",
        sizeBytes: 219340,
        url: "/v1/uploads/f-2/content",
      }),
      attachment({
        id: "f-3",
        fileName: "правила.pdf",
        contentType: "application/pdf",
        sizeBytes: 49410,
        url: "/v1/uploads/f-3/content",
      }),
    );

  const isOpen = (wrapper: Awaited<ReturnType<typeof render>>) =>
    wrapper.get(".attachments-fold").classes().includes("open");

  beforeEach(() => {
    localStorage.clear();
    clearRegistry();
    // "Развернуть все" opens the reviews of the post as well.
    vi.spyOn(gameApi, "getPostReviews").mockResolvedValue({
      data: { resources: [] },
      error: undefined,
    } as never);
  });

  afterEach(() => {
    vi.restoreAllMocks();
    vi.useRealTimers();
    clearRegistry();
  });

  it("draws no file in the post, only a closed clip in the corner", async () => {
    const wrapper = await render("/", carrying(attachment()));

    expect(wrapper.find("img").exists()).toBe(false);
    const clip = wrapper.get(".post-card > .attachments-toggle");
    expect(clip.attributes("aria-expanded")).toBe("false");
    expect(clip.attributes("aria-label")).toBe("Вложение");
    expect(clip.find(".attachments-count").exists()).toBe(false);
    expect(isOpen(wrapper)).toBe(false);
    wrapper.unmount();
  });

  it("draws nothing for a post without files", async () => {
    const wrapper = await render("/");

    expect(wrapper.find(".attachments-toggle").exists()).toBe(false);
    expect(wrapper.find(".attachments-fold").exists()).toBe(false);
    wrapper.unmount();
  });

  it("counts the files beside the clip when there are several", async () => {
    const wrapper = await render("/", three());

    const clip = wrapper.get(".attachments-toggle");
    expect(clip.get(".attachments-count").text()).toBe("3");
    expect(clip.attributes("aria-label")).toBe("Вложения: 3");
    wrapper.unmount();
  });

  it("opens the strip over the text, even for one file", async () => {
    const wrapper = await render("/", carrying(attachment()));

    await wrapper.get(".attachments-toggle").trigger("click");

    expect(isOpen(wrapper)).toBe(true);
    expect(wrapper.get(".attachments-toggle").attributes("aria-expanded")).toBe(
      "true",
    );
    // Over the text: the strip comes before the text in the column.
    const body = [...wrapper.get(".post-body").element.children].map(
      (block) => block.classList[0],
    );
    expect(body.indexOf("expand-fold")).toBeLessThan(
      body.indexOf("truncated-content-wrapper"),
    );
    const name = wrapper.get(".attachments-strip .attachment-name");
    expect(name.text()).toBe("карта-подземелья.jpg");
    expect(name.attributes("href")).toContain(`/v1/uploads/${MAP_ID}/content`);
    expect(name.attributes("target")).toBe("_blank");
    expect(wrapper.get(".attachment-row").text().replace(/\s+/g, " ")).toBe(
      "карта-подземелья.jpg 6,5 КБ",
    );
    wrapper.unmount();
  });

  it("lists every file on a line of its own", async () => {
    const wrapper = await render("/", three());

    await wrapper.get(".attachments-toggle").trigger("click");

    expect(
      wrapper
        .findAll(".attachment-row")
        .map((row) => row.text().replace(/\s+/g, " ")),
    ).toEqual([
      "карта-подземелья.jpg 6,5 КБ",
      "план-таверны.png 214,2 КБ",
      "правила.pdf 48,3 КБ",
    ]);
    wrapper.unmount();
  });

  it("offers nothing that copies the address of a file", async () => {
    const write = vi.fn().mockResolvedValue(undefined);
    Object.assign(navigator, { clipboard: { writeText: write } });
    const wrapper = await render("/", three());
    await wrapper.get(".attachments-toggle").trigger("click");

    const strip = wrapper.get(".attachments-strip");
    expect(strip.find("button").exists()).toBe(false);
    expect(strip.text()).not.toMatch(/Скопировать|Скопировано/);
    for (const name of strip.findAll(".attachment-name"))
      await name.trigger("click");
    expect(write).not.toHaveBeenCalled();
    wrapper.unmount();
  });

  it("opens with the page's Развернуть все", async () => {
    const wrapper = await render("/", carrying(attachment()));

    expandAll();
    await flushPromises();

    expect(isOpen(wrapper)).toBe(true);
    wrapper.unmount();
  });

  it("keeps the clip grey and the strip the rolls' strip", () => {
    const rules = styleRules(componentCss(resolve(HERE, "GamePost.vue")));
    const first = (selector: string) =>
      rules.find((rule) => rule.selector === selector)?.declarations;

    expect(first(".attachments-toggle")).toMatchObject({
      position: "absolute",
      color: "var(--text-muted)",
    });
    expect(first(".attachments-strip")).toMatchObject({
      margin: "16px 0 11px 10px",
      padding: "2px 7px",
      "background-color": "var(--bg-element-accent)",
    });
  });
});

/**
 * The edit line stands under the text in the right column: "Отредактировано
 * ДД.ММ.ГГГГ в ЧЧ:ММ | Редактировать | Удалить", 22px under the last block of
 * the post. The row across the whole card that used to carry the controls and
 * "(отредактировано)" is gone; the number stands alone in its corner.
 */
describe("GamePost edit line", () => {
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

  /** A post of the viewer's own, inside the edit window, on the room page. */
  async function renderAs(
    viewer: string | null,
    over: Record<string, unknown> = {},
    editable = true,
    role = "Player",
  ) {
    const router = createRouter({
      history: createMemoryHistory(),
      routes: ROUTES,
    });
    await router.push("/game/abcde/rooms/2");
    await router.isReady();

    const pinia = createPinia();
    setActivePinia(pinia);
    if (viewer) {
      useAuthStore().user = { username: viewer, role } as never;
    }

    const wrapper = mount(GamePost, {
      props: {
        post: {
          ...roomPost,
          createdUtc: new Date().toISOString(),
          ...over,
        } as unknown as Post,
        number: 7,
        editable,
      },
      global: { plugins: [pinia, router] },
    });
    await flushPromises();
    return wrapper;
  }

  const EDITED = { edits: [{ id: "e-1", modifiedUtc: "2026-07-26T15:30:00" }] };

  const lineOf = (wrapper: Awaited<ReturnType<typeof renderAs>>) =>
    wrapper.find(".post-edit-line");

  const copied = (element: Element) =>
    (element.textContent ?? "").replace(/\s+/g, " ").trim();

  it("says when the post was edited, then what the viewer may do", async () => {
    const wrapper = await renderAs("Author", EDITED);

    expect(copied(lineOf(wrapper).element)).toBe(
      "Отредактировано 26.07.2026 в 15:30 | Редактировать | Удалить",
    );
    wrapper.unmount();
  });

  it("stands under the text in the right column, and leaves the number alone in the footer", async () => {
    const wrapper = await renderAs("Author", EDITED);

    const body = wrapper.get(".post-body").element;
    expect(lineOf(wrapper).element.parentElement).toBe(body);
    expect(body.lastElementChild).toBe(lineOf(wrapper).element);
    expect(copied(wrapper.get(".post-footer").element)).toBe("7");
    expect(wrapper.find(".post-footer button").exists()).toBe(false);
    wrapper.unmount();
  });

  it("offers the actions alone on a post nobody edited", async () => {
    const wrapper = await renderAs("Author");

    expect(copied(lineOf(wrapper).element)).toBe("Редактировать | Удалить");
    wrapper.unmount();
  });

  // The game's list does not close the rooms: the author's own post keeps
  // its edit, whatever list the game keeps.
  it("offers the author on the game's list the edit of his fresh post", async () => {
    const wrapper = await renderAs(
      "Author",
      { room: onAListedGame(roomPost).room },
      true,
    );

    expect(copied(lineOf(wrapper).element)).toBe("Редактировать | Удалить");
    wrapper.unmount();
  });

  it("says only that it was edited where the viewer may do nothing", async () => {
    const wrapper = await renderAs("Reader", EDITED, false);

    expect(copied(lineOf(wrapper).element)).toBe(
      "Отредактировано 26.07.2026 в 15:30",
    );
    wrapper.unmount();
  });

  it("draws no line when there is nothing to say", async () => {
    const wrapper = await renderAs("Reader", {}, false);

    expect(lineOf(wrapper).exists()).toBe(false);
    wrapper.unmount();
  });

  it("dates a fresh edit by the edit itself", async () => {
    vi.spyOn(gameApi, "getPostForEdit").mockResolvedValue({
      data: { resource: null },
      error: undefined,
    } as never);
    vi.spyOn(gameApi, "updatePost").mockResolvedValue({
      data: {
        resource: {
          ...roomPost,
          gameText: "<p>Новый текст</p>",
          edits: [{ id: "e-2", modifiedUtc: "2026-07-27T09:05:00" }],
        },
      },
      error: undefined,
    } as never);
    const wrapper = await renderAs("Author");

    const edit = wrapper
      .findAll(".post-edit-line .caption-action")
      .find((b) => b.text() === "Редактировать")!;
    await edit.trigger("click");
    await flushPromises();
    await wrapper.get(".edit-btn.save").trigger("click");
    await flushPromises();

    expect(copied(lineOf(wrapper).element)).toBe(
      "Отредактировано 27.07.2026 в 09:05 | Редактировать | Удалить",
    );
    wrapper.unmount();
  });

  it("names the text the server refused by the label over its editor", async () => {
    const toast = useToast();
    for (const standing of toast.toasts.value) toast.dismiss(standing.id);
    vi.spyOn(gameApi, "getPostForEdit").mockResolvedValue({
      data: { resource: null },
      error: undefined,
    } as never);
    vi.spyOn(gameApi, "updatePost").mockResolvedValue({
      data: undefined,
      error: {
        status: 400,
        title: "",
        detail: "",
        type: "",
        traceId: "",
        errors: { GameText: ["Invalid"] },
      },
    } as never);
    const wrapper = await renderAs("Author");

    const edit = wrapper
      .findAll(".post-edit-line .caption-action")
      .find((b) => b.text() === "Редактировать")!;
    await edit.trigger("click");
    await flushPromises();
    await wrapper.get(".edit-btn.save").trigger("click");
    await flushPromises();

    // Under the buttons of the edit, which stays open; no plate.
    expect(wrapper.get(".edit-actions + .action-result").text()).toBe(
      "Игровой текст: некорректное значение",
    );
    expect(toast.toasts.value.map((t) => t.message)).toEqual([]);
    for (const standing of toast.toasts.value) toast.dismiss(standing.id);
    wrapper.unmount();
  });

  // DM2's div.p.comment: 10px in at the left edge of the text, 11px under the
  // last block's own 11px, and the column's 11px under it.
  it("is the 12px grey caption line, 11px above and below", () => {
    const rules = styleRules(componentCss(resolve(HERE, "GamePost.vue")));
    expect(
      rules.find((rule) => rule.selector === ".post-body")?.declarations,
    ).toMatchObject({ padding: "11px 0" });
    expect(
      rules.find((rule) => rule.selector === ".post-edit-line")?.declarations,
    ).toMatchObject({
      margin: "11px 5px 0 10px",
      // The line stands in the block as text: the block takes the caption's
      // size, so the line is 15px high and not the post text's.
      "font-size": "12px",
      "line-height": "15px",
    });
  });

  /**
   * Every part at every width (owner, 12 October 2026: no "Еще", nothing put
   * away): where the text column is narrower than the line, it wraps between
   * its parts, as the caption of a topic does, and the date of the edit goes
   * with its time.
   */
  it("keeps every part, the date of the edit with its time in one part", async () => {
    const wrapper = await renderAs("Author", EDITED);
    const line = lineOf(wrapper);

    expect(line.find(".scroll-row").exists()).toBe(false);
    expect(line.find(".more-menu").exists()).toBe(false);
    expect(line.find("[hidden]").exists()).toBe(false);
    const queued = [line.element, ...line.element.querySelectorAll("*")]
      .flatMap((element) => [...element.attributes])
      .filter((attribute) => attribute.name.startsWith("data-fit"))
      .map((attribute) => attribute.name);
    expect(queued).toEqual([]);

    const edited = line.findAll(".caption-line__part")[0];
    expect(copied(edited.element)).toBe("Отредактировано 26.07.2026 в 15:30");
    // One text, not the day and a span of its time apart.
    expect(edited.element.firstElementChild?.children.length).toBe(0);
    wrapper.unmount();
  });
});

/**
 * The sum of the reviews in the left column opens them under the card. For a
 * reader who may rate the post, the "+0" of a post nobody has rated opens the
 * form of its first review; it used to be plain text, and the only way to the
 * form was a post someone else had rated first.
 */
describe("GamePost review sum", () => {
  const unrated = { ...roomPost, reviewCount: 0, rating: 0 } as unknown as Post;

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

  async function renderFor(viewer: string | null, subject: Post) {
    const router = createRouter({
      history: createMemoryHistory(),
      routes: ROUTES,
    });
    await router.push("/game/abcde/rooms/2");
    await router.isReady();

    const pinia = createPinia();
    setActivePinia(pinia);
    if (viewer) {
      useAuthStore().user = { username: viewer, role: "Player" } as never;
    }
    const wrapper = mount(GamePost, {
      props: { post: subject, number: 7 },
      global: { plugins: [pinia, router] },
    });
    await flushPromises();
    return wrapper;
  }

  it("opens the form of the first review from the +0 of a reader who may rate", async () => {
    const wrapper = await renderFor("Reader", answered(unrated));

    const sum = wrapper.get("button.rating-value");
    expect(sum.text()).toBe("+0");
    await sum.trigger("click");
    await flushPromises();

    expect(wrapper.get(".reviews-collapse").classes()).toContain("expanded");
    expect(wrapper.find(".review-form .review-input").exists()).toBe(true);
    wrapper.unmount();
  });

  // The author is told no by the server. A guest gets nothing even where the
  // post still carries a yes given to somebody before.
  it("leaves the +0 plain text for a guest and for the author", async () => {
    for (const viewer of [null, "Author"]) {
      const wrapper = await renderFor(viewer, answered(unrated, !viewer));
      expect(wrapper.find("button.rating-value").exists()).toBe(false);
      expect(wrapper.get("span.rating-value").text()).toBe("+0");
      wrapper.unmount();
    }
  });

  // A rating is feedback, and the game's blacklist does not close it: the
  // "+0" opens the form of the first one for a reader on the list too.
  it("opens the form of the first review for a reader on the game's blacklist", async () => {
    const wrapper = await renderFor("Reader", answered(onAListedGame(unrated)));

    await wrapper.get("button.rating-value").trigger("click");
    await flushPromises();

    expect(wrapper.find(".review-form .review-input").exists()).toBe(true);
    wrapper.unmount();
  });

  it("keeps a negative sum a toggle in its colour", async () => {
    const wrapper = await renderFor(null, {
      ...roomPost,
      reviewCount: 2,
      rating: -2,
    } as unknown as Post);

    const sum = wrapper.get("button.rating-value");
    expect(sum.text()).toBe("-2");
    expect(sum.classes()).toContain("negative");
    await sum.trigger("click");
    await flushPromises();
    expect(wrapper.get(".reviews-collapse").classes()).toContain("expanded");
    wrapper.unmount();
  });

  // The pointer is answered the way a caption action answers it, with an
  // underline alone. The grey "+0" used to turn the blue of a link, and the
  // signed sums a lighter shade of their own colour.
  it("underlines the sum on hover and keeps its colour in every sign", () => {
    const rules = styleRules(componentCss(resolve(HERE, "GamePost.vue")));
    const hovers = rules.filter(
      (rule) =>
        rule.selector.includes("rating-value") &&
        rule.selector.includes(":hover"),
    );

    expect(hovers.map((rule) => rule.selector)).toEqual([
      "button.rating-value:hover",
    ]);
    for (const { declarations } of hovers) {
      expect(declarations).not.toHaveProperty("color");
    }
    expect(hovers[0].declarations).toMatchObject({
      "text-decoration": "underline",
    });
    const colourOf = (sign: string) =>
      rules.find((rule) => rule.selector === `.rating-value.${sign}`)
        ?.declarations.color;
    expect(colourOf("neutral")).toBe("var(--text-muted)");
    expect(colourOf("negative")).toBe("var(--accent-red)");
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
    expect(seed).toContain('[private="Чак"]');
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

  // The metagame text is saved on the comment surface, which refuses a
  // hidden block. Its editor keeps the post's buttons and preview and checks
  // the markup as that surface, so the refusal is named before the save.
  it("checks the metagame text as the surface it is saved on", async () => {
    vi.spyOn(gameApi, "getPostForEdit").mockResolvedValue({
      data: { resource: ownPost },
      error: undefined,
    } as never);

    const wrapper = await renderOwn();
    await editButton(wrapper).trigger("click");
    await flushPromises();

    expect(
      wrapper
        .findAllComponents({ name: "BBCodeEditor" })
        .map((editor) => [
          editor.props("context"),
          editor.props("checkContext"),
        ]),
    ).toEqual([
      ["post", undefined],
      ["post", "common"],
    ]);
  });
});

/**
 * A refused review answers in the form, not in the corner.
 *
 * The form stays on screen after a refusal with the draft still in it, and
 * that is where the reader is looking. The toast this used to raise stood in
 * the corner, away from the field, and being an error toast it never faded,
 * so it also had to be dismissed by hand. The sentence is the server's own,
 * read with describeFailure the way the other form strips read theirs.
 *
 * The statuses the response interceptor announces on its own - 401, 403, 429,
 * 5xx and a dead connection - get no line here. The old branch skipped 403
 * and 429 only, and raised its own toast on every 5xx over the interceptor's:
 * one failure told twice, in two sentences.
 */
describe("GamePost answers a refused review in the form", () => {
  const toast = useToast();
  const saidOutLoud = () => toast.toasts.value.map((t) => t.message);

  beforeEach(() => {
    localStorage.clear();
    vi.spyOn(gameApi, "getPostReviews").mockResolvedValue({
      data: { resources: REVIEWS },
      error: undefined,
    } as never);
  });

  afterEach(() => {
    vi.restoreAllMocks();
    // Toasts are module state and an error toast never fades: one left over
    // would be read by the next test's assertion on silence.
    for (const standing of toast.toasts.value) toast.dismiss(standing.id);
  });

  /** A signed-in reader who has not rated the post, with the reviews open. */
  async function renderReader(subject: Post = answered(roomPost)) {
    const router = createRouter({
      history: createMemoryHistory(),
      routes: ROUTES,
    });
    await router.push("/game/abcde/rooms/2");
    await router.isReady();

    const pinia = createPinia();
    setActivePinia(pinia);
    useAuthStore().user = { username: "Reader", role: "Player" } as never;

    const wrapper = mount(GamePost, {
      props: { post: subject, number: 7 },
      global: { plugins: [pinia, router] },
    });
    await flushPromises();
    await wrapper.get(".rating-value").trigger("click");
    await flushPromises();
    return wrapper;
  }

  type Reader = Awaited<ReturnType<typeof renderReader>>;

  const refuseWith = (error: Record<string, unknown>) =>
    vi.spyOn(gameApi, "createPostReview").mockResolvedValue({
      data: null,
      error,
    } as never);

  async function send(wrapper: Reader) {
    await wrapper.get(".review-input").setValue("Хороший ход");
    await wrapper.get(".submit-btn").trigger("click");
    await flushPromises();
  }

  const lineOf = (wrapper: Reader) => wrapper.find(".review-form .form-error");

  /**
   * A rating is feedback, and the game's blacklist does not close it. The
   * form used to give way to a sentence about the list; it is the form
   * everybody gets.
   */
  it("offers the form to a reader on the game's blacklist", async () => {
    const wrapper = await renderReader(answered(onAListedGame(roomPost)));

    expect(wrapper.find(".review-form .review-input").exists()).toBe(true);
    expect(wrapper.text()).not.toContain("черном списке");
    wrapper.unmount();
  });

  /**
   * The sign is a toggle of segments, as every mode of the site is chosen
   * ("Месяц | Год", "Ведущий | Игрок"): "+" is pressed when the form opens,
   * and the sign sent is the segment pressed last.
   */
  it("sends the sign pressed among the segments", async () => {
    const create = vi.spyOn(gameApi, "createPostReview").mockResolvedValue({
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
    // Nothing is taken off the screen: the draft stays and the button takes
    // the next press. Otherwise the answer to a refusal is retyping.
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

  // A 5xx the interceptor used to plate stands in the form now, in the
  // server's words, and the plate is taken down (refusalHere; the owner's
  // rule of 26 September 2026).
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

  // A dead connection is about the reader's network: the plate says it, and
  // the form keeps quiet.
  it("stays quiet about a request that never arrived", async () => {
    refuseWith({ status: 0, type: "", title: "", traceId: "" });
    const wrapper = await renderReader();

    await send(wrapper);

    expect(lineOf(wrapper).exists()).toBe(false);
  });
});

/**
 * The question before a delete waits for the answer and says a refusal in
 * itself. It used to close before the request went out, and a refusal came
 * back as a plate over a post that was still there, with nothing left on
 * screen to say which post the plate was about.
 */
describe("GamePost asks before it deletes", () => {
  const toast = useToast();

  beforeEach(() => {
    localStorage.clear();
    vi.spyOn(gameApi, "getPostReviews").mockResolvedValue({
      data: { resources: REVIEWS },
      error: undefined,
    } as never);
  });

  afterEach(() => {
    vi.restoreAllMocks();
    for (const standing of toast.toasts.value) toast.dismiss(standing.id);
    document.body.innerHTML = "";
  });

  /** The author of the post, on the room page, with the controls opted into. */
  async function renderAuthor() {
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
      props: { post: roomPost, number: 7, editable: true },
      global: { plugins: [pinia, router] },
    });
    await flushPromises();
    return wrapper;
  }

  const question = () => document.body.querySelector(".dialog-container");

  async function confirmDelete(
    wrapper: Awaited<ReturnType<typeof renderAuthor>>,
  ) {
    await wrapper
      .findAll(".post-edit-line .caption-action")
      .find((action) => action.text() === "Удалить")!
      .trigger("click");
    question()?.querySelector<HTMLButtonElement>(".dialog-btn-submit")?.click();
    await flushPromises();
  }

  it("keeps the question open with the refusal in it, and the post in place", async () => {
    const remove = vi.spyOn(gameApi, "deletePost").mockResolvedValue({
      data: null,
      error: { type: "", title: "", status: 404, traceId: "t" },
    } as never);
    const wrapper = await renderAuthor();

    await confirmDelete(wrapper);

    expect(remove).toHaveBeenCalledWith("p-1", {
      ownsRefusal: true,
      ownsRateLimit: true,
    });
    expect(
      question()?.querySelector(".dialog-refusal")?.textContent?.trim(),
    ).toBe("Не удалось удалить пост");
    expect(toast.toasts.value).toEqual([]);
    expect(wrapper.find(".post-deleted").exists()).toBe(false);
    expect(wrapper.emitted("deleted")).toBeUndefined();
    wrapper.unmount();
  });

  it("takes the post off and closes once the server has", async () => {
    vi.spyOn(gameApi, "deletePost").mockResolvedValue({
      data: null,
      error: null,
    } as never);
    const wrapper = await renderAuthor();

    await confirmDelete(wrapper);

    expect(wrapper.find(".post-deleted").exists()).toBe(true);
    expect(wrapper.emitted("deleted")).toEqual([["p-1"]]);
    expect(question()).toBeNull();
    wrapper.unmount();
  });
});

/**
 * The two blocks of text the author wrote are set as DM2 set them: flush on
 * both sides at every width, a phone included, for a phone shows the page of
 * the desktop, and hyphenated while "Перенос слов" of the panel is on, as it
 * is until the reader turns it off. The rolls between them are a line of
 * figures and take no breaks in words.
 */
describe("GamePost typesetting", () => {
  const withEverything = {
    ...post,
    metagameText: "<p>Метаигровой текст</p>",
    diceRolls: [
      { rolls: 1, edges: 20, bonus: 0, results: [{ value: 7 }], comment: "" },
    ],
  } as unknown as Post;

  const classesOf = (
    wrapper: Awaited<ReturnType<typeof render>>,
    selector: string,
  ) => wrapper.get(selector).classes();

  beforeEach(() => {
    localStorage.clear();
    clearRegistry();
  });

  afterEach(() => {
    clearRegistry();
  });

  it("hyphenates both blocks of text by default, and not the rolls", async () => {
    const wrapper = await render("/", withEverything);

    for (const block of [".game-text", ".metagame-text"]) {
      expect(classesOf(wrapper, block)).toContain("hyphenated");
    }
    expect(classesOf(wrapper, ".dice-rolls")).not.toContain("hyphenated");
    wrapper.unmount();
  });

  it("drops hyphenation the reader turned off on a previous visit", async () => {
    localStorage.setItem("dm_hyphenate_words", "off");
    const wrapper = await render("/", withEverything);

    for (const block of [".game-text", ".metagame-text"]) {
      expect(classesOf(wrapper, block)).not.toContain("hyphenated");
    }
    wrapper.unmount();
  });

  it("follows the panel without a reload", async () => {
    const wrapper = await render("/", withEverything);
    const ui = useUiStore();

    ui.setHyphenateWords(false);
    await flushPromises();
    expect(classesOf(wrapper, ".game-text")).not.toContain("hyphenated");

    ui.setHyphenateWords(true);
    await flushPromises();
    expect(classesOf(wrapper, ".metagame-text")).toContain("hyphenated");
    wrapper.unmount();
  });

  it("justifies at every width and hyphenates through the class alone", () => {
    const css = componentCss(resolve(HERE, "GamePost.vue"));
    const merged = (selector: string) =>
      Object.assign(
        {},
        ...styleRules(css)
          .filter((rule) => rule.selector.split(", ").includes(selector))
          .map((rule) => rule.declarations),
      ) as Record<string, string>;

    // No width sets the post apart: a phone shows the post of 1366.
    expect(css).not.toMatch(/@media/);
    for (const block of [".game-text", ".metagame-text"]) {
      expect(merged(block)).toMatchObject({
        "line-height": "1.25",
        "text-align": "justify",
      });
      expect(merged(`${block}.hyphenated`)).toMatchObject({
        hyphens: "auto",
        "-webkit-hyphens": "auto",
      });
      // The switch turned off has to take the property with it, so the bare
      // block may not carry it on its own.
      expect(merged(block)).not.toHaveProperty("hyphens");
    }

    // The words of a roll are never broken.
    expect(merged(".dice-rolls")).toMatchObject({ hyphens: "manual" });
  });
});

/**
 * The post stands in DM2's geometry (Session.aspx, div.commentItem): a left
 * column of 140 whose content, 11px in from the top and 5px from the sides,
 * leaves 128 for the name, the login, the portrait and the date; a spacer of
 * 10; the text 22px under the top of its column and 10px in from either edge;
 * the number in the very corner of the card. At every width: the column stands
 * beside the text on a phone as on 1366.
 */
describe("GamePost geometry", () => {
  const rules = () => styleRules(componentCss(resolve(HERE, "GamePost.vue")));
  const of = (selector: string) =>
    Object.assign(
      {},
      ...rules()
        .filter((rule) => rule.selector.split(", ").includes(selector))
        .map((rule) => rule.declarations),
    ) as Record<string, string>;

  it("keeps the column of 140 beside the text, its content 128 wide", () => {
    expect(of(".post-columns")).toMatchObject({ display: "flex", gap: "10px" });
    expect(of(".post-columns")).not.toHaveProperty("flex-direction");
    expect(of(".post-meta")).toMatchObject({
      width: "140px",
      "line-height": "1.25",
    });
    expect(of(".meta-inner")).toMatchObject({
      margin: "11px 5px",
      "padding-right": "2px",
    });
  });

  it("sets the text 22px under the top of its column and 10px in from its edges", () => {
    expect(of(".post-body")).toMatchObject({ padding: "11px 0" });
    expect(of(".game-text")).toMatchObject({ margin: "11px 10px" });
    expect(of(".metagame-text")).toMatchObject({ margin: "11px 10px" });
    expect(of(".post-edit")).toMatchObject({ margin: "11px 10px" });
  });

  it("puts the number in the very corner of the card", () => {
    expect(of(".post-card")).toMatchObject({ padding: "5px" });
    expect(of(".post-footer")).not.toHaveProperty("padding-right");
  });

  it("asks for the portrait at the width it is drawn", async () => {
    const wrapper = await render("/", {
      ...post,
      character: {
        id: "ch-1",
        name: "Джек",
        picture: { smallUrl: "/s.png", mediumUrl: "/m.png" },
      },
    } as unknown as Post);

    expect(wrapper.get("img.avatar").attributes("sizes")).toBe("128px");
    wrapper.unmount();
  });
});

/**
 * Who may not rate a post gets the reviews and nothing more. The list used to
 * end in "Нельзя оценивать собственный пост" for the author and "Вы уже
 * оценили этот пост" for whoever had, and a newbie read under his single "="
 * that only "=" was his: a refusal standing where the form would be, said to
 * somebody the site already knew it would refuse.
 */
describe("GamePost says no refusal under the reviews", () => {
  beforeEach(() => {
    localStorage.clear();
    vi.spyOn(gameApi, "getPostReviews").mockResolvedValue({
      data: { resources: REVIEWS },
      error: undefined,
    } as never);
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  async function openReviewsAs(
    viewer: Record<string, unknown>,
    canReview: boolean,
  ) {
    const router = createRouter({
      history: createMemoryHistory(),
      routes: ROUTES,
    });
    await router.push("/game/abcde/rooms/2");
    await router.isReady();

    const pinia = createPinia();
    setActivePinia(pinia);
    useAuthStore().user = { role: "Player", ...viewer } as never;

    const wrapper = mount(GamePost, {
      props: { post: answered(roomPost, canReview), number: 7 },
      global: { plugins: [pinia, router] },
    });
    await flushPromises();
    await wrapper.get(".rating-value").trigger("click");
    await flushPromises();
    return wrapper;
  }

  // The server says no to both, and the page says nothing in place of the form.
  it.each([
    ["the author of the post", "Author"],
    ["a reader who has rated it", "Vasya"],
  ])("gives %s the reviews alone", async (_, username) => {
    const wrapper = await openReviewsAs({ username }, false);

    expect(wrapper.findAll(".reviews-section > *")).toHaveLength(
      REVIEWS.length,
    );
    expect(wrapper.find(".review-form").exists()).toBe(false);
    expect(wrapper.find(".review-notice").exists()).toBe(false);
    expect(wrapper.text()).not.toContain("Нельзя оценивать");
    expect(wrapper.text()).not.toContain("уже оценили");
    wrapper.unmount();
  });

  // The single "=" is the whole answer: it shows the sign a newbie may give.
  it("offers a newbie the neutral sign without a word about the rest", async () => {
    const wrapper = await openReviewsAs(
      { username: "Reader", isNewbie: true },
      true,
    );

    expect(
      wrapper.findAll(".review-form .segment").map((s) => s.text()),
    ).toEqual(["="]);
    expect(wrapper.find(".review-form").text()).not.toContain("Новичкам");
    expect(wrapper.text()).not.toContain("100 постов");
    wrapper.unmount();
  });
});

/**
 * The rating is offered by the server's answer for the viewer
 * (post.viewer.canReview): not their own post, not rated by them, and no rating
 * of theirs in the game during the last three days. A reader who had rated in
 * the game the day before got the form, typed, and read "Слишком много
 * попыток" after "Отправить". What the answer cannot know is what happened on
 * the page since: a rating sent here, a viewer who signed in, a rating taken
 * back.
 */
describe("GamePost offers the rating by the server's answer", () => {
  const unrated = { ...roomPost, reviewCount: 0, rating: 0 } as unknown as Post;

  beforeEach(() => {
    localStorage.clear();
    vi.spyOn(gameApi, "getPostReviews").mockResolvedValue({
      // A copy: the list a post shows is the array it was answered with, and a
      // rating sent in one test would stand in the list of the next.
      data: { resources: [...REVIEWS] },
      error: undefined,
    } as never);
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  async function routerAtRoom() {
    const router = createRouter({
      history: createMemoryHistory(),
      routes: ROUTES,
    });
    await router.push("/game/abcde/rooms/2");
    await router.isReady();
    return router;
  }

  /** Posts on one page, as a room shows them, for `viewer`. */
  async function renderPage(viewer: string | null, posts: Post[]) {
    const router = await routerAtRoom();
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

  it("gives a reader the server refuses the sum alone, and no form under the reviews", async () => {
    const [bare] = await renderPage("Reader", [answered(unrated, false)]);
    expect(bare.find("button.rating-value").exists()).toBe(false);

    const [rated] = await renderPage("Reader", [answered(roomPost, false)]);
    await rated.get("button.rating-value").trigger("click");
    await flushPromises();
    expect(rated.findAll(".reviews-section > *")).toHaveLength(REVIEWS.length);
    expect(rated.find(".review-form").exists()).toBe(false);
  });

  // The rating closes its game to the rater for three days, and the other
  // posts of the game on the page came with a yes given before it.
  it("closes the other posts of the game on the page once a rating is sent", async () => {
    vi.spyOn(gameApi, "createPostReview").mockResolvedValue({
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

  // The server lists the newest rating first and numbers the list by that
  // order. The rating just sent went to the end of the list with the last
  // number, and a reload made it #1: one rating, two numbers.
  it("puts the rating just sent first, under the number the server gives it", async () => {
    vi.spyOn(gameApi, "createPostReview").mockResolvedValue({
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

  // The answer is for whoever held the session when the post was read: a
  // guest who signs in is asked about anew, and offered nothing before it.
  it("asks the server again for a viewer who signed in", async () => {
    let answer: (value: unknown) => void = () => undefined;
    const getPost = vi.spyOn(gameApi, "getPost").mockReturnValue(
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

  // A yes given to the viewer before is not a yes to the next one: until the
  // server answers for them, nothing is offered.
  it("offers the next viewer nothing until the server answers for them", async () => {
    vi.spyOn(gameApi, "getPost").mockReturnValue(
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

  // A rating taken back: whether the post may be rated again is the server's
  // to say, so it is asked, and its yes brings the form back.
  it("asks again when the viewer takes their rating back", async () => {
    const getPost = vi.spyOn(gameApi, "getPost").mockResolvedValue({
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

/**
 * How much of a long post shows before "показать полностью": as many lines as
 * stand beside the left column, its height less the line of the button, at
 * every width, the column standing beside the text on a phone as on 1366.
 *
 * jsdom lays nothing out: the column is answered 262 high (the name, the
 * player, the portrait, the date, the sum of reviews).
 */
describe("GamePost text up to the button", () => {
  const realHeight = Object.getOwnPropertyDescriptor(
    HTMLElement.prototype,
    "offsetHeight",
  );

  beforeEach(() => {
    Object.defineProperty(HTMLElement.prototype, "offsetHeight", {
      configurable: true,
      get(this: HTMLElement) {
        return this.classList.contains("post-meta") ? 262 : 0;
      },
    });
  });

  afterEach(() => {
    vi.restoreAllMocks();
    if (realHeight)
      Object.defineProperty(HTMLElement.prototype, "offsetHeight", realHeight);
  });

  async function budget(): Promise<number> {
    const router = createRouter({
      history: createMemoryHistory(),
      routes: ROUTES,
    });
    await router.push("/");
    await router.isReady();
    const wrapper = mount(GamePost, {
      props: { post, truncatable: true },
      global: { plugins: [createPinia(), router] },
    });
    await flushPromises();
    await nextTick();
    return wrapper.getComponent(TruncatedContent).props("maxHeight") as number;
  }

  it("takes the lines beside the column: the column less the button, in whole lines", async () => {
    // 262 less 22 for the button is 240, twelve lines of 20.
    expect(await budget()).toBe(240);
  });
});
