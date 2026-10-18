/**
 * @vitest-environment jsdom
 */

/**
 * The mentor's panel at the top of the left column: who sees it, what it
 * lists, the star that says a game waits for the mentor's turn, and what it
 * forgets when the mentor leaves. The server is answered by URL, so the spec
 * holds whichever client the panel asks through.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { flushPromises, mount } from "@vue/test-utils";
import { nextTick } from "vue";
import { createPinia, setActivePinia } from "pinia";
import { Api } from "@/shared/api";
import { useGamesStore, GameParticipation, GameStatus } from "@/entities/game";
import { useBlogsStore } from "@/entities/blog";
import { useAuthStore } from "@/entities/user";
import { UserRole } from "@/shared/api/models/common";
import MentorPanel from "./MentorPanel.vue";

const ME = "mentor";

const stubs = {
  "router-link": { template: "<a><slot /></a>", props: ["to"] },
  Tooltip: {
    template: '<span class="tooltip" :data-text="text"><slot /></span>',
    props: ["text"],
  },
};

const game = (
  id: string,
  status: GameStatus,
  participation: GameParticipation[],
) => ({
  id,
  publicId: id,
  title: `Игра ${id}`,
  status,
  participation,
  unreadPostsCount: 0,
  unreadCommentsCount: 0,
});

const blog = (
  id: string,
  createdUtc: string,
  over: Record<string, unknown> = {},
) => ({
  id,
  publicId: id,
  title: `Блог ${id}`,
  status: "Active",
  createdUtc,
  author: { username: "someone" },
  assistants: [],
  ...over,
});

const pendency = (characterName: string, waitingFor: string, done = false) => ({
  characterName,
  waitingFor: { username: waitingFor },
  fulfilledUtc: done ? "2026-01-01T00:00:00Z" : null,
});

/** What the server says: the rooms of each game and the mentor of each blog. */
interface Server {
  rooms?: Record<string, ReturnType<typeof pendency>[][]>;
  blogMentors?: Record<string, string>;
}

let get: ReturnType<typeof vi.spyOn>;

function serve(server: Server) {
  get = vi.spyOn(Api, "get").mockImplementation((async (url: string) => {
    const rooms = /^games\/([^/]+)\/rooms$/.exec(url);
    if (rooms) {
      const list = server.rooms?.[rooms[1]] ?? [];
      return {
        data: { resources: list.map((pendencies) => ({ pendencies })) },
        error: null,
      };
    }
    const users = /^blogs\/([^/]+)\/users$/.exec(url);
    if (users) {
      const mentor = server.blogMentors?.[users[1]];
      return {
        data: {
          resources: mentor
            ? [{ role: "Mentor", user: { username: mentor } }]
            : [],
        },
        error: null,
      };
    }
    throw new Error(`unexpected request ${url}`);
  }) as never);
}

function signIn(role: UserRole | undefined, username = ME) {
  useAuthStore().user = { username, role } as never;
}

async function render(
  games: ReturnType<typeof game>[],
  blogs: ReturnType<typeof blog>[],
) {
  const gamesStore = useGamesStore();
  const blogsStore = useBlogsStore();
  const fetchGames = vi
    .spyOn(gamesStore, "fetchParticipatingGames")
    .mockResolvedValue(undefined as never);
  const fetchBlogs = vi
    .spyOn(blogsStore, "fetchParticipatingBlogs")
    .mockResolvedValue(undefined as never);
  gamesStore.participatingGames = games as never;
  blogsStore.participatingBlogs = blogs as never;
  const wrapper = mount(MentorPanel, { global: { stubs } });
  await flushPromises();
  return { wrapper, fetchGames, fetchBlogs };
}

const sections = (wrapper: Awaited<ReturnType<typeof render>>["wrapper"]) =>
  wrapper.findAll(".section-title").map((title) => title.text());

beforeEach(() => {
  setActivePinia(createPinia());
});

afterEach(() => {
  vi.restoreAllMocks();
});

describe("MentorPanel: who sees it", () => {
  it.each([UserRole.RegularUser, undefined])(
    "stays hidden from a reader of role %s, whatever they curate, and asks for nothing",
    async (role) => {
      signIn(role);
      serve({ blogMentors: { b1: ME } });

      const { wrapper, fetchGames } = await render(
        [game("g1", GameStatus.Active, [GameParticipation.Moderator])],
        [blog("b1", "2026-01-01")],
      );

      expect(wrapper.html()).toBe("<!--v-if-->");
      expect(fetchGames).not.toHaveBeenCalled();
    },
  );

  it.each([
    UserRole.Mentor,
    UserRole.Moderator,
    UserRole.SeniorModerator,
    UserRole.Admin,
  ])(
    "shows itself to a %s who curates a game, and loads the lists",
    async (role) => {
      signIn(role);
      serve({});

      const { wrapper, fetchGames, fetchBlogs } = await render(
        [game("g1", GameStatus.Active, [GameParticipation.Moderator])],
        [],
      );

      expect(wrapper.text()).toContain("Наставничество");
      expect(fetchGames).toHaveBeenCalled();
      expect(fetchBlogs).toHaveBeenCalled();
    },
  );

  it("stays hidden from a mentor who curates nothing", async () => {
    signIn(UserRole.Mentor);
    serve({ blogMentors: { b1: "another" } });

    const { wrapper } = await render(
      [
        game("g1", GameStatus.Active, [GameParticipation.Player]),
        game("g2", GameStatus.Closed, [GameParticipation.Moderator]),
      ],
      [blog("b1", "2026-01-01")],
    );

    expect(wrapper.html()).toBe("<!--v-if-->");
  });
});

describe("MentorPanel: what it lists", () => {
  it("lists the games it curates that are forming or going, in the order given", async () => {
    signIn(UserRole.Mentor);
    serve({});

    const { wrapper } = await render(
      [
        game("g1", GameStatus.Active, [GameParticipation.Moderator]),
        game("g2", GameStatus.Closed, [GameParticipation.Moderator]),
        game("g3", GameStatus.Draft, [
          GameParticipation.Player,
          GameParticipation.Moderator,
        ]),
        game("g4", GameStatus.Active, [GameParticipation.Reader]),
      ],
      [],
    );

    expect(sections(wrapper)).toEqual(["Курируемые игры"]);
    expect(wrapper.findAll("li.link").map((row) => row.text())).toEqual([
      expect.stringContaining("Игра g1"),
      expect.stringContaining("Игра g3"),
    ]);
  });

  it("asks each blog for its mentor and lists the ones it is, newest first", async () => {
    signIn(UserRole.Mentor);
    serve({ blogMentors: { b1: ME, b2: ME, b3: "another" } });

    const { wrapper } = await render(
      [],
      [
        blog("b1", "2026-01-01"),
        blog("b2", "2026-03-01"),
        blog("b3", "2026-02-01"),
        blog("own", "2026-04-01", { author: { username: ME } }),
        blog("helped", "2026-04-02", { assistants: [{ username: ME }] }),
        blog("closed", "2026-04-03", { status: "Closed" }),
      ],
    );

    expect(sections(wrapper)).toEqual(["Курируемые блоги"]);
    expect(wrapper.text()).toMatch(/Блог b2[\s\S]*Блог b1/);
    expect(wrapper.text()).not.toContain("Блог b3");
    const asked = get.mock.calls.map(([url, params]: unknown[]) => [
      url,
      params,
    ]);
    expect(asked).toEqual([
      ["blogs/b1/users", { role: "mentor" }],
      ["blogs/b2/users", { role: "mentor" }],
      ["blogs/b3/users", { role: "mentor" }],
    ]);
  });
});

describe("MentorPanel: the star of a game waiting for the mentor", () => {
  it("names in its hint the characters that wait for the mentor's turn", async () => {
    signIn(UserRole.Mentor);
    serve({
      rooms: {
        g1: [
          [pendency("Гоблин", ME), pendency("Эльф", "player")],
          [pendency("Орк", ME), pendency("Тролль", ME, true)],
        ],
        g2: [[pendency("Эльф", "player")]],
      },
    });

    const { wrapper } = await render(
      [
        game("g1", GameStatus.Active, [GameParticipation.Moderator]),
        game("g2", GameStatus.Active, [GameParticipation.Moderator]),
      ],
      [],
    );

    const [first, second] = wrapper.findAll("li.link");
    expect(first.find(".star").text()).toBe("*");
    const starHint = first
      .findAll(".tooltip")
      .find((tooltip) => tooltip.find(".star").exists());
    expect(starHint?.attributes("data-text")).toBe(
      "Вашего хода ждут: Гоблин, Орк",
    );
    expect(second.find(".star").exists()).toBe(false);
  });
});

describe("MentorPanel: leaving and coming back", () => {
  it("forgets the stars and the blogs when the mentor leaves", async () => {
    signIn(UserRole.Mentor);
    serve({
      rooms: { g1: [[pendency("Гоблин", ME)]] },
      blogMentors: { b1: ME },
    });
    const { wrapper } = await render(
      [game("g1", GameStatus.Active, [GameParticipation.Moderator])],
      [blog("b1", "2026-01-01")],
    );
    expect(wrapper.find(".star").exists()).toBe(true);

    useAuthStore().user = null;
    await flushPromises();
    signIn(UserRole.Mentor, "another");
    await flushPromises();

    expect(wrapper.find(".star").exists()).toBe(false);
    expect(wrapper.text()).not.toContain("Блог b1");
  });

  it("loads the lists for a mentor who signs in without a reload", async () => {
    serve({});
    const { fetchGames, fetchBlogs } = await render([], []);
    expect(fetchGames).not.toHaveBeenCalled();

    signIn(UserRole.Mentor);
    await nextTick();

    expect(fetchGames).toHaveBeenCalledOnce();
    expect(fetchBlogs).toHaveBeenCalledOnce();
  });
});
