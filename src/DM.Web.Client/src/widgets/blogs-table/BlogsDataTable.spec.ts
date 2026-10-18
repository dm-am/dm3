/**
 * @vitest-environment jsdom
 */

/**
 * The blogs catalogue table as a reader of the list meets it: the words of an
 * empty list, the rows that stay under a refusal, the column the address sorts
 * by and the numbering of a later page.
 */
import { describe, it, expect, vi, afterEach } from "vitest";
import { RouterLinkStub } from "@vue/test-utils";
import { useBlogsStore } from "@/entities/blog";
import { onListPage, type Page } from "@/shared/lib/testing/urlFilter";
import BlogsDataTable from "./BlogsDataTable.vue";

let page: Page | null = null;

afterEach(async () => {
  await page?.settled();
  page?.wrapper.unmount();
  page = null;
  vi.restoreAllMocks();
});

const blog = (id: string) => ({
  id,
  publicId: id,
  title: `Блог ${id}`,
  status: "Active",
  author: { username: "author" },
  assistants: [],
});

interface Answer {
  rows?: ReturnType<typeof blog>[];
  skip?: number;
  error?: string | null;
}

async function tableOn(query: Record<string, string>, answer: Answer = {}) {
  let search: ReturnType<typeof vi.spyOn> | null = null;
  page = await onListPage(
    BlogsDataTable,
    query,
    {
      global: {
        stubs: { BlogsFilter: true, Paging: true, RouterLink: RouterLinkStub },
      },
    },
    () => {
      const store = useBlogsStore();
      search = vi.spyOn(store, "searchBlogs").mockResolvedValue();
      store.searchResult = {
        resources: answer.rows ?? [],
        paging: { skip: answer.skip ?? 0, pages: 1, total: 0 },
      } as never;
      store.searchError = answer.error ?? null;
    },
  );
  return { ...page, search: search! };
}

describe("BlogsDataTable", () => {
  it.each([
    ["no filter", {}, "Блогов пока нет"],
    [
      "a filter",
      { status: "Closed" },
      "Блогов по заданным фильтрам не найдено",
    ],
  ])("says of an empty list under %s: %s", async (_what, query, words) => {
    const on = await tableOn(query);

    expect(on.wrapper.get(".table-empty").text()).toBe(words);
  });

  it("keeps the rows it has under a refusal, and the refusal over them", async () => {
    const on = await tableOn(
      {},
      { rows: [blog("b1")], error: "Не удалось загрузить блоги" },
    );

    expect(on.wrapper.get(".error-message").text()).toBe(
      "Не удалось загрузить блоги",
    );
    expect(on.wrapper.findAll("tr.table-row")).toHaveLength(1);
  });

  it("draws no empty table under a refusal with nothing to show", async () => {
    const on = await tableOn({}, { error: "Не удалось загрузить блоги" });

    expect(on.wrapper.find("table").exists()).toBe(false);
  });

  it.each([
    [{ sortBy: "title", sortOrder: "asc" }, { "col-title": "ascending" }],
    [{ sortBy: "status" }, { "col-status": "descending" }],
    [{ sortBy: "popularity" }, {}],
  ])("marks the column the address %j sorts by", async (query, marked) => {
    const on = await tableOn(query, { rows: [blog("b1")] });

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

  it("numbers the rows of a later page from where the page starts", async () => {
    const on = await tableOn({ number: "2" }, { rows: [blog("b1")], skip: 20 });

    expect(on.wrapper.findAll("td.col-number").map((td) => td.text())).toEqual([
      "21",
    ]);
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
