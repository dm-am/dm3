/**
 * @vitest-environment jsdom
 */

/**
 * The two premoderation queues draw one table.
 *
 * "Премодерируемые блоги" and "Премодерируемые игры" were one file written
 * twice: same filter, same two-bucket merge, same sort, same four columns,
 * different nouns. They now share PremoderatedQueue, and what a merge like
 * that has to prove is that nothing on screen moved — a rendered page is not
 * covered by a test that only calls the code behind it.
 *
 * So the two pages are mounted on the same row and compared to each other:
 * the games markup, with the words the pages legitimately disagree on
 * translated into the blogs ones, must be the blogs markup character for
 * character. The list below is therefore the whole difference between the two
 * screens — an unlisted one fails here, which is the point.
 *
 * The exact assertions after it are the details a substitution table cannot
 * see: the column keys DataTable turns into cell classes, where a title leads
 * (a game by its public id), and the spaces around the empty-queue sentence,
 * which an interpolation written on its own line silently drops.
 */
import { describe, it, expect, vi, beforeEach } from "vitest";
import { mount, flushPromises } from "@vue/test-utils";
import { createMemoryHistory, createRouter } from "vue-router";
import type { Component } from "vue";
import PageTitle from "@/shared/ui/Layout/PageTitle.vue";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import FormField from "@/shared/ui/Form/FormField.vue";
import ModerationPremoderatedBlogs from "./ModerationPremoderatedBlogs.vue";
import ModerationPremoderatedGames from "./ModerationPremoderatedGames.vue";

const getPremoderatedBlogs = vi.hoisted(() => vi.fn());
const getPremoderatedGames = vi.hoisted(() => vi.fn());

// Partial: the page reads PREMODERATION_STATUS_LABELS out of lib/labels.ts,
// which reads its enums from this same barrel.
vi.mock("@/entities/moderation", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/entities/moderation")>()),
  moderationApi: { getPremoderatedBlogs, getPremoderatedGames },
}));

// The gate has its own spec (roleGate.spec.ts); here the viewer is a moderator.
vi.mock("./lib/useRoleGate", () => ({
  useRoleGate: () => ({ hasAccess: true, deniedText: "" }),
}));

const router = createRouter({
  history: createMemoryHistory(),
  routes: [
    { path: "/blogs/:id", name: "blog", component: { template: "<div />" } },
    { path: "/games/:id", name: "game", component: { template: "<div />" } },
    {
      path: "/u/:username",
      name: "profile",
      component: { template: "<div />" },
    },
  ],
});

/** One row, spelled the same for both queues so only the words differ. */
const ROW = {
  id: "queue-row",
  publicId: "",
  title: "Название",
  author: { username: "Владеющий" },
  master: { username: "Владеющий" },
  createdUtc: "2026-05-01",
};

async function render(page: Component) {
  const wrapper = mount(page, {
    global: {
      plugins: [router],
      components: {
        PageTitle,
        "page-title": PageTitle,
        SecondaryText,
        "secondary-text": SecondaryText,
        FormField,
      },
    },
  });
  await flushPromises();
  return wrapper;
}

/**
 * Scope ids are compile-time tokens (the styles moved with the markup they
 * gate), and FormField gives every instance of a label its own generated id.
 * Neither is what this file is about.
 */
function markup(html: string): string {
  return html
    .replace(/ data-v-[0-9a-f]+=""/g, "")
    .replace(/-[a-z0-9]{5,10}-label/g, "-label");
}

/**
 * Every word the two queues are allowed to disagree on, games spelling first.
 * Ordered: the longer sentences before the words they contain.
 */
const DIFFERENCES: [string, string][] = [
  ["Премодерируемые игры", "Премодерируемые блоги"],
  ["Премодерируемых игр пока нет", "Премодерируемых блогов пока нет"],
  [
    "Не удалось загрузить премодерируемые игры",
    "Не удалось загрузить премодерируемые блоги",
  ],
  ["premoderated-games", "premoderated-blogs"],
  ["col-master", "col-author"],
  [">Мастер<", ">Владелец<"],
  [">Игра<", ">Блог<"],
  [">Создана<", ">Создан<"],
  ["/games/", "/blogs/"],
];

function asBlogs(html: string): string {
  return DIFFERENCES.reduce(
    (text, [game, blog]) => text.split(game).join(blog),
    html,
  );
}

beforeEach(() => {
  getPremoderatedBlogs.mockReset();
  getPremoderatedGames.mockReset();
});

/**
 * Both buckets answer, so the merge and the sort run as they do in life: the
 * row of the second one is the older, and has to come out on top.
 */
function answerWith(row: object | null) {
  const reply = async (status: string) => ({
    data: {
      resources: row
        ? [
            {
              ...row,
              id: `${(row as { id: string }).id}-${status}`,
              createdUtc:
                status === "AwaitingApproval" ? "2026-05-01" : "2026-04-01",
            },
          ]
        : [],
      paging: null,
    },
    error: null,
  });
  getPremoderatedBlogs.mockImplementation(reply);
  getPremoderatedGames.mockImplementation(reply);
}

describe("premoderation queues", () => {
  it("draws the same table for blogs and for games", async () => {
    answerWith(ROW);

    const blogs = await render(ModerationPremoderatedBlogs);
    const games = await render(ModerationPremoderatedGames);

    // The comparison is worth nothing over two pages that rendered nothing.
    expect(blogs.findAll("tr.table-row").length).toBe(2);
    expect(games.findAll("tr.table-row").length).toBe(2);

    expect(asBlogs(markup(games.html()))).toBe(markup(blogs.html()));
  });

  it("keeps the empty sentence and its spaces", async () => {
    answerWith(null);

    const blogs = await render(ModerationPremoderatedBlogs);
    const games = await render(ModerationPremoderatedGames);

    expect(blogs.find(".secondary-text").html()).toContain(
      "> Премодерируемых блогов пока нет <",
    );
    expect(games.find(".secondary-text").html()).toContain(
      "> Премодерируемых игр пока нет <",
    );
  });

  it("says which queue failed to load", async () => {
    const refused = { data: null, error: { status: 500 } };
    getPremoderatedBlogs.mockResolvedValue(refused);
    getPremoderatedGames.mockResolvedValue(refused);

    const blogs = await render(ModerationPremoderatedBlogs);
    const games = await render(ModerationPremoderatedGames);

    expect(blogs.find(".error-state").text()).toContain(
      "Не удалось загрузить премодерируемые блоги",
    );
    expect(games.find(".error-state").text()).toContain(
      "Не удалось загрузить премодерируемые игры",
    );
  });

  it("gives each queue its own columns and links", async () => {
    answerWith({ ...ROW, publicId: "the-public-id" });

    const blogs = await render(ModerationPremoderatedBlogs);
    const games = await render(ModerationPremoderatedGames);

    // The owner column keeps its own key: DataTable spells it into the cell
    // class, and the cell is filled through a slot named after it.
    expect(blogs.find("td.col-author").text()).toBe("Владеющий");
    expect(games.find("td.col-master").text()).toBe("Владеющий");

    // Oldest first across the two merged buckets: the AwaitingEdits row is
    // the older one here, so it is the row on top.
    expect(blogs.find("td.col-title a").attributes("href")).toBe(
      "/blogs/queue-row-AwaitingEdits",
    );
    // A game is addressed by its public id when it has one; a blog has none.
    expect(games.find("td.col-title a").attributes("href")).toBe(
      "/games/the-public-id",
    );
  });

  /**
   * The comparison above is mutual, so a change made to the shared queue shows
   * up on both sides at once and passes it. These are the parts of the drawn
   * table that only a fixed expectation holds still.
   */
  it("keeps the table it drew before the two pages were merged", async () => {
    answerWith(ROW);

    const blogs = await render(ModerationPremoderatedBlogs);

    expect(blogs.find("table").attributes("aria-label")).toBe(
      "Премодерируемые блоги",
    );
    expect(
      blogs.findAll("thead th").map((th) => th.classes().join(" ")),
    ).toEqual([
      "col col-title align-left",
      "col col-author align-left",
      "col col-created hide-mobile align-left",
      "col col-status align-left",
    ]);
    expect(
      blogs.findAll("thead th").map((th) => th.attributes("style")),
    ).toEqual([undefined, "width: 20%;", "width: 20%;", "width: 20%;"]);
    expect(
      blogs
        .findAll("tr.table-row")[0]
        .findAll("td")
        .map((td) => td.text()),
    ).toEqual([
      "Название",
      "Владеющий",
      "01.04.2026 в 00:00",
      "Требует правок",
    ]);

    // The filter above the table: one select, both buckets and the "all" that
    // merges them.
    expect(blogs.find(".filters select").attributes("id")).toBe(
      "premoderation-status",
    );
    expect(blogs.findAll(".filters option").map((o) => o.text())).toEqual([
      "Все статусы",
      "Ожидает проверки",
      "Требует правок",
    ]);
  });
});
