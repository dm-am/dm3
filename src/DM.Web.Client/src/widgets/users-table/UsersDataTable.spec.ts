/**
 * @vitest-environment jsdom
 */

/**
 * The community table as a reader of the list meets it: the words of an empty
 * list, the rows that stay under a refusal, the column the address sorts by
 * (its sort names are not its column names) and the numbering of a later page.
 */
import { describe, it, expect, vi, afterEach } from "vitest";
import { RouterLinkStub } from "@vue/test-utils";
import { useCommunityStore } from "@/entities/user";
import { onListPage, type Page } from "@/shared/lib/testing/urlFilter";
import UsersDataTable from "./UsersDataTable.vue";

let page: Page | null = null;

afterEach(async () => {
  await page?.settled();
  page?.wrapper.unmount();
  page = null;
  vi.restoreAllMocks();
});

const user = (username: string) => ({
  id: username,
  username,
  role: "RegularUser",
  registeredUtc: "2020-01-01T00:00:00Z",
  lastVisitDate: "2026-01-01T00:00:00Z",
});

interface Answer {
  rows?: ReturnType<typeof user>[];
  skip?: number;
  error?: string | null;
}

async function tableOn(query: Record<string, string>, answer: Answer = {}) {
  let search: ReturnType<typeof vi.spyOn> | null = null;
  page = await onListPage(
    UsersDataTable,
    query,
    {
      global: {
        stubs: { UsersFilter: true, Paging: true, RouterLink: RouterLinkStub },
      },
    },
    () => {
      const store = useCommunityStore();
      search = vi.spyOn(store, "searchUsers").mockResolvedValue();
      store.searchResult = {
        resources: answer.rows ?? [],
        paging: { skip: answer.skip ?? 0, pages: 1, total: 0 },
      } as never;
      store.searchError = answer.error ?? null;
    },
  );
  return { ...page, search: search! };
}

describe("UsersDataTable", () => {
  it.each([
    ["no filter", {}, "Пользователей пока нет"],
    [
      "a filter",
      { role: "Mentor" },
      "Пользователей по заданным фильтрам не найдено",
    ],
  ])("says of an empty list under %s: %s", async (_what, query, words) => {
    const on = await tableOn(query);

    expect(on.wrapper.get(".table-empty").text()).toBe(words);
  });

  it("keeps the rows it has under a refusal, and the refusal over them", async () => {
    const on = await tableOn(
      {},
      { rows: [user("anna")], error: "Не удалось загрузить пользователей" },
    );

    expect(on.wrapper.get(".error-message").text()).toBe(
      "Не удалось загрузить пользователей",
    );
    expect(on.wrapper.findAll("tr.table-row")).toHaveLength(1);
  });

  it("draws no empty table under a refusal with nothing to show", async () => {
    const on = await tableOn(
      {},
      { error: "Не удалось загрузить пользователей" },
    );

    expect(on.wrapper.find("table").exists()).toBe(false);
  });

  it.each([
    [{}, { "col-activity": "descending" }],
    [{ sortBy: "username", sortOrder: "asc" }, { "col-username": "ascending" }],
    [{ sortBy: "rating" }, { "col-rating": "descending" }],
    [{ sortBy: "registered" }, { "col-registered": "descending" }],
    [{ sortBy: "popularity" }, {}],
  ])("marks the column the address %j sorts by", async (query, marked) => {
    const on = await tableOn(query, { rows: [user("anna")] });

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
    const on = await tableOn(
      { number: "4" },
      { rows: [user("anna"), user("boris")], skip: 60 },
    );

    expect(on.wrapper.findAll("td.col-number").map((td) => td.text())).toEqual([
      "61",
      "62",
    ]);
  });

  it("asks for the list the address names, and again when the address moves", async () => {
    const on = await tableOn({ role: "Mentor" });
    expect(on.search).toHaveBeenLastCalledWith(
      expect.objectContaining({ role: "Mentor" }),
    );

    await on.router.replace({ name: "list", query: { role: "Admin" } });
    await on.settled();

    expect(on.search).toHaveBeenLastCalledWith(
      expect.objectContaining({ role: "Admin" }),
    );
  });
});
