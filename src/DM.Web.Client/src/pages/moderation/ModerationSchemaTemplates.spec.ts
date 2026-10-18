/**
 * @vitest-environment jsdom
 */

/**
 * The catalogue of starting character sheets, as the moderator works with it.
 *
 * Three facts of the section are the section: the mark that says which sheet a
 * new game opens with, the handover of that mark, and a removal that asks
 * first. The mark is the reason the two write controls of a row are not the
 * same on every row - the template that carries it cannot be removed at all,
 * and the server says so with a refusal, so a row offering the control would
 * be offering a refusal.
 *
 * The handover is checked by the re-read rather than by the row: the mark
 * moves between two rows and only the server knows which one lost it, so a
 * page that flipped the clicked row would be drawing a list it had made up.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { mount, flushPromises, enableAutoUnmount } from "@vue/test-utils";
import type { VueWrapper } from "@vue/test-utils";
import ModerationSchemaTemplates from "./ModerationSchemaTemplates.vue";

const getTemplates = vi.hoisted(() => vi.fn());
const makeDefault = vi.hoisted(() => vi.fn());
const deleteTemplate = vi.hoisted(() => vi.fn());

vi.mock("@/entities/attribute-schema-template", () => ({
  attributeSchemaTemplateApi: { getTemplates, makeDefault, deleteTemplate },
}));

/** The gate has its own spec; here the viewer is a senior moderator. */
const gate = vi.hoisted(() => ({ hasAccess: true }));

vi.mock("./lib/useRoleGate", async () => {
  const { ref } = await import("vue");
  return {
    useRoleGate: () => ({ hasAccess: ref(gate.hasAccess), deniedText: "" }),
  };
});

const toastError = vi.hoisted(() => vi.fn());

vi.mock("@/shared/lib/composables/useToast", () => ({
  useToast: () => ({ error: toastError, success: vi.fn() }),
}));

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

function render() {
  return mount(ModerationSchemaTemplates, {
    global: {
      stubs: {
        SecondaryText: true,
        // The confirmation is the real window, drawn in place: the removal is
        // its request, and a refusal is printed in it.
        Teleport: true,
        RouterLink: {
          props: ["to"],
          template: "<a><slot /></a>",
        },
      },
    },
  });
}

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
    toastError.mockReset();
    gate.hasAccess = true;
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
    const wrapper = render();
    await flushPromises();

    expect(rowFor(wrapper, "Дефолтная").find(".template-default").text()).toBe(
      "по умолчанию",
    );
    expect(rowFor(wrapper, "Лайт").find(".template-default").exists()).toBe(
      false,
    );
  });

  it("offers neither handover nor removal on the marked template", async () => {
    const wrapper = render();
    await flushPromises();

    expect(
      control(wrapper, "Дефолтная", "Сделать по умолчанию"),
    ).toBeUndefined();
    expect(control(wrapper, "Дефолтная", "Удалить")).toBeUndefined();
    expect(control(wrapper, "Лайт", "Сделать по умолчанию")).toBeDefined();
    expect(control(wrapper, "Лайт", "Удалить")).toBeDefined();
  });

  it("hands the mark over and re-reads the catalogue after it", async () => {
    const wrapper = render();
    await flushPromises();

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
    const wrapper = render();
    await flushPromises();

    const handover = control(wrapper, "Лайт", "Сделать по умолчанию")!;
    await handover.trigger("click");

    // The wait here is the answer plus the re-read that follows it, and the
    // control says nothing about either unless it wears the state.
    expect(handover.attributes("disabled")).toBeDefined();
    expect(handover.attributes("aria-busy")).toBe("true");

    answer({ data: null, error: null });
    await flushPromises();

    const back = control(wrapper, "Лайт", "Сделать по умолчанию")!;
    expect(back.attributes("disabled")).toBeUndefined();
    expect(back.attributes("aria-busy")).toBeUndefined();
  });

  it("removes a template only once the removal is confirmed", async () => {
    const wrapper = render();
    await flushPromises();

    await control(wrapper, "Лайт", "Удалить")!.trigger("click");
    expect(deleteTemplate).not.toHaveBeenCalled();
    expect(wrapper.find(".dialog-message").text()).toContain("Лайт");

    await wrapper.find(".dialog-btn-submit").trigger("click");
    await flushPromises();

    // The window prints a refusal in itself, so the interceptor keeps quiet.
    expect(deleteTemplate).toHaveBeenCalledWith("t-light", OWNS_REFUSAL);
    expect(getTemplates).toHaveBeenCalledTimes(2);
    expect(wrapper.find(".dialog-container").exists()).toBe(false);
  });

  it("keeps the catalogue as it was and says the refusal in the window", async () => {
    deleteTemplate.mockResolvedValue(refused);
    const wrapper = render();
    await flushPromises();

    await control(wrapper, "Лайт", "Удалить")!.trigger("click");
    await wrapper.find(".dialog-btn-submit").trigger("click");
    await flushPromises();

    expect(wrapper.get(".dialog-refusal").text()).toBe(
      "Не удалось удалить шаблон",
    );
    expect(wrapper.find(".dialog-container").exists()).toBe(true);
    expect(toastError).not.toHaveBeenCalled();
    expect(getTemplates).toHaveBeenCalledTimes(1);
  });

  it("asks for nothing on behalf of a viewer it refuses", async () => {
    gate.hasAccess = false;
    render();
    await flushPromises();

    expect(getTemplates).not.toHaveBeenCalled();
  });

  /**
   * A failed read leaves no list, so the line that replaces it is the one
   * carrier of the failure, and the response interceptor used to raise a plate
   * over it saying the same thing - on a 403 and a 5xx the identical sentence,
   * since both read it out of the same problem document. The read says on the
   * way out that this page answers for it, which is the only way the
   * interceptor can know.
   *
   * Asserted on the request rather than on the plate, because that is where the
   * decision is: the interceptor lives below the mock this suite installs.
   */
  it("takes the refusal of the read over from the interceptor", async () => {
    getTemplates.mockResolvedValue(closed);
    const wrapper = render();
    await flushPromises();

    expect(getTemplates).toHaveBeenCalledWith(OWNS_REFUSAL);
    // And the reason the server named is what the line reads, not a sentence
    // of the page's own.
    expect(wrapper.find(".error").text()).toBe("Раздел закрыт");
    expect(wrapper.find(".templates-list").exists()).toBe(false);
  });

  /**
   * The wait comes over with the refusal, so the line has to say what the plate
   * would have: the seconds the server asked for. The title sent with a 429
   * names no number, so relaying it would drop the one part a reader acts on.
   */
  it("names the wait when the read is rate limited", async () => {
    getTemplates.mockResolvedValue(limited);
    const wrapper = render();
    await flushPromises();

    expect(wrapper.find(".error").text()).toBe(
      "Слишком много попыток, попробуйте через 30 секунд",
    );
  });
});
