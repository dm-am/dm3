/**
 * @vitest-environment jsdom
 */

/**
 * A removed account signs what it left as "Аккаунт удален", and the signature
 * leads to the profile of that name, on every surface that names an author, a
 * master or an owner.
 *
 * The server hides a removed account and keeps its posts, comments, messages,
 * reviews, games and blogs, so on the wire the person over such a piece is
 * null. The forum turned that null into the link from the start. The comment
 * and the topic card printed the same words as plain text, the game post
 * called the author "Системное", and the chat message read the name off the
 * null and did not draw at all. Each surface decides it on its own, so each is
 * mounted here with the person missing, and the link is read off the rendered
 * anchor through a real router, the way a reader would follow it.
 *
 * The address changed once: the signature used to lead to /removed-account, a
 * page that only explained the name. It leads to the profile of the name now,
 * which gathers everything removed accounts left, and the old address is a
 * redirect there (router.spec.ts). The test router here has no route for the
 * old address, so a signature left pointing at it resolves to the catch-all
 * and fails the comparison instead of passing by accident.
 */
import { describe, it, expect, afterEach, vi } from "vitest";
import { enableAutoUnmount, flushPromises, mount } from "@vue/test-utils";
import { createPinia, setActivePinia, type Pinia } from "pinia";
import type { Component } from "vue";
import { createMemoryHistory, createRouter } from "vue-router";
import { CommentItem } from "@/features/comment";
import { TopicCard } from "@/features/topic";
import { GamePost } from "@/widgets/game-post";
import { ChatMessage } from "@/widgets/chat-message";
import { GamesDataTable } from "@/widgets/games-table";
import { BlogsDataTable } from "@/widgets/blogs-table";
import {
  GameReviewCard,
  PostReviewItem,
  gameApi,
  useGameDetailsStore,
  useGamesStore,
  type Game,
  type Post,
  type PostReview,
} from "@/entities/game";
import { useBlogDetailsStore, useBlogsStore, type Blog } from "@/entities/blog";
import { Api } from "@/shared/api";
import GameDetails from "@/pages/game/GameDetails.vue";
import BlogDetails from "@/pages/blog/BlogDetails.vue";
import ProfileGamesTable from "@/pages/profile/ProfileGamesTable.vue";
import ProfileBlogsTable from "@/pages/profile/ProfileBlogsTable.vue";
import type { Comment } from "@/shared/api/models/common/comment";
import type { GameReview } from "@/shared/api/models/game/reviews";
import type { MessageWithContinuation } from "@/shared/lib/utils/chat";

// The editors behind the edit modes are tiptap; nothing here edits.
vi.mock("@/shared/ui/BBCodeEditor", () => ({
  BBCodeEditor: { template: "<textarea />", props: ["modelValue"] },
}));

// The post sizes its meta column and the chat truncates its body with a
// ResizeObserver, which jsdom has not. Geometry is not what is read here.
vi.stubGlobal(
  "ResizeObserver",
  class {
    observe() {}
    unobserve() {}
    disconnect() {}
  },
);

enableAutoUnmount(afterEach);
afterEach(() => {
  vi.restoreAllMocks();
});

const page = { template: "<div />" };

/**
 * The names the surfaces below link to, each with the parameters it takes.
 * The profile is the one that matters; the rest only have to resolve.
 */
const ROUTES: Array<[string, string]> = [
  ["profile", "/users/:username"],
  ["game", "/game/:id"],
  ["game-character", "/game/:id/characters/:characterId"],
  ["game-room", "/game/:id/rooms/:num"],
  ["game-reviews", "/game/:id/reviews"],
  ["game-post-reviews", "/game/:id/post-reviews"],
  ["game-first-unread-post", "/game/:id/first-unread-post"],
  ["game-first-unread-comment", "/game/:id/first-unread-comment"],
  ["games", "/games"],
  ["blog", "/blogs/:id"],
  ["blogs", "/blogs"],
];

const makeRouter = async () => {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: "/", name: "home", component: page },
      ...ROUTES.map(([name, path]) => ({ name, path, component: page })),
      { path: "/:rest(.*)*", name: "anything", component: page },
    ],
  });
  await router.push("/");
  await router.isReady();
  return router;
};

const mountWith = async (
  component: Component,
  props: Record<string, unknown> = {},
  prepare?: (pinia: Pinia) => void,
) => {
  const pinia = createPinia();
  setActivePinia(pinia);
  prepare?.(pinia);
  const wrapper = mount(component, {
    props,
    global: {
      plugins: [pinia, await makeRouter()],
      // The filter bars of the catalogue tables read tags and people off the
      // server, and the paging bar watches the viewport; neither is read here.
      stubs: { GamesFilter: true, BlogsFilter: true, Paging: true },
    },
  });
  await flushPromises();
  return wrapper;
};

/** The profile the signature leads to, as the address bar shows it. */
const PROFILE = "/users/Аккаунт удален";

/** Where an anchor leads, decoded the way the address bar shows it. */
const target = (href: string | undefined) => decodeURI(href ?? "");

/** Every anchor that carries the signature, as "text -> address". */
const signatures = (wrapper: Awaited<ReturnType<typeof mountWith>>) =>
  wrapper
    .findAll("a")
    .filter((a) => a.text().includes("Аккаунт удален"))
    .map((a) => `${a.text().trim()} -> ${target(a.attributes("href"))}`);

const SIGNED = `Аккаунт удален -> ${PROFILE}`;

const done = () => Promise.resolve({ error: null });

const comment = {
  id: "c-1",
  text: "<p>Комментарий</p>",
  createdUtc: "2026-09-24T10:00:00Z",
  modifiedUtc: null,
  author: null,
  likes: [],
  isRemoved: false,
} as unknown as Comment;

const commentProps = (compact: boolean) => ({
  comment,
  compact,
  fetchEditSource: done,
  submitEdit: done,
  submitDelete: done,
  submitLike: done,
  submitUnlike: done,
});

const post = (over: Partial<Post> = {}) =>
  ({
    id: "p-1",
    createdUtc: "2026-09-24T10:00:00Z",
    gameText: "<p>Текст поста</p>",
    reviewCount: 0,
    rating: 0,
    author: null,
    room: { id: "room-guid", roomNumber: 1, game: { publicId: "abcde" } },
    ...over,
  }) as unknown as Post;

const message = {
  id: "m-1",
  createdUtc: "2026-09-24T10:00:00Z",
  modifiedUtc: null,
  author: null,
  text: "Сообщение",
  isRemoved: false,
  isContinuation: false,
  likes: [],
  edits: [],
} as unknown as MessageWithContinuation;

/** A game of the catalogue whose master's account was removed. */
const game = {
  id: "g-1",
  publicId: "abcde",
  title: "Покинутая игра",
  status: "Active",
  master: null,
  assistants: [],
  participation: [],
  subscribersCount: 0,
  recruitment: { isOpen: false, pcCount: 0 },
  unreadPostsCount: 0,
  unreadCommentsCount: 0,
  gameReviewsCount: 0,
  postReviewsCount: 0,
  tags: [],
  readers: [],
  createdUtc: "2026-09-01T10:00:00Z",
} as unknown as Game;

/** A blog of the catalogue whose owner's account was removed. */
const blog = {
  id: "b-1",
  publicId: "fghij",
  title: "Покинутый блог",
  status: "Active",
  author: null,
  assistants: [],
  createdUtc: "2026-09-01T10:00:00Z",
  publicationCount: 0,
  subscribersCount: 0,
  rubrics: [],
} as unknown as Blog;

const envelope = <T>(resources: T[]) => ({
  resources,
  paging: { total: resources.length, pages: 1, current: 1, size: 20, skip: 0 },
});

describe("the signature of a removed account", () => {
  it("leads to the profile of the name from a comment, in both layouts", async () => {
    expect(
      signatures(await mountWith(CommentItem, commentProps(false))),
    ).toEqual([SIGNED]);
    expect(
      signatures(await mountWith(CommentItem, commentProps(true))),
    ).toEqual([SIGNED]);
  });

  // Without the column the text of such a comment stood left of its
  // neighbours. The chat keeps its column with the default silhouette, and so
  // does the comment.
  it("keeps the avatar column of a comment, with the default silhouette", async () => {
    const full = await mountWith(CommentItem, commentProps(false));

    const avatar = full.get(".avatar-link");
    expect(target(avatar.attributes("href"))).toBe(PROFILE);
    expect(avatar.find("svg.default-avatar").exists()).toBe(true);
    expect(avatar.get("svg").attributes("aria-label")).toBe("Аккаунт удален");

    const compact = await mountWith(CommentItem, commentProps(true));
    expect(compact.find(".avatar-link").exists()).toBe(false);
  });

  it("leads to the profile of the name from a topic card", async () => {
    const wrapper = await mountWith(TopicCard, {
      title: "Правила",
      contentHtml: "<p>Текст</p>",
      author: null,
      createdUtc: "2026-09-24T10:00:00Z",
      commentsCount: 0,
      commentsTo: "/forum",
    });

    expect(signatures(wrapper)).toEqual([SIGNED]);
  });

  it("stands in a game post where the author would, and not as a system post", async () => {
    const wrapper = await mountWith(GamePost, { post: post(), number: 1 });

    expect(signatures(wrapper)).toEqual([SIGNED]);
    expect(wrapper.text()).not.toContain("Системное");
  });

  it("stands under the character and under the game role of a game post", async () => {
    const asCharacter = await mountWith(GamePost, {
      post: post({
        character: { id: "ch-1", name: "Ведьмак" },
      } as unknown as Partial<Post>),
      number: 1,
    });
    expect(asCharacter.text()).toContain("Ведьмак");
    expect(signatures(asCharacter)).toEqual([SIGNED]);

    const asMaster = await mountWith(GamePost, {
      post: post({ authorGameRole: "Мастер" } as unknown as Partial<Post>),
      number: 1,
    });
    expect(asMaster.text()).toContain("Мастер");
    expect(signatures(asMaster)).toEqual([SIGNED]);
  });

  // A post of the "Лучший ход" spotlight carries its game, and the game its
  // master, and the edits their editor: all three may be nobody now.
  it("draws a post whose game master and editor were removed", async () => {
    const wrapper = await mountWith(GamePost, {
      post: post({
        author: { id: "u-1", username: "Живой", role: "RegularUser" },
        edits: [
          { id: "e-1", modifiedUtc: "2026-09-24T11:00:00Z", editor: null },
        ],
        room: {
          id: "room-guid",
          roomNumber: 1,
          title: "Комната",
          game: { ...game, master: null },
        },
      } as unknown as Partial<Post>),
      number: 1,
      showNavigation: true,
    });

    expect(wrapper.text()).toContain("Живой");
    expect(wrapper.text()).toContain("Покинутая игра");
  });

  it("signs a chat message in both layouts, the avatar leading the same way", async () => {
    const full = await mountWith(ChatMessage, { message, compact: false });
    expect(full.get(".msg-author").text()).toBe("Аккаунт удален");
    expect(target(full.get(".msg-author").attributes("href"))).toBe(PROFILE);
    expect(target(full.get(".msg-avatar-link").attributes("href"))).toBe(
      PROFILE,
    );

    const compact = await mountWith(ChatMessage, { message, compact: true });
    expect(signatures(compact)).toEqual([SIGNED]);
  });

  it("keeps a chat message of a moderator's reveal signed the same way", async () => {
    const wrapper = await mountWith(ChatMessage, {
      message: { ...message, isRemoved: true },
      isDeletedExpanded: true,
    });

    expect(target(wrapper.get(".msg-author").attributes("href"))).toBe(PROFILE);
  });

  it("signs a review of a game", async () => {
    const review = {
      id: "r-1",
      gameId: "abcde",
      gameTitle: "Игра",
      author: null,
      text: "<p>Рецензия</p>",
      createdUtc: "2026-09-24T10:00:00Z",
    } as unknown as GameReview;

    expect(signatures(await mountWith(GameReviewCard, { review }))).toEqual([
      SIGNED,
    ]);
  });

  it("signs a rating of a post", async () => {
    const review = {
      id: "pr-1",
      postId: "p-1",
      gameId: "g-1",
      author: null,
      postAuthor: null,
      text: "<p>Оценка</p>",
      sign: "Positive",
      createdUtc: "2026-09-24T10:00:00Z",
    } as unknown as PostReview;

    expect(signatures(await mountWith(PostReviewItem, { review }))).toEqual([
      SIGNED,
    ]);
  });
});

describe("a removed master or owner", () => {
  it("is named in the facts of a game", async () => {
    const wrapper = await mountWith(GameDetails, {}, () => {
      useGameDetailsStore().game = game;
    });

    const row = wrapper
      .findAll(".info-table tr")
      .find((tr) => tr.find("th").text() === "Мастер");
    expect(row, "the facts of the game have no master row").toBeTruthy();
    expect(signatures(row!.find("td") as never)).toEqual([SIGNED]);
  });

  it("is named in the facts of a blog", async () => {
    const wrapper = await mountWith(BlogDetails, {}, () => {
      useBlogDetailsStore().blog = blog;
    });

    const row = wrapper
      .findAll(".info-table tr")
      .find((tr) => tr.find("th").text() === "Автор");
    expect(row, "the facts of the blog have no author row").toBeTruthy();
    expect(signatures(row!.find("td") as never)).toEqual([SIGNED]);
  });

  it("is named in the catalogue of games", async () => {
    const wrapper = await mountWith(GamesDataTable, {}, () => {
      const store = useGamesStore();
      vi.spyOn(store, "searchGames").mockResolvedValue();
      vi.spyOn(store, "fetchTags").mockResolvedValue(undefined as never);
      store.searchResult = envelope([game]) as never;
    });

    expect(wrapper.text()).toContain("Покинутая игра");
    expect(signatures(wrapper)).toEqual([SIGNED]);
  });

  it("is named in the catalogue of blogs", async () => {
    const wrapper = await mountWith(BlogsDataTable, {}, () => {
      const store = useBlogsStore();
      vi.spyOn(store, "searchBlogs").mockResolvedValue();
      store.searchResult = envelope([blog]) as never;
    });

    expect(wrapper.text()).toContain("Покинутый блог");
    expect(signatures(wrapper)).toEqual([SIGNED]);
  });

  it("is named in the games of a profile", async () => {
    vi.spyOn(gameApi, "searchGames").mockResolvedValue({
      data: envelope([game]),
      error: null,
    } as never);

    const wrapper = await mountWith(ProfileGamesTable, {
      username: "Аккаунт удален",
    });

    expect(wrapper.text()).toContain("Покинутая игра");
    expect(signatures(wrapper)).toEqual([SIGNED]);
  });

  it("is named in the blogs of a profile", async () => {
    vi.spyOn(Api, "get").mockResolvedValue({
      data: envelope([blog]),
      error: null,
    } as never);

    const wrapper = await mountWith(ProfileBlogsTable, {
      username: "Аккаунт удален",
    });

    expect(wrapper.text()).toContain("Покинутый блог");
    expect(signatures(wrapper)).toEqual([SIGNED]);
  });
});
