/**
 * @vitest-environment jsdom
 */

/**
 * The games catalogue table as a reader of the list meets it: the words of an
 * empty list, the rows that stay under a refusal, the column the address
 * sorts by, a tag that narrows the list it stands in, and the numbering of a
 * later page. The store is given its answer; the table is not asked how it
 * got it.
 */
import { describe, it, expect, vi, afterEach } from "vitest";
import { RouterLinkStub } from "@vue/test-utils";
import { useGamesStore } from "@/entities/game";
import { onListPage, type Page } from "@/shared/lib/testing/urlFilter";
import GamesDataTable from "./GamesDataTable.vue";

let page: Page | null = null;

afterEach(async () => {
  await page?.settled();
  page?.wrapper.unmount();
  page = null;
  vi.restoreAllMocks();
});

const game = (id: string, tagIds: number[] = []) => ({
  id,
  publicId: id,
  title: `Игра ${id}`,
  status: "Active",
  master: { username: "master" },
  assistants: [],
  tagIds,
  participation: [],
});

interface Answer {
  rows?: ReturnType<typeof game>[];
  skip?: number;
  error?: string | null;
}

async function tableOn(query: Record<string, string>, answer: Answer = {}) {
  let search: ReturnType<typeof vi.spyOn> | null = null;
  page = await onListPage(
    GamesDataTable,
    query,
    {
      global: {
        stubs: { GamesFilter: true, Paging: true, RouterLink: RouterLinkStub },
      },
    },
    () => {
      const store = useGamesStore();
      search = vi.spyOn(store, "searchGames").mockResolvedValue();
      vi.spyOn(store, "fetchTags").mockResolvedValue(undefined as never);
      store.tags = [
        { id: 5, title: "Фэнтези" },
        { id: 7, title: "Хоррор" },
      ] as never;
      store.searchResult = {
        resources: answer.rows ?? [],
        paging: { skip: answer.skip ?? 0, pages: 1, total: 0 },
      } as never;
      store.searchError = answer.error ?? null;
    },
  );
  return { ...page, search: search! };
}

const rowNumbers = (on: Page) =>
  on.wrapper.findAll("td.col-number").map((cell) => cell.text());

describe("GamesDataTable", () => {
  it.each([
    ["no filter", {}, "Игр пока нет"],
    ["a filter", { status: "Closed" }, "Игр по заданным фильтрам не найдено"],
    ["a search", { search: "эльф" }, "Игр по заданным фильтрам не найдено"],
  ])("says of an empty list under %s: %s", async (_what, query, words) => {
    const on = await tableOn(query);

    expect(on.wrapper.get(".table-empty").text()).toBe(words);
  });

  it("keeps the rows it has under a refusal, and the refusal over them", async () => {
    const on = await tableOn(
      {},
      { rows: [game("g1")], error: "Не удалось загрузить игры" },
    );

    expect(on.wrapper.get(".error-message").text()).toBe(
      "Не удалось загрузить игры",
    );
    expect(on.wrapper.findAll("tr.table-row")).toHaveLength(1);
  });

  it("draws no empty table under a refusal with nothing to show", async () => {
    const on = await tableOn({}, { error: "Не удалось загрузить игры" });

    expect(on.wrapper.find(".error-message").exists()).toBe(true);
    expect(on.wrapper.find("table").exists()).toBe(false);
  });

  it.each([
    [{ sortBy: "title", sortOrder: "asc" }, { "col-title": "ascending" }],
    [{ sortBy: "status" }, { "col-status": "descending" }],
    [{ sortBy: "popularity" }, {}],
    [{}, {}],
  ])("marks the column the address %j sorts by", async (query, marked) => {
    const on = await tableOn(query, { rows: [game("g1")] });

    const sorted = Object.fromEntries(
      on.wrapper
        .findAll("th[aria-sort]")
        .map((th) => [
          th.classes().find((c) => c.startsWith("col-") && c !== "col-number"),
          th.attributes("aria-sort"),
        ]),
    );
    expect(sorted).toEqual(marked);
  });

  it("sends a tag to the list it stands in, narrowed by it, from its first page", async () => {
    const on = await tableOn(
      { status: "Active", requiredTags: "5", number: "3", search: "эльф" },
      { rows: [game("g1", [7])] },
    );

    const tag = on.wrapper
      .findAllComponents(RouterLinkStub)
      .find((link) => link.text() === "Хоррор")!;
    expect(tag.props("to")).toEqual({
      name: "games",
      query: { status: "Active", search: "эльф", requiredTags: "5,7" },
    });
  });

  it("numbers the rows of a later page from where the page starts", async () => {
    const on = await tableOn(
      { number: "3" },
      { rows: [game("g1"), game("g2")], skip: 40 },
    );

    expect(rowNumbers(on)).toEqual(["41", "42"]);
  });

  it("asks for the list the address names, and again when the address moves", async () => {
    const on = await tableOn({ status: "Draft" });
    expect(on.search).toHaveBeenLastCalledWith(
      expect.objectContaining({ status: "Draft" }),
    );

    await on.router.replace({ name: "list", query: { status: "Closed" } });
    await on.settled();

    expect(on.search).toHaveBeenLastCalledWith(
      expect.objectContaining({ status: "Closed" }),
    );
  });
});
