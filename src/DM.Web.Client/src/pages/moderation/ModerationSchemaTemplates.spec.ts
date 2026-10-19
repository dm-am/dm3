/**
 * @vitest-environment jsdom
 */

// The default mark: its template offers no removal, and its handover is
// checked by the re-read, since only the server knows which row lost it.
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { flushPromises, enableAutoUnmount } from "@vue/test-utils";
import type { VueWrapper } from "@vue/test-utils";
import { useToast } from "@/shared/lib/composables/useToast";
import ModerationSchemaTemplates from "./ModerationSchemaTemplates.vue";
import {
  mountPage,
  windows,
  type Address,
} from "@/shared/lib/testing/mountPage";

const { getTemplates, makeDefault, deleteTemplate } = vi.hoisted(() => ({
  getTemplates: vi.fn(),
  makeDefault: vi.fn(),
  deleteTemplate: vi.fn(),
}));

// The network only: the router, the window and the plates are the real ones.
vi.mock(
  "@/entities/attribute-schema-template/api/attributeSchemaTemplateApi",
  () => ({ default: { getTemplates, makeDefault, deleteTemplate } }),
);

const template = (over: Record<string, unknown> = {}) => ({
  id: "t-1",
  title: "Базовая анкета",
  isDefault: false,
  specifications: [],
  ...over,
});

const served = (resources: unknown[]) => ({
  data: { resources, paging: null },
  error: null,
});

const refused = { data: null, error: { type: "Conflict", status: 409 } };

/** What the page says it will answer for on the read that fills it. */
const OWNS_REFUSAL = { ownsRefusal: true, ownsRateLimit: true };

/** A read the server turned down, with the reason it named. */
const closed = {
  data: null,
  error: { type: "Forbidden", status: 403, title: "Раздел закрыт" },
};

/** A read the limiter turned down, with the wait beside the title. */
const limited = {
  data: null,
  error: {
    type: "TooManyRequests",
    status: 429,
    title: "Слишком много запросов",
    retryAfterSeconds: 30,
  },
};

enableAutoUnmount(afterEach);

/** The catalogue and the two addresses of one sheet. */
const TEMPLATE_ADDRESSES: Address[] = [
  ["moderation-schema-templates", "/moderation/schema-templates"],
  ["moderation-schema-template-new", "/moderation/schema-templates/new"],
  ["moderation-schema-template", "/moderation/schema-templates/:id"],
];

async function render() {
  const { wrapper } = await mountPage(
    ModerationSchemaTemplates,
    "/moderation/schema-templates",
    {
      routes: TEMPLATE_ADDRESSES,
    },
  );
  await flushPromises();
  return wrapper;
}

/** What the corner of the screen says, if anything. */
const plates = () => useToast().toasts.value.map((toast) => toast.message);

/** The row of one template, by the title it shows. */
function rowFor(wrapper: VueWrapper, title: string) {
  const row = wrapper
    .findAll(".template-row")
    .find((candidate) => candidate.find(".template-title").text() === title);
  expect(row, `no row for ${title}`).toBeDefined();
  return row!;
}

/** The control of a row, by its caption. */
function control(wrapper: VueWrapper, title: string, caption: string) {
  return rowFor(wrapper, title)
    .findAll("button")
    .find((button) => button.text() === caption);
}

describe("the schema template catalogue", () => {
  beforeEach(() => {
    getTemplates.mockReset();
    makeDefault.mockReset();
    deleteTemplate.mockReset();
    const { toasts, dismiss } = useToast();
    for (const toast of [...toasts.value]) dismiss(toast.id);
    getTemplates.mockResolvedValue(
      served([
        template({ id: "t-default", title: "Дефолтная", isDefault: true }),
        template({ id: "t-light", title: "Лайт" }),
      ]),
    );
    makeDefault.mockResolvedValue({ data: null, error: null });
    deleteTemplate.mockResolvedValue({ data: null, error: null });
  });

  it("marks the template a new game starts from", async () => {
    const wrapper = await render();

    expect(rowFor(wrapper, "Дефолтная").find(".template-default").text()).toBe(
      "по умолчанию",
    );
    expect(rowFor(wrapper, "Лайт").find(".template-default").exists()).toBe(
      false,
    );
  });

  it("leads every row to its own sheet and the new one to an empty sheet", async () => {
    const wrapper = await render();

    const edit = rowFor(wrapper, "Лайт")
      .findAll("a")
      .find((link) => link.text() === "Редактировать");
    expect(edit?.attributes("href")).toBe(
      "/moderation/schema-templates/t-light",
    );
    expect(wrapper.get(".templates-header a").attributes("href")).toBe(
      "/moderation/schema-templates/new",
    );
  });

  it("offers neither handover nor removal on the marked template", async () => {
    const wrapper = await render();

    expect(
      control(wrapper, "Дефолтная", "Сделать по умолчанию"),
    ).toBeUndefined();
    expect(control(wrapper, "Дефолтная", "Удалить")).toBeUndefined();
    expect(control(wrapper, "Лайт", "Сделать по умолчанию")).toBeDefined();
    expect(control(wrapper, "Лайт", "Удалить")).toBeDefined();
  });

  it("hands the mark over and re-reads the catalogue after it", async () => {
    const wrapper = await render();

    await control(wrapper, "Лайт", "Сделать по умолчанию")!.trigger("click");
    await flushPromises();

    expect(makeDefault).toHaveBeenCalledWith("t-light");
    expect(getTemplates).toHaveBeenCalledTimes(2);
  });

  it("stops the handover control while its request is in flight", async () => {
    let answer!: (result: { data: null; error: null }) => void;
    makeDefault.mockReturnValue(
      new Promise((resolve) => {
        answer = resolve;
      }),
    );
    const wrapper = await render();

    const handover = control(wrapper, "Лайт", "Сделать по умолчанию")!;
    await handover.trigger("click");

    // The wait is the answer plus the re-read, and the control wears it.
    expect(handover.attributes("disabled")).toBeDefined();
    expect(handover.attributes("aria-busy")).toBe("true");

    answer({ data: null, error: null });
    await flushPromises();

    const back = control(wrapper, "Лайт", "Сделать по умолчанию")!;
    expect(back.attributes("disabled")).toBeUndefined();
    expect(back.attributes("aria-busy")).toBeUndefined();
  });

  it("removes a template only once the removal is confirmed", async () => {
    const wrapper = await render();

    await control(wrapper, "Лайт", "Удалить")!.trigger("click");
    expect(deleteTemplate).not.toHaveBeenCalled();
    expect(windows().get(".dialog-message").text()).toContain("Лайт");

    await windows().get(".dialog-btn-submit").trigger("click");
    await flushPromises();

    // The window prints a refusal in itself, so the interceptor keeps quiet.
    expect(deleteTemplate).toHaveBeenCalledWith("t-light", OWNS_REFUSAL);
    expect(getTemplates).toHaveBeenCalledTimes(2);
    expect(windows().find(".dialog-container").exists()).toBe(false);
  });

  it("keeps the catalogue as it was and says the refusal in the window", async () => {
    deleteTemplate.mockResolvedValue(refused);
    const wrapper = await render();

    await control(wrapper, "Лайт", "Удалить")!.trigger("click");
    await windows().get(".dialog-btn-submit").trigger("click");
    await flushPromises();

    expect(windows().get(".dialog-refusal").text()).toBe(
      "Не удалось удалить шаблон",
    );
    expect(windows().find(".dialog-container").exists()).toBe(true);
    expect(plates()).toEqual([]);
    expect(getTemplates).toHaveBeenCalledTimes(1);
  });

  // The read owns its refusal; asserted on the request, where the interceptor reads it.
  it("takes the refusal of the read over from the interceptor", async () => {
    getTemplates.mockResolvedValue(closed);
    const wrapper = await render();

    expect(getTemplates).toHaveBeenCalledWith(OWNS_REFUSAL);
    // The line reads the server's reason, not a sentence of the page.
    expect(wrapper.find(".error-message").text()).toBe("Раздел закрыт");
    expect(wrapper.find(".templates-list").exists()).toBe(false);
  });

  // The 429 title carries no number, so the line says the seconds itself.
  it("names the wait when the read is rate limited", async () => {
    getTemplates.mockResolvedValue(limited);
    const wrapper = await render();

    expect(wrapper.find(".error-message").text()).toBe(
      "Слишком много попыток, попробуйте через 30 секунд",
    );
  });

  it("reads the catalogue again from the line's own retry", async () => {
    getTemplates
      .mockResolvedValueOnce(closed)
      .mockResolvedValueOnce(served([template()]));
    const wrapper = await render();

    await wrapper.get(".error-retry").trigger("click");
    await flushPromises();

    expect(wrapper.find(".error-state").exists()).toBe(false);
    expect(wrapper.get(".template-title").text()).toBe("Базовая анкета");
  });

  // Shown rows stay: the re-read after a handover of the mark does not blank the list.
  it("keeps the rows standing under aria-busy while it reads them again", async () => {
    getTemplates.mockResolvedValueOnce(served([template()]));
    makeDefault.mockResolvedValue({ data: null, error: null });
    const wrapper = await render();
    getTemplates.mockReturnValueOnce(new Promise(() => {}));

    const promote = wrapper
      .findAll(".template-actions button")
      .find((button) => button.text() === "Сделать по умолчанию")!;
    await promote.trigger("click");
    await flushPromises();

    expect(wrapper.get(".templates-list").attributes("aria-busy")).toBe("true");
    expect(wrapper.find(".template-row").exists()).toBe(true);
    expect(wrapper.text()).not.toContain("Загрузка...");
  });
});
