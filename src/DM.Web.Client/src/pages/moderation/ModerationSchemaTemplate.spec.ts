/**
 * @vitest-environment jsdom
 */

// One page answers both addresses: an id in the address decides whether the
// sheet is read and whether saving replaces a template or adds one beside it.
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import {
  flushPromises,
  enableAutoUnmount,
  type VueWrapper,
} from "@vue/test-utils";
import type { Router } from "vue-router";
import { AttributeSpecificationType } from "@/shared/api/models/game/attributeSchemas";
import { useToast } from "@/shared/lib/composables/useToast";
import ModerationSchemaTemplate from "./ModerationSchemaTemplate.vue";
import { mountPage, type Address } from "@/shared/lib/testing/mountPage";

const { getTemplate, createTemplate, updateTemplate } = vi.hoisted(() => ({
  getTemplate: vi.fn(),
  createTemplate: vi.fn(),
  updateTemplate: vi.fn(),
}));

// The network only: the router, the shared editor and the plates are the real ones.
vi.mock(
  "@/entities/attribute-schema-template/api/attributeSchemaTemplateApi",
  () => ({ default: { getTemplate, createTemplate, updateTemplate } }),
);

/** One attribute of a sheet, as the server holds it. */
const spec = (id: string, title: string) => ({
  id,
  title,
  required: false,
  type: AttributeSpecificationType.Text,
  order: 0,
  isDescriptor: false,
  isHidden: false,
  maxLength: null,
  values: [],
});

const served = {
  data: {
    resource: {
      id: "t-1",
      title: "Дефолтная",
      isDefault: true,
      specifications: [spec("s-1", "Мировоззрение")],
    },
  },
  error: null,
};

/** Another template, the one the address moves on to. */
const servedNext = {
  data: {
    resource: {
      id: "t-2",
      title: "Лайт",
      isDefault: false,
      specifications: [spec("s-2", "Сила")],
    },
  },
  error: null,
};

/** What the server answers a name that is already taken. */
const nameTaken = {
  data: null,
  error: { type: "Conflict", status: 409, title: "Такое название уже занято" },
};

/** What the read owns; a refused save leaves the form standing and its plate over it. */
const OWNS_REFUSAL = { ownsRefusal: true, ownsRateLimit: true };

/** A read the server turned down, with the reason it named. */
const closed = {
  data: null,
  error: { type: "Forbidden", status: 403, title: "Шаблон закрыт" },
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

const NEW = "/moderation/schema-templates/new";
const CATALOGUE = "/moderation/schema-templates";

enableAutoUnmount(afterEach);

/** The catalogue and the two addresses of one sheet. */
const TEMPLATE_ADDRESSES: Address[] = [
  ["moderation-schema-templates", "/moderation/schema-templates"],
  ["moderation-schema-template-new", "/moderation/schema-templates/new"],
  ["moderation-schema-template", "/moderation/schema-templates/:id"],
];

let router: Router;

async function render(address = NEW) {
  const mounted = await mountPage(ModerationSchemaTemplate, address, {
    routes: TEMPLATE_ADDRESSES,
  });
  router = mounted.router;
  await flushPromises();
  return mounted.wrapper;
}

/** Where the reader is now. */
const address = () => router.currentRoute.value.fullPath;

/** What the corner of the screen says, if anything. */
const plates = () => useToast().toasts.value.map((toast) => toast.message);

const titleField = (wrapper: VueWrapper) =>
  wrapper.find<HTMLInputElement>("#schema-title");

const attributes = (wrapper: VueWrapper) =>
  wrapper.findAll(".attr-row .col-name").map((cell) => cell.text());

/** The name typed padded, as a moderator might, and the sheet saved. */
async function saveAs(wrapper: VueWrapper, title: string) {
  await titleField(wrapper).setValue(title);
  const save = wrapper
    .findAll(".editor-actions button")
    .find((button) => button.text() === "Сохранить")!;
  await save.trigger("click");
  await flushPromises();
}

describe("the schema template page", () => {
  beforeEach(() => {
    const { toasts, dismiss } = useToast();
    for (const toast of [...toasts.value]) dismiss(toast.id);
    getTemplate.mockReset();
    createTemplate.mockReset();
    updateTemplate.mockReset();
    getTemplate.mockResolvedValue(served);
    createTemplate.mockResolvedValue({ data: null, error: null });
    updateTemplate.mockResolvedValue({ data: null, error: null });
  });

  it("opens on an empty sheet at the address that creates one", async () => {
    const wrapper = await render();

    expect(getTemplate).not.toHaveBeenCalled();
    expect(titleField(wrapper).element.value).toBe("");
    expect(attributes(wrapper)).toEqual([]);
  });

  it("reads the template named by the address", async () => {
    const wrapper = await render("/moderation/schema-templates/t-1");

    expect(getTemplate).toHaveBeenCalledWith("t-1", OWNS_REFUSAL);
    expect(titleField(wrapper).element.value).toBe("Дефолтная");
    expect(attributes(wrapper)).toEqual(["Мировоззрение"]);
  });

  it("asks for the name field and offers nothing to copy from", async () => {
    const wrapper = await render();

    expect(titleField(wrapper).exists()).toBe(true);
    expect(wrapper.find("#schema-source").exists()).toBe(false);
  });

  it("writes a new template, then goes back to the catalogue", async () => {
    const wrapper = await render();

    await saveAs(wrapper, "  Лайт  ");

    expect(createTemplate).toHaveBeenCalledWith({
      title: "  Лайт  ",
      specifications: [],
    });
    expect(address()).toBe(CATALOGUE);
  });

  it("replaces the template named by the address instead of adding one", async () => {
    const wrapper = await render("/moderation/schema-templates/t-1");

    await saveAs(wrapper, "  Лайт  ");

    expect(createTemplate).not.toHaveBeenCalled();
    expect(updateTemplate).toHaveBeenCalledWith("t-1", {
      title: "  Лайт  ",
      specifications: [spec("s-1", "Мировоззрение")],
    });
    expect(address()).toBe(CATALOGUE);
  });

  it("follows the address when it moves to another template", async () => {
    const wrapper = await render("/moderation/schema-templates/t-1");

    getTemplate.mockResolvedValue(servedNext);
    await router.push("/moderation/schema-templates/t-2");
    await flushPromises();

    expect(getTemplate).toHaveBeenLastCalledWith("t-2", OWNS_REFUSAL);
    expect(titleField(wrapper).element.value).toBe("Лайт");

    await saveAs(wrapper, "Лайт");

    // The sheet in the editor and the template replaced are the same one.
    expect(updateTemplate).toHaveBeenCalledWith("t-2", {
      title: "Лайт",
      specifications: [spec("s-2", "Сила")],
    });
  });

  it("answers a name already taken inside the form", async () => {
    createTemplate.mockResolvedValue(nameTaken);
    const wrapper = await render();

    await saveAs(wrapper, "Лайт");

    expect(wrapper.get(".schema-editor > .form-error").text()).toBe(
      nameTaken.error.title,
    );
    expect(plates()).toEqual([]);
    expect(address()).toBe(NEW);
  });

  // The read owns its refusal; asserted on the request, where the interceptor reads it.
  it("takes the refusal of the read over from the interceptor", async () => {
    getTemplate.mockResolvedValue(closed);
    const wrapper = await render("/moderation/schema-templates/t-1");

    expect(getTemplate).toHaveBeenCalledWith("t-1", OWNS_REFUSAL);
    // The line reads the server's reason, not a sentence of the page.
    expect(wrapper.find(".error-message").text()).toBe("Шаблон закрыт");
    expect(wrapper.find(".schema-editor").exists()).toBe(false);
  });

  // The 429 title carries no number, so the line says the seconds itself.
  it("names the wait when the read is rate limited", async () => {
    getTemplate.mockResolvedValue(limited);
    const wrapper = await render("/moderation/schema-templates/t-1");

    expect(wrapper.find(".error-message").text()).toBe(
      "Слишком много попыток, попробуйте через 30 секунд",
    );
  });

  // A refused save stands in the line under the editor: the form is still on screen.
  it("says every other refusal in the same line, and in no plate", async () => {
    createTemplate.mockResolvedValue({
      data: null,
      error: { type: "NotFound", status: 404, title: "Ничего не найдено" },
    });
    const wrapper = await render();

    await saveAs(wrapper, "Лайт");

    expect(plates()).toEqual([]);
    expect(wrapper.get(".schema-editor > .form-error").text()).toBe(
      "Ничего не найдено",
    );
    expect(address()).toBe(NEW);
  });
});
