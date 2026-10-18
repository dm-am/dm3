/**
 * @vitest-environment jsdom
 */

/**
 * "Все загруженные файлы", the name filter over it.
 *
 * The filter is the search line of the moderation lists: the site's search
 * field and "Найти" beside it, no caption over them. It is checked through the
 * address on both sides, as on the warnings beside: what the address says
 * reaches the request and the field, and what the moderator types reaches the
 * address without the page number of the list being left. The name used to
 * live in a ref, and a moderator on the third page of everything who looked a
 * name up was shown the third page of that name, which mostly does not exist.
 *
 * The server looks the name up before it lists anything, and a name nobody
 * carries is its 404. The page used to say "Не удалось загрузить список
 * файлов" with "Повторить" over it, which sends a moderator to retry a typo;
 * it is a list with nothing in it, and is said as one.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { mount, flushPromises, enableAutoUnmount } from "@vue/test-utils";
import type { Mock } from "vitest";
import type { Ref } from "vue";
import * as vueRouter from "vue-router";
import ModerationUploads from "./ModerationUploads.vue";

const getAllUploads = vi.hoisted(() => vi.fn());

vi.mock("@/entities/moderation", () => ({
  moderationApi: { getAllUploads },
}));

/**
 * The address bar, held in a ref the page reads through computeds. The push
 * moves that same ref, the way a real navigation would, and refuses to move
 * when the address it is given is the one already open, as the real router
 * does.
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

// The gate has its own spec (roleGate); here the viewer is a moderator.
vi.mock("./lib/useRoleGate", () => ({
  useRoleGate: () => ({ hasAccess: true, deniedText: "" }),
}));

/** The page size of the list. */
const PAGE_SIZE = 25;

const upload = (over: Record<string, unknown> = {}) => ({
  id: "u-1",
  userId: "user-1",
  uploaderUsername: "Загрузивший",
  originalFileName: "карта.png",
  storedFileName: "abc.png",
  contentType: "image/png",
  sizeBytes: 2048,
  createdUtc: "2026-10-01T10:00:00Z",
  status: "Confirmed",
  ...over,
});

const page = (resources: unknown[]) => ({
  data: {
    resources,
    paging: { skip: 0, take: PAGE_SIZE, total: resources.length, pages: 1 },
  },
  error: null,
});

const notFound = {
  data: null,
  error: { status: 404, title: "Пользователь не найден" },
};

enableAutoUnmount(afterEach);

function render() {
  return mount(ModerationUploads, {
    global: {
      stubs: {
        RouterLink: {
          props: ["to"],
          template: "<a class='router-link'><slot /></a>",
        },
        "page-title": { template: "<h1><slot /></h1>" },
        // Registered on the app, so a mount of the page alone knows neither.
        FormField: {
          props: ["label"],
          template:
            "<div class='field-stub'><label>{{ label }}</label><slot /></div>",
        },
        Button: { template: "<button><slot /></button>" },
        Paging: { template: "<nav class='paging-stub' />" },
        ErrorState: {
          props: ["message"],
          template: "<div class='error-stub'>{{ message }}</div>",
        },
        ConfirmDialog: true,
      },
    },
  });
}

async function rendered() {
  const wrapper = render();
  await flushPromises();
  return wrapper;
}

const nameField = (wrapper: ReturnType<typeof render>) =>
  wrapper.find<HTMLInputElement>("form input");

async function lookUp(wrapper: ReturnType<typeof render>, name: string) {
  await nameField(wrapper).setValue(name);
  await wrapper.find("form").trigger("submit");
  await flushPromises();
}

describe("the moderation list of uploads", () => {
  beforeEach(() => {
    getAllUploads.mockReset();
    getAllUploads.mockResolvedValue(page([upload()]));
    routerPush.mockClear();
    routeQuery.value = {};
  });

  it("draws the search line of the moderation lists, no caption over it", async () => {
    const wrapper = await rendered();

    expect(wrapper.find("label").exists()).toBe(false);
    expect(wrapper.find("form .search-container").exists()).toBe(true);
    expect(nameField(wrapper).attributes("placeholder")).toBe(
      "Точное имя пользователя",
    );
    expect(wrapper.find("form button").text()).toBe("Найти");
  });

  it("asks for the name and the page the address names", async () => {
    routeQuery.value = { username: "Загрузивший", number: "2" };

    const wrapper = await rendered();

    expect(getAllUploads).toHaveBeenCalledWith({
      username: "Загрузивший",
      number: 2,
      size: PAGE_SIZE,
    });
    // A link handed to another moderator opens with the name in the field.
    expect(nameField(wrapper).element.value).toBe("Загрузивший");
  });

  it("puts the name in the address without the page number of the list being left", async () => {
    routeQuery.value = { number: "3" };
    const wrapper = await rendered();

    await lookUp(wrapper, "  Загрузивший  ");

    expect(routerPush).toHaveBeenCalledWith({
      name: "moderation-uploads",
      query: { username: "Загрузивший" },
    });
    expect(getAllUploads).toHaveBeenLastCalledWith({
      username: "Загрузивший",
      number: 1,
      size: PAGE_SIZE,
    });
    expect(nameField(wrapper).element.value).toBe("Загрузивший");
  });

  /** Typing is no search yet: the server finds whole names only. */
  it("asks for nothing while the name is being typed", async () => {
    const wrapper = await rendered();

    await nameField(wrapper).setValue("Загр");
    await flushPromises();

    expect(routerPush).not.toHaveBeenCalled();
    expect(getAllUploads).toHaveBeenCalledTimes(1);
  });

  it("drops the name at once when the cross empties the field", async () => {
    routeQuery.value = { username: "Загрузивший", number: "2" };
    const wrapper = await rendered();

    await wrapper.find("form .clear-input-btn").trigger("click");
    await flushPromises();

    expect(routerPush).toHaveBeenCalledWith({
      name: "moderation-uploads",
      query: {},
    });
    expect(getAllUploads).toHaveBeenLastCalledWith({
      username: undefined,
      number: 1,
      size: PAGE_SIZE,
    });
  });

  it("says a name nobody carries found nothing, and not that the list failed", async () => {
    routeQuery.value = { username: "НетТакого" };
    getAllUploads.mockResolvedValue(notFound);

    const wrapper = await rendered();

    expect(wrapper.find(".error-stub").exists()).toBe(false);
    expect(wrapper.text()).toContain(
      "Загруженных файлов по заданным фильтрам не найдено",
    );
    expect(wrapper.find("table").exists()).toBe(false);
  });

  it("says the same of a name that uploaded nothing", async () => {
    routeQuery.value = { username: "Молчун" };
    getAllUploads.mockResolvedValue(page([]));

    const wrapper = await rendered();

    expect(wrapper.text()).toContain(
      "Загруженных файлов по заданным фильтрам не найдено",
    );
    expect(wrapper.text()).not.toContain("Загруженных файлов пока нет");
  });

  /** Without a name a 404 is no typo: the list did fail. */
  it("still says the list failed when nothing was looked up", async () => {
    getAllUploads.mockResolvedValue(notFound);

    const wrapper = await rendered();

    expect(wrapper.find(".error-stub").text()).toBe(
      "Не удалось загрузить список файлов",
    );
  });

  it("draws the empty list of the site when nothing is uploaded at all", async () => {
    getAllUploads.mockResolvedValue(page([]));

    const wrapper = await rendered();

    expect(wrapper.text()).toContain("Загруженных файлов пока нет");
  });
});
