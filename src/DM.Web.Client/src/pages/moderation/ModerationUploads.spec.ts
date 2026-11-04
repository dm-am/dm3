/**
 * @vitest-environment jsdom
 */

// The name filter goes both ways through the address, and a new name opens on
// the first page. A name nobody carries is the server's 404 and an empty list.
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { mount, flushPromises, enableAutoUnmount } from "@vue/test-utils";
import type { Mock } from "vitest";
import type { Ref } from "vue";
import * as vueRouter from "vue-router";
import { createPinia, setActivePinia } from "pinia";
import { useAuthStore } from "@/shared/stores";
import { DEFAULT_PAGE_SIZE } from "@/shared/lib/composables/usePaging";
import ModerationUploads from "./ModerationUploads.vue";

const getAllUploads = vi.hoisted(() => vi.fn());

vi.mock("@/entities/moderation", () => ({
  moderationApi: { getAllUploads },
}));

vi.mock("vue-router", async () =>
  (await import("./moderationLists.doubles")).vueRouter(),
);

const { routeQuery, routerPush } = vueRouter as unknown as {
  routeQuery: Ref<Record<string, string>>;
  routerPush: Mock;
};

/** The page of a reader who chose no size. */
const PAGE_SIZE = DEFAULT_PAGE_SIZE;

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
    setActivePinia(createPinia());
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

  it("asks for pages as long as the reader's lists", async () => {
    routeQuery.value = { number: "3" };
    useAuthStore().user = {
      settings: { pageSize: 7 },
    } as never;

    await rendered();

    expect(getAllUploads).toHaveBeenCalledWith({
      username: undefined,
      number: 3,
      size: 7,
    });
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
