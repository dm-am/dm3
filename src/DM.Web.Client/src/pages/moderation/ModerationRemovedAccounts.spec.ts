/**
 * @vitest-environment jsdom
 */

/**
 * The removed accounts, as the moderator works with them.
 *
 * A removed account is hidden from every other list, profile and lookup in the
 * product, so this page is the only surface in the client that can show one and
 * the only one that can bring one back. What it draws has to come from the
 * endpoint that answers with removed accounts and from nowhere else: a page
 * that filled itself from a general user list would look right on a seeded
 * stand, where most accounts are live, and quietly offer "Восстановить" over
 * accounts that were never removed.
 *
 * The search is checked through the address on both sides, because that is
 * where it lives: what the address says reaches the request, and what the
 * moderator types reaches the address without the page number of the list they
 * are leaving. A term kept in a ref here would pass a check written against the
 * request alone and still hand over a link that opens the whole list.
 *
 * The restore is checked all the way round — the confirmation first, the name
 * of the row second, the re-read third. Nothing here decides locally that a row
 * is gone: the account comes back with its name and everything under it, and
 * which page of the list it leaves is the server's answer, not this one's.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { mount, flushPromises, enableAutoUnmount } from "@vue/test-utils";
import type { VueWrapper } from "@vue/test-utils";
import type { Mock } from "vitest";
import type { Ref } from "vue";
import * as vueRouter from "vue-router";
import ModerationRemovedAccounts from "./ModerationRemovedAccounts.vue";

const getRemovedAccounts = vi.hoisted(() => vi.fn());
const restoreAccount = vi.hoisted(() => vi.fn());

vi.mock("@/entities/moderation", () => ({
  moderationApi: { getRemovedAccounts, restoreAccount },
}));

/**
 * The address bar, held in a ref rather than a plain object: the page derives
 * the term and the page number inside computeds and refetches on their change,
 * and a plain property read there is tracked by nothing. The push moves that
 * same ref, the way a real navigation would, and refuses to move when the
 * address it is given is the one already open, as the real router does.
 */
vi.mock("vue-router", async () => {
  const { shallowRef } = await import("vue");
  const query = shallowRef<Record<string, string>>({});
  const isCurrent = (next: Record<string, string>) => {
    const current = query.value;
    const keys = Object.keys(next);
    return (
      keys.length === Object.keys(current).length &&
      keys.every((key) => current[key] === next[key])
    );
  };
  const push = vi.fn((to: { query?: Record<string, string> }) => {
    const next = { ...(to.query ?? {}) };
    if (isCurrent(next)) return;
    query.value = next;
  });
  return {
    useRoute: () => ({
      get query() {
        return query.value;
      },
    }),
    useRouter: () => ({ push }),
    routeQuery: query,
    routerPush: push,
  };
});

const { routeQuery, routerPush } = vueRouter as unknown as {
  routeQuery: Ref<Record<string, string>>;
  routerPush: Mock;
};

/** The gate has its own spec (roleGate); here the viewer is set per test. */
const gate = vi.hoisted(() => ({ hasAccess: true, isAdmin: false }));

vi.mock("./lib/useRoleGate", async () => {
  const { computed } = await import("vue");
  return {
    useRoleGate: () => ({
      hasAccess: computed(() => gate.hasAccess),
      deniedText: "Страница доступна только старшим модераторам",
      isAdmin: computed(() => gate.isAdmin),
    }),
  };
});

const toastSuccess = vi.hoisted(() => vi.fn());

vi.mock("@/shared/lib/composables/useToast", () => ({
  useToast: () => ({ success: toastSuccess, error: vi.fn() }),
}));

const notifyFailure = vi.hoisted(() => vi.fn());

vi.mock("@/shared/lib/errors", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/shared/lib/errors")>()),
  notifyFailure,
}));

/** The page of the neighbouring moderation lists. */
const PAGE_SIZE = 25;

const account = (over: Record<string, unknown> = {}) => ({
  id: "a-1",
  username: "Иван",
  email: "ivan@example.com",
  role: "RegularUser",
  registeredUtc: "2024-03-01T10:00:00",
  lastActivityUtc: "2026-01-04T18:30:00",
  ...over,
});

const page = (resources: unknown[], total = resources.length, pages = 1) => ({
  data: {
    resources,
    paging: { skip: 0, take: PAGE_SIZE, total, current: 1, pages },
  },
  error: null,
});

// The address is shared by every mount in this file, so a page left standing
// would refetch when the next test moves the query under it.
enableAutoUnmount(afterEach);

/**
 * The table is mounted for real: what a row shows and which rows exist at all
 * is the subject here, and a stubbed table would answer both questions itself.
 * So is the confirmation, drawn in place: the restore is its request, and a
 * refusal is printed in it.
 */

function render() {
  return mount(ModerationRemovedAccounts, {
    global: {
      stubs: {
        RouterLink: {
          props: ["to"],
          template:
            "<a class='router-link' :data-to='JSON.stringify(to)'><slot /></a>",
        },
        PageTitle: { template: "<h1><slot /></h1>" },
        "page-title": { template: "<h1><slot /></h1>" },
        // Registered on the app, so a mount of the page alone knows neither.
        FormField: {
          props: ["label", "name"],
          template:
            "<div class='field-stub'><label>{{ label }}</label><slot /></div>",
        },
        Button: { template: "<button type='submit'><slot /></button>" },
        Paging: { props: ["paging"], template: "<nav class='paging-stub' />" },
        ErrorState: { template: "<div class='error-stub' />" },
        Teleport: true,
      },
    },
  });
}

async function rendered() {
  const wrapper = render();
  await flushPromises();
  return wrapper;
}

/** The names the table draws, in the order it draws them. */
function names(wrapper: VueWrapper): string[] {
  return wrapper
    .findAll("tbody tr.table-row")
    .map((row) => row.findAll("td")[0].text().trim());
}

/** The row of one account, by the name it shows. */
function rowFor(wrapper: VueWrapper, username: string) {
  const row = wrapper
    .findAll("tbody tr.table-row")
    .find((candidate) => candidate.findAll("td")[0].text().trim() === username);
  expect(row, `no row for ${username}`).toBeDefined();
  return row!;
}

/** The control that brings one account back. */
function restoreControl(wrapper: VueWrapper, username: string) {
  return rowFor(wrapper, username)
    .findAll("button")
    .find((button) => button.text() === "Восстановить");
}

/** Types a term into the field and submits the form under it. */
async function search(wrapper: VueWrapper, term: string) {
  await wrapper.find("form input").setValue(term);
  await wrapper.find("form").trigger("submit");
  await flushPromises();
}

describe("the removed accounts page", () => {
  beforeEach(() => {
    getRemovedAccounts.mockReset();
    restoreAccount.mockReset();
    toastSuccess.mockReset();
    notifyFailure.mockReset();
    routerPush.mockClear();
    routeQuery.value = {};
    gate.hasAccess = true;
    gate.isAdmin = false;
    getRemovedAccounts.mockResolvedValue(page([account()]));
    restoreAccount.mockResolvedValue({ data: null, error: null });
  });

  it("draws the accounts the removed list answered with, and only those", async () => {
    getRemovedAccounts.mockResolvedValue(
      page([
        account({ id: "a-1", username: "Иван" }),
        account({ id: "a-2", username: "Петр" }),
      ]),
    );

    const wrapper = await rendered();

    expect(getRemovedAccounts).toHaveBeenCalledTimes(1);
    expect(names(wrapper)).toEqual(["Иван", "Петр"]);
  });

  /**
   * The role dictionary names the staff roles in the plural, because it titles
   * the sections that group them. A column about one account has to read in the
   * singular, and the difference only shows on the four roles that are a group.
   */
  it("names the role of one account in the singular", async () => {
    getRemovedAccounts.mockResolvedValue(
      page([account({ username: "Гоблин", role: "SeniorModerator" })]),
    );

    const wrapper = await rendered();

    const cells = rowFor(wrapper, "Гоблин")
      .findAll("td")
      .map((cell) => cell.text());
    expect(cells).toContain("Старший модератор");
    expect(cells).not.toContain("Старшие модераторы");
  });

  it("asks for the page named in the address, at the size of the list", async () => {
    routeQuery.value = { number: "3" };

    await rendered();

    expect(getRemovedAccounts).toHaveBeenCalledWith({
      search: undefined,
      skip: 2 * PAGE_SIZE,
      take: PAGE_SIZE,
    });
  });

  it("shows no row at all when nothing has been removed", async () => {
    getRemovedAccounts.mockResolvedValue(page([]));

    const wrapper = await rendered();

    expect(names(wrapper)).toEqual([]);
    expect(wrapper.text()).toContain("Удаленных пользователей нет");
  });

  it("carries the term the address holds into the request", async () => {
    routeQuery.value = { search: "ivan@example.com" };

    await rendered();

    expect(getRemovedAccounts).toHaveBeenCalledWith({
      search: "ivan@example.com",
      skip: 0,
      take: PAGE_SIZE,
    });
  });

  it("sends what the moderator typed to the address and on into the request", async () => {
    const wrapper = await rendered();

    await search(wrapper, "  Петр  ");

    expect(routerPush).toHaveBeenCalledWith({
      name: "moderation-removed-accounts",
      query: { search: "Петр" },
    });
    expect(getRemovedAccounts).toHaveBeenLastCalledWith({
      search: "Петр",
      skip: 0,
      take: PAGE_SIZE,
    });
  });

  it("leaves the page number of the list it is narrowing behind", async () => {
    routeQuery.value = { number: "4" };
    const wrapper = await rendered();

    await search(wrapper, "Петр");

    expect(routerPush).toHaveBeenCalledWith({
      name: "moderation-removed-accounts",
      query: { search: "Петр" },
    });
    expect(getRemovedAccounts).toHaveBeenLastCalledWith({
      search: "Петр",
      skip: 0,
      take: PAGE_SIZE,
    });
  });

  it("drops the filter when the field is emptied", async () => {
    routeQuery.value = { search: "Петр" };
    const wrapper = await rendered();

    await search(wrapper, "");

    expect(getRemovedAccounts).toHaveBeenLastCalledWith({
      search: undefined,
      skip: 0,
      take: PAGE_SIZE,
    });
  });

  /**
   * The search line of the moderation lists: the site's search field and
   * "Найти" beside it, no caption over them. The server matches the term
   * anywhere in a name or an address, so the field names both and does not
   * call itself exact, as the name lookups beside do.
   */
  it("draws the search line of the moderation lists", async () => {
    const wrapper = await rendered();

    expect(wrapper.find(".field-stub").exists()).toBe(false);
    expect(wrapper.find("form .search-container").exists()).toBe(true);
    expect(wrapper.find("form input").attributes("placeholder")).toBe(
      "Имя или почта",
    );
    expect(wrapper.find("form button").text()).toBe("Найти");
  });

  /** The cross of the field drops the term at once, without "Найти". */
  it("drops the term at once when the cross empties the field", async () => {
    routeQuery.value = { search: "Петр", number: "2" };
    const wrapper = await rendered();

    await wrapper.find("form .clear-input-btn").trigger("click");
    await flushPromises();

    expect(routerPush).toHaveBeenCalledWith({
      name: "moderation-removed-accounts",
      query: {},
    });
    expect(getRemovedAccounts).toHaveBeenLastCalledWith({
      search: undefined,
      skip: 0,
      take: PAGE_SIZE,
    });
  });

  it("brings the account of the pressed row back, once the question is answered", async () => {
    getRemovedAccounts.mockResolvedValue(
      page([
        account({ id: "a-1", username: "Иван" }),
        account({ id: "a-2", username: "Петр" }),
      ]),
    );
    const wrapper = await rendered();

    await restoreControl(wrapper, "Петр")!.trigger("click");
    expect(restoreAccount).not.toHaveBeenCalled();
    expect(wrapper.find(".dialog-message").text()).toContain("Петр");

    await wrapper.find(".dialog-btn-submit").trigger("click");
    await flushPromises();

    // The window prints a refusal in itself, so the interceptor keeps quiet.
    expect(restoreAccount).toHaveBeenCalledWith("Петр", {
      ownsRefusal: true,
      ownsRateLimit: true,
    });
    expect(wrapper.find(".dialog-container").exists()).toBe(false);
    expect(toastSuccess).not.toHaveBeenCalled();
    // The row leaves the list because the list is read again, not because this
    // page decided it should.
    expect(getRemovedAccounts).toHaveBeenCalledTimes(2);
  });

  it("keeps the list as it was and says the refusal in the window", async () => {
    restoreAccount.mockResolvedValue({
      data: null,
      error: { type: "NotFound", status: 404 },
    });
    const wrapper = await rendered();

    await restoreControl(wrapper, "Иван")!.trigger("click");
    await wrapper.find(".dialog-btn-submit").trigger("click");
    await flushPromises();

    expect(wrapper.get(".dialog-refusal").text()).toBe(
      "Не удалось восстановить аккаунт",
    );
    expect(wrapper.find(".dialog-container").exists()).toBe(true);
    expect(notifyFailure).not.toHaveBeenCalled();
    expect(toastSuccess).not.toHaveBeenCalled();
    expect(getRemovedAccounts).toHaveBeenCalledTimes(1);
  });

  it("offers the address to an administrator and to nobody else", async () => {
    const withoutAddress = await rendered();
    expect(withoutAddress.text()).not.toContain("ivan@example.com");
    expect(withoutAddress.find("thead").text()).not.toContain("Почта");

    gate.isAdmin = true;
    const withAddress = await rendered();
    expect(withAddress.find("thead").text()).toContain("Почта");
    expect(withAddress.text()).toContain("ivan@example.com");
  });

  it("asks for nothing on behalf of a viewer it refuses", async () => {
    gate.hasAccess = false;

    const wrapper = await rendered();

    expect(getRemovedAccounts).not.toHaveBeenCalled();
    expect(wrapper.text()).toContain(
      "Страница доступна только старшим модераторам",
    );
  });
});
