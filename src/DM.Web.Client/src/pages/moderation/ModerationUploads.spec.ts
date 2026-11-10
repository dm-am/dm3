/**
 * @vitest-environment jsdom
 */

// The name filter goes both ways through the address, and a new name opens on
// the first page. A name nobody carries is the server's 404 and an empty list.
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import {
  flushPromises,
  enableAutoUnmount,
  type VueWrapper,
} from "@vue/test-utils";
import type { Router } from "vue-router";
import { createPinia, getActivePinia, setActivePinia } from "pinia";
import { useAuthStore } from "@/shared/stores";
import { DEFAULT_PAGE_SIZE } from "@/shared/lib/composables/usePaging";
import { mountPage } from "@/shared/lib/testing/mountPage";
import ModerationUploads from "./ModerationUploads.vue";

const getAllUploads = vi.hoisted(() => vi.fn());

vi.mock("@/entities/moderation", () => ({
  moderationApi: { getAllUploads },
}));

const UPLOADS = "/moderation/uploads";

let router: Router;
/** Where the list stands: its route and the filter in the address. */
const where = () => ({
  name: router.currentRoute.value.name,
  query: router.currentRoute.value.query,
});

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

async function render(address: string) {
  const mounted = await mountPage(ModerationUploads, address, {
    routes: [
      ["moderation-uploads", UPLOADS],
      ["profile", "/users/:username"],
    ],
    pinia: getActivePinia(),
    stubs: {
      "page-title": { template: "<h1><slot /></h1>" },
      // Plain, so the assertions read the page's own words.
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
  });
  router = mounted.router;
  return mounted.wrapper;
}

async function rendered(address = UPLOADS) {
  const wrapper = await render(address);
  await flushPromises();
  return wrapper;
}

const nameField = (wrapper: VueWrapper) =>
  wrapper.find<HTMLInputElement>("form input");

async function lookUp(wrapper: VueWrapper, name: string) {
  await nameField(wrapper).setValue(name);
  await wrapper.find("form").trigger("submit");
  await flushPromises();
}

describe("the moderation list of uploads", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    getAllUploads.mockReset();
    getAllUploads.mockResolvedValue(page([upload()]));
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
    const wrapper = await rendered(`${UPLOADS}?username=Загрузивший&number=2`);

    expect(getAllUploads).toHaveBeenCalledWith({
      username: "Загрузивший",
      number: 2,
      size: PAGE_SIZE,
    });
    // A link handed to another moderator opens with the name in the field.
    expect(nameField(wrapper).element.value).toBe("Загрузивший");
  });

  it("asks for pages as long as the reader's lists", async () => {
    useAuthStore().user = {
      settings: { pageSize: 7 },
    } as never;

    await rendered(`${UPLOADS}?number=3`);

    expect(getAllUploads).toHaveBeenCalledWith({
      username: undefined,
      number: 3,
      size: 7,
    });
  });

  it("puts the name in the address without the page number of the list being left", async () => {
    const wrapper = await rendered(`${UPLOADS}?number=3`);

    await lookUp(wrapper, "  Загрузивший  ");

    expect(where()).toEqual({
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

    expect(where()).toEqual({ name: "moderation-uploads", query: {} });
    expect(getAllUploads).toHaveBeenCalledTimes(1);
  });

  it("drops the name at once when the cross empties the field", async () => {
    const wrapper = await rendered(`${UPLOADS}?username=Загрузивший&number=2`);

    await wrapper.find("form .clear-input-btn").trigger("click");
    await flushPromises();

    expect(where()).toEqual({ name: "moderation-uploads", query: {} });
    expect(getAllUploads).toHaveBeenLastCalledWith({
      username: undefined,
      number: 1,
      size: PAGE_SIZE,
    });
  });

  it("says a name nobody carries found nothing, and not that the list failed", async () => {
    getAllUploads.mockResolvedValue(notFound);

    const wrapper = await rendered(`${UPLOADS}?username=НетТакого`);

    expect(wrapper.find(".error-stub").exists()).toBe(false);
    expect(wrapper.text()).toContain(
      "Загруженных файлов по заданным фильтрам не найдено",
    );
    expect(wrapper.find("table").exists()).toBe(false);
  });

  it("says the same of a name that uploaded nothing", async () => {
    getAllUploads.mockResolvedValue(page([]));

    const wrapper = await rendered(`${UPLOADS}?username=Молчун`);

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
