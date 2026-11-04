/**
 * @vitest-environment jsdom
 */

// What the page draws comes from the endpoint of removed accounts alone. The
// search goes both ways through the address, and the restore runs through the
// confirmation and a re-read: nothing here decides locally that a row is gone.
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { flushPromises, enableAutoUnmount } from "@vue/test-utils";
import type { VueWrapper } from "@vue/test-utils";
import type { Router } from "vue-router";
import { createPinia, setActivePinia } from "pinia";
import { UserRole } from "@/shared/api/models/common/user";
import { useAuthStore } from "@/shared/stores";
import { useToast } from "@/shared/lib/composables/useToast";
import { DEFAULT_PAGE_SIZE } from "@/shared/lib/composables/usePaging";
import ModerationRemovedAccounts from "./ModerationRemovedAccounts.vue";
import {
  mountPage,
  windows,
  type MountOptions,
} from "@/shared/lib/testing/mountPage";

const { getRemovedAccounts, restoreAccount } = vi.hoisted(() => ({
  getRemovedAccounts: vi.fn(),
  restoreAccount: vi.fn(),
}));

// The network only: the router, the store, the plates and the window are the real ones.
vi.mock("@/entities/moderation/api/moderationApi", async (importOriginal) => ({
  ...(await importOriginal<
    typeof import("@/entities/moderation/api/moderationApi")
  >()),
  default: { getRemovedAccounts, restoreAccount },
}));

/** The page of a reader who chose no size. */
const PAGE_SIZE = DEFAULT_PAGE_SIZE;

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

enableAutoUnmount(afterEach);

let router: Router;

async function rendered(
  address = "/moderation/removed-accounts",
  options: Omit<MountOptions, "routes"> = {},
) {
  const mounted = await mountPage(ModerationRemovedAccounts, address, {
    routes: [["moderation-removed-accounts", "/moderation/removed-accounts"]],
    ...options,
  });
  router = mounted.router;
  await flushPromises();
  return mounted.wrapper;
}

/** Where the reader is now. */
const address = () => router.currentRoute.value.fullPath;

/** What the corner of the screen says, if anything. */
const plates = () => useToast().toasts.value.map((toast) => toast.message);

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
    const { toasts, dismiss } = useToast();
    for (const toast of [...toasts.value]) dismiss(toast.id);
    getRemovedAccounts.mockReset();
    restoreAccount.mockReset();
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

  // ROLE_INFO names the staff roles in the plural; a column about one account reads in the singular.
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
    await rendered("/moderation/removed-accounts?number=3");

    expect(getRemovedAccounts).toHaveBeenCalledWith({
      search: undefined,
      skip: 2 * PAGE_SIZE,
      take: PAGE_SIZE,
    });
  });

  it("asks for pages as long as the reader's lists", async () => {
    const pinia = createPinia();
    setActivePinia(pinia);
    useAuthStore().user = {
      settings: { pageSize: 7 },
    } as never;

    await rendered("/moderation/removed-accounts?number=3", { pinia });

    expect(getRemovedAccounts).toHaveBeenCalledWith({
      search: undefined,
      skip: 14,
      take: 7,
    });
  });

  it("shows no row at all when nothing has been removed", async () => {
    getRemovedAccounts.mockResolvedValue(page([]));

    const wrapper = await rendered();

    expect(names(wrapper)).toEqual([]);
    expect(wrapper.text()).toContain("Удаленных пользователей нет");
  });

  it("carries the term the address holds into the request", async () => {
    await rendered("/moderation/removed-accounts?search=ivan%40example.com");

    expect(getRemovedAccounts).toHaveBeenCalledWith({
      search: "ivan@example.com",
      skip: 0,
      take: PAGE_SIZE,
    });
  });

  it("sends what the moderator typed to the address and on into the request", async () => {
    const wrapper = await rendered();

    await search(wrapper, "  Петр  ");

    expect(router.currentRoute.value.query).toEqual({ search: "Петр" });
    expect(getRemovedAccounts).toHaveBeenLastCalledWith({
      search: "Петр",
      skip: 0,
      take: PAGE_SIZE,
    });
  });

  it("leaves the page number of the list it is narrowing behind", async () => {
    const wrapper = await rendered("/moderation/removed-accounts?number=4");

    await search(wrapper, "Петр");

    expect(router.currentRoute.value.query).toEqual({ search: "Петр" });
    expect(getRemovedAccounts).toHaveBeenLastCalledWith({
      search: "Петр",
      skip: 0,
      take: PAGE_SIZE,
    });
  });

  it("drops the filter when the field is emptied", async () => {
    const wrapper = await rendered(
      "/moderation/removed-accounts?search=%D0%9F%D0%B5%D1%82%D1%80",
    );

    await search(wrapper, "");

    expect(address()).toBe("/moderation/removed-accounts");
    expect(getRemovedAccounts).toHaveBeenLastCalledWith({
      search: undefined,
      skip: 0,
      take: PAGE_SIZE,
    });
  });

  // The server matches anywhere in a name or an address, so the field does not call itself exact.
  it("draws the search line of the moderation lists", async () => {
    const wrapper = await rendered();

    expect(wrapper.find("form label").exists()).toBe(false);
    expect(wrapper.find("form .search-container").exists()).toBe(true);
    expect(wrapper.find("form input").attributes("placeholder")).toBe(
      "Имя или почта",
    );
    expect(wrapper.find("form button[type=submit]").text()).toBe("Найти");
  });

  it("drops the term at once when the cross empties the field", async () => {
    const wrapper = await rendered(
      "/moderation/removed-accounts?search=%D0%9F%D0%B5%D1%82%D1%80&number=2",
    );

    await wrapper.find("form .clear-input-btn").trigger("click");
    await flushPromises();

    expect(address()).toBe("/moderation/removed-accounts");
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
    expect(windows().get(".dialog-message").text()).toContain("Петр");

    await windows().get(".dialog-btn-submit").trigger("click");
    await flushPromises();

    // The window prints a refusal in itself, so the interceptor keeps quiet.
    expect(restoreAccount).toHaveBeenCalledWith("Петр", {
      ownsRefusal: true,
      ownsRateLimit: true,
    });
    expect(windows().find(".dialog-container").exists()).toBe(false);
    expect(plates()).toEqual([]);
    // The row leaves because the list is read again.
    expect(getRemovedAccounts).toHaveBeenCalledTimes(2);
  });

  it("keeps the list as it was and says the refusal in the window", async () => {
    restoreAccount.mockResolvedValue({
      data: null,
      error: { type: "NotFound", status: 404 },
    });
    const wrapper = await rendered();

    await restoreControl(wrapper, "Иван")!.trigger("click");
    await windows().get(".dialog-btn-submit").trigger("click");
    await flushPromises();

    expect(windows().get(".dialog-refusal").text()).toBe(
      "Не удалось восстановить аккаунт",
    );
    expect(windows().find(".dialog-container").exists()).toBe(true);
    expect(plates()).toEqual([]);
    expect(getRemovedAccounts).toHaveBeenCalledTimes(1);
  });

  it("offers the address to an administrator and to nobody else", async () => {
    const withoutAddress = await rendered();
    expect(withoutAddress.text()).not.toContain("ivan@example.com");
    expect(withoutAddress.find("thead").text()).not.toContain("Почта");

    const pinia = createPinia();
    setActivePinia(pinia);
    useAuthStore().updateUser({
      id: "u-admin",
      username: "Админ",
      role: UserRole.Admin,
    } as never);
    const withAddress = await rendered(undefined, { pinia });
    expect(withAddress.find("thead").text()).toContain("Почта");
    expect(withAddress.text()).toContain("ivan@example.com");
  });
});
