/**
 * @vitest-environment jsdom
 */

/**
 * One page answers both addresses of the section, so what it does depends on a
 * single fact: whether the address carries an identifier. That fact decides
 * two things a reader cannot see until it is too late - whether the sheet in
 * the editor is read from the server, and whether saving replaces a template
 * or adds one beside it. A page that always created would leave two templates
 * of one name, which the server refuses, and a page that always replaced would
 * need an identifier it does not have.
 *
 * The form itself is the shared editor and has its own spec; what belongs here
 * is what this host asks of it - a name field, no list of sources - and what
 * it does with the draft handed back.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { mount, flushPromises, enableAutoUnmount } from "@vue/test-utils";
import ModerationSchemaTemplate from "./ModerationSchemaTemplate.vue";

const getTemplate = vi.hoisted(() => vi.fn());
const createTemplate = vi.hoisted(() => vi.fn());
const updateTemplate = vi.hoisted(() => vi.fn());

vi.mock("@/entities/attribute-schema-template", () => ({
  attributeSchemaTemplateApi: { getTemplate, createTemplate, updateTemplate },
}));

/** The sheet the editor hands back when the moderator saves. */
const EDITED = vi.hoisted(() => ({
  title: "  Лайт  ",
  specifications: [{ id: "drafted", title: "Сила" }],
}));

/**
 * The editor stands in for its own spec: what matters here is the sheet it is
 * handed, the props this host asks it for, and the sheet it hands back.
 */
vi.mock("@/features/attribute-schema-editor", () => ({
  AttributeSchemaEditor: {
    // Typed rather than named: a valueless `show-title` is cast to true only
    // for a prop declared boolean, and the editor declares one.
    props: {
      modelValue: Object,
      showTitle: Boolean,
      sources: Array,
      saving: Boolean,
      refusal: String,
      save: Function,
    },
    emits: ["cancel"],
    template: `<div
        class="editor-stub"
        :data-title-field="String(Boolean(showTitle))"
        :data-sources="sources === undefined ? 'none' : String(sources.length)"
      >
        <span class="draft-title">{{ modelValue.title }}</span>
        <span class="spec-titles">{{
          modelValue.specifications.map((s) => s.title).join(", ")
        }}</span>
        <span class="refusal">{{ refusal }}</span>
        <button type="button" class="save" @click="save(edited)">
          Сохранить
        </button>
      </div>`,
    data: () => ({ edited: EDITED }),
  },
}));

/** The gate has its own spec; here the viewer is a senior moderator. */
const gate = vi.hoisted(() => ({ hasAccess: true }));

vi.mock("./lib/useRoleGate", async () => {
  const { ref } = await import("vue");
  return {
    useRoleGate: () => ({ hasAccess: ref(gate.hasAccess), deniedText: "" }),
  };
});

/**
 * The address, as the page reads it. A live route and not a snapshot: both
 * addresses of the section are one route record, so a step from one template
 * to another changes the parameter under a page that is already standing.
 */
const address = vi.hoisted(() => ({
  visit: (_params: Record<string, string>) => {},
}));
const push = vi.hoisted(() => vi.fn());

vi.mock("vue-router", async () => {
  const { reactive } = await import("vue");
  const route = reactive({ params: {} as Record<string, string> });
  address.visit = (params) => {
    route.params = params;
  };
  return {
    useRoute: () => route,
    useRouter: () => ({ push }),
  };
});

const toastError = vi.hoisted(() => vi.fn());

vi.mock("@/shared/lib/composables/useToast", () => ({
  useToast: () => ({ error: toastError, success: vi.fn() }),
}));

const served = {
  data: {
    resource: {
      id: "t-1",
      title: "Дефолтная",
      isDefault: true,
      specifications: [{ id: "s-1", title: "Мировоззрение" }],
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
      specifications: [{ id: "s-2", title: "Сила" }],
    },
  },
  error: null,
};

/** What the server answers a name that is already taken. */
const nameTaken = {
  data: null,
  error: { type: "Conflict", status: 409, title: "Такое название уже занято" },
};

/**
 * What the page says it will answer for on the read.
 *
 * On the read alone: a refused save leaves the form standing and its plate
 * belongs over it, while a refused read leaves an empty page whose only
 * sentence is the line where the editor would be.
 */
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

const CATALOGUE = { name: "moderation-schema-templates" };

/** The body both writes send: the name trimmed, the sheet as edited. */
const WRITTEN = { title: "Лайт", specifications: EDITED.specifications };

enableAutoUnmount(afterEach);

function render() {
  return mount(ModerationSchemaTemplate, {
    global: { stubs: { SecondaryText: true } },
  });
}

describe("the schema template page", () => {
  beforeEach(() => {
    getTemplate.mockReset();
    createTemplate.mockReset();
    updateTemplate.mockReset();
    toastError.mockReset();
    push.mockReset();
    gate.hasAccess = true;
    address.visit({});
    getTemplate.mockResolvedValue(served);
    createTemplate.mockResolvedValue({ data: null, error: null });
    updateTemplate.mockResolvedValue({ data: null, error: null });
    push.mockResolvedValue(undefined);
  });

  it("opens on an empty sheet at the address that creates one", async () => {
    const wrapper = render();
    await flushPromises();

    expect(getTemplate).not.toHaveBeenCalled();
    expect(wrapper.find(".draft-title").text()).toBe("");
    expect(wrapper.find(".spec-titles").text()).toBe("");
  });

  it("reads the template named by the address", async () => {
    address.visit({ id: "t-1" });
    const wrapper = render();
    await flushPromises();

    expect(getTemplate).toHaveBeenCalledWith("t-1", OWNS_REFUSAL);
    expect(wrapper.find(".draft-title").text()).toBe("Дефолтная");
    expect(wrapper.find(".spec-titles").text()).toBe("Мировоззрение");
  });

  it("asks for the name field and offers nothing to copy from", async () => {
    const wrapper = render();
    await flushPromises();

    const editor = wrapper.find(".editor-stub");
    expect(editor.attributes("data-title-field")).toBe("true");
    expect(editor.attributes("data-sources")).toBe("none");
  });

  it("writes a new template, then goes back to the catalogue", async () => {
    const wrapper = render();
    await flushPromises();

    await wrapper.find(".save").trigger("click");
    await flushPromises();

    expect(createTemplate).toHaveBeenCalledWith(WRITTEN);
    expect(push).toHaveBeenCalledWith(CATALOGUE);
  });

  it("replaces the template named by the address instead of adding one", async () => {
    address.visit({ id: "t-1" });
    const wrapper = render();
    await flushPromises();

    await wrapper.find(".save").trigger("click");
    await flushPromises();

    expect(createTemplate).not.toHaveBeenCalled();
    expect(updateTemplate).toHaveBeenCalledWith("t-1", WRITTEN);
    expect(push).toHaveBeenCalledWith(CATALOGUE);
  });

  it("follows the address when it moves to another template", async () => {
    address.visit({ id: "t-1" });
    const wrapper = render();
    await flushPromises();

    getTemplate.mockResolvedValue(servedNext);
    address.visit({ id: "t-2" });
    await flushPromises();

    expect(getTemplate).toHaveBeenLastCalledWith("t-2", OWNS_REFUSAL);
    expect(wrapper.find(".draft-title").text()).toBe("Лайт");

    await wrapper.find(".save").trigger("click");
    await flushPromises();

    // The sheet in the editor and the template the write replaces are the same
    // template, or the reader has just overwritten one with the other.
    expect(updateTemplate).toHaveBeenCalledWith("t-2", WRITTEN);
  });

  it("answers a name already taken inside the form", async () => {
    createTemplate.mockResolvedValue(nameTaken);
    const wrapper = render();
    await flushPromises();

    await wrapper.find(".save").trigger("click");
    await flushPromises();

    expect(wrapper.find(".refusal").text()).toBe(nameTaken.error.title);
    expect(toastError).not.toHaveBeenCalled();
    expect(push).not.toHaveBeenCalled();
  });

  /**
   * A failed read leaves no editor, so the line that replaces it is the one
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
    getTemplate.mockResolvedValue(closed);
    address.visit({ id: "t-1" });
    const wrapper = render();
    await flushPromises();

    expect(getTemplate).toHaveBeenCalledWith("t-1", OWNS_REFUSAL);
    // And the reason the server named is what the line reads, not a sentence
    // of the page's own.
    expect(wrapper.find(".error").text()).toBe("Шаблон закрыт");
    expect(wrapper.find(".editor-stub").exists()).toBe(false);
  });

  /**
   * The wait comes over with the refusal, so the line has to say what the plate
   * would have: the seconds the server asked for. The title sent with a 429
   * names no number, so relaying it would drop the one part a reader acts on.
   */
  it("names the wait when the read is rate limited", async () => {
    getTemplate.mockResolvedValue(limited);
    address.visit({ id: "t-1" });
    const wrapper = render();
    await flushPromises();

    expect(wrapper.find(".error").text()).toBe(
      "Слишком много попыток, попробуйте через 30 секунд",
    );
  });

  // Every refusal stands in the line under the editor now, not in a plate
  // (the owner's rule of 26 September 2026): the form is still on screen.
  it("says every other refusal in the same line, and in no plate", async () => {
    createTemplate.mockResolvedValue({
      data: null,
      error: { type: "NotFound", status: 404, title: "Ничего не найдено" },
    });
    const wrapper = render();
    await flushPromises();

    await wrapper.find(".save").trigger("click");
    await flushPromises();

    expect(toastError).not.toHaveBeenCalled();
    expect(wrapper.find(".refusal").text()).toBe("Ничего не найдено");
    expect(push).not.toHaveBeenCalled();
  });
});
