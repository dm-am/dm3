/**
 * @vitest-environment jsdom
 */

import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { mount, flushPromises, type VueWrapper } from "@vue/test-utils";
import { defineComponent, h } from "vue";
import { createMemoryHistory, createRouter, RouterView } from "vue-router";
import { useToast } from "@/shared/lib/composables/useToast";
import CharacterForm from "./CharacterForm.vue";
import { characterApi, AttributeSpecificationType } from "@/entities/game";
import { makeCharacter, makeSchema, makeSpec } from "./CharacterForm.fixtures";

beforeEach(() => {
  vi.clearAllMocks();
});

afterEach(() => {
  vi.restoreAllMocks();
});

const toast = useToast();
const saidOutLoud = () => toast.toasts.value.map((t) => t.message);

const SHEET = () =>
  makeSchema([
    makeSpec({
      id: "text-spec",
      title: "Род занятий",
      type: AttributeSpecificationType.Text,
      order: 1,
    }),
  ]);

const editProps = () => ({
  schema: SHEET(),
  characterId: "character-1",
  initialName: "Арагорн",
  initialValues: { "text-spec": "Следопыт" },
});

function serveUpdate() {
  return vi.spyOn(characterApi, "updateCharacter").mockResolvedValue({
    data: { resource: makeCharacter([]) },
    error: null,
  } as unknown as Awaited<ReturnType<typeof characterApi.updateCharacter>>);
}

function refuseUpdate(error: Record<string, unknown>) {
  return vi.spyOn(characterApi, "updateCharacter").mockResolvedValue({
    data: null,
    error,
  } as unknown as Awaited<ReturnType<typeof characterApi.updateCharacter>>);
}

const submitButton = (wrapper: VueWrapper) => wrapper.find(".controls button");

const answer = (wrapper: VueWrapper) => ({
  label: submitButton(wrapper).text(),
  inert: submitButton(wrapper).attributes("disabled") !== undefined,
});

async function save(wrapper: VueWrapper) {
  await wrapper.find("form").trigger("submit");
  await flushPromises();
}

describe("CharacterForm save confirmation", () => {
  beforeEach(() => {
    [...toast.toasts.value].forEach((t) => toast.dismiss(t.id));
  });

  it("does not claim a save the reader has not made", () => {
    const wrapper = mount(CharacterForm, { props: editProps() });

    expect(answer(wrapper)).toEqual({ label: "Сохранить", inert: false });
  });

  it("reads Сохранено and takes no press once the server confirms", async () => {
    serveUpdate();
    const wrapper = mount(CharacterForm, { props: editProps() });

    await save(wrapper);

    expect(answer(wrapper)).toEqual({ label: "Сохранено", inert: true });
  });

  it("says nothing in the corner about the save it has just shown", async () => {
    serveUpdate();
    const wrapper = mount(CharacterForm, { props: editProps() });

    await save(wrapper);

    expect(saidOutLoud()).toEqual([]);
  });

  it("offers the save again as soon as an attribute changes", async () => {
    serveUpdate();
    const wrapper = mount(CharacterForm, { props: editProps() });
    await save(wrapper);

    await wrapper.find("input#spec-text-spec").setValue("Следопыт из Арнора");

    expect(answer(wrapper)).toEqual({ label: "Сохранить", inert: false });
  });

  // The name is held apart from the values, so a rename has to unsave the sheet too.
  it("offers the save again as soon as the name changes", async () => {
    serveUpdate();
    const wrapper = mount(CharacterForm, { props: editProps() });
    await save(wrapper);

    await wrapper.find("input#character-name").setValue("Странник");

    expect(answer(wrapper)).toEqual({ label: "Сохранить", inert: false });
  });

  it("keeps offering the save after the server refuses it", async () => {
    refuseUpdate({ status: 400, type: "", title: "", traceId: "t" });
    const wrapper = mount(CharacterForm, { props: editProps() });

    await save(wrapper);

    expect(answer(wrapper)).toEqual({ label: "Сохранить", inert: false });
    expect(wrapper.find(".form-error").text()).toBe(
      "Не удалось сохранить персонажа",
    );
    expect(saidOutLoud()).toEqual([]);
  });

  it("keeps its own word on a character that has no id yet", async () => {
    vi.spyOn(characterApi, "createCharacter").mockResolvedValue({
      data: { resource: makeCharacter([]) },
      error: null,
    } as unknown as Awaited<ReturnType<typeof characterApi.createCharacter>>);
    const wrapper = mount(CharacterForm, {
      props: { schema: SHEET(), gameId: "game-1", initialName: "Арагорн" },
    });

    expect(answer(wrapper)).toEqual({
      label: "Создать персонажа",
      inert: false,
    });

    await save(wrapper);

    expect(submitButton(wrapper).text()).toBe("Создать персонажа");
  });
});

// A real router: the guard registers against the matched route record.
const Elsewhere = defineComponent({ setup: () => () => h("div", "elsewhere") });

const SheetPage = defineComponent({
  setup() {
    // Built once: a fresh schema on every render would be a new prop identity.
    const props = editProps();
    return () => h(CharacterForm, props);
  },
});

async function mountAtSheet() {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: "/sheet", component: SheetPage },
      { path: "/elsewhere", component: Elsewhere },
    ],
  });
  router.push("/sheet");
  await router.isReady();

  const wrapper = mount(defineComponent({ setup: () => () => h(RouterView) }), {
    global: { plugins: [router] },
  });
  await flushPromises();

  return { router, wrapper };
}

describe("CharacterForm unsaved-work guard", () => {
  it("lets an untouched sheet go without a word", async () => {
    const { router } = await mountAtSheet();

    await router.push("/elsewhere");

    expect(router.currentRoute.value.path).toBe("/elsewhere");
  });

  it("asks again about leaving when the sheet is edited after a save", async () => {
    serveUpdate();
    const { router, wrapper } = await mountAtSheet();
    await save(wrapper);

    await wrapper.find("input#spec-text-spec").setValue("Следопыт из Арнора");
    const leaving = router.push("/elsewhere");
    await flushPromises();

    expect(router.currentRoute.value.path).toBe("/sheet");
    expect(document.body.textContent).toContain("Уйти со страницы?");

    // The question stands in <body>: "Уйти" is its submit button.
    document.body
      .querySelector<HTMLButtonElement>(".dialog-btn-submit")!
      .click();
    await leaving;
    await flushPromises();
    expect(router.currentRoute.value.path).toBe("/elsewhere");
  });

  it("lets the sheet go without a word while it matches the save", async () => {
    serveUpdate();
    const { router, wrapper } = await mountAtSheet();

    await wrapper.find("input#spec-text-spec").setValue("Следопыт из Арнора");
    await save(wrapper);
    await router.push("/elsewhere");

    expect(router.currentRoute.value.path).toBe("/elsewhere");
  });
});

// The request flag is what the interceptor reads; the interceptor itself never runs here.
describe("CharacterForm refusal", () => {
  const OWNED = { ownsRefusal: true, ownsRateLimit: true };

  it("sends a save of an existing sheet with the refusal taken over", async () => {
    const update = serveUpdate();
    const wrapper = mount(CharacterForm, { props: editProps() });

    await save(wrapper);

    expect(update).toHaveBeenCalledWith(
      "character-1",
      expect.any(Object),
      OWNED,
    );
  });

  it("sends the creation of a sheet the same way", async () => {
    const create = vi.spyOn(characterApi, "createCharacter").mockResolvedValue({
      data: { resource: makeCharacter([]) },
      error: null,
    } as unknown as Awaited<ReturnType<typeof characterApi.createCharacter>>);
    const wrapper = mount(CharacterForm, {
      props: { schema: SHEET(), gameId: "game-1", initialName: "Арагорн" },
    });

    await save(wrapper);

    expect(create).toHaveBeenCalledWith("game-1", expect.any(Object), OWNED);
  });

  // The title the API sends with a rate limit names no number, so the strip says the seconds.
  it("names the wait under the fields when the save is rate limited", async () => {
    refuseUpdate({
      status: 429,
      type: "",
      title: "Слишком много запросов",
      traceId: "t",
      retryAfterSeconds: 30,
    });
    const wrapper = mount(CharacterForm, { props: editProps() });

    await save(wrapper);

    expect(wrapper.find(".form-error").text()).toBe(
      "Слишком много попыток, попробуйте через 30 секунд",
    );
  });

  it("reads out the reason the server named for a refusal", async () => {
    refuseUpdate({
      status: 403,
      type: "",
      title: "Мастер закрыл правку листов",
      traceId: "t",
    });
    const wrapper = mount(CharacterForm, { props: editProps() });

    await save(wrapper);

    expect(wrapper.find(".form-error").text()).toBe(
      "Мастер закрыл правку листов",
    );
  });

  // A 500 the strip took over must still be said, or the save fails in silence.
  it("says a broken server in the words the server titled it with", async () => {
    refuseUpdate({
      status: 500,
      type: "",
      title: "Ошибка сервера",
      traceId: "t",
    });
    const wrapper = mount(CharacterForm, { props: editProps() });

    await save(wrapper);

    expect(wrapper.find(".form-error").text()).toBe("Ошибка сервера");
  });

  // 401 and status 0 are the interceptor's alone: the strip adds no second sentence.
  it.each([
    ["an expired session", 401, "Сессия истекла"],
    ["a request that never came back", 0, ""],
  ])(
    "leaves %s to the plate and says nothing under the fields",
    async (_, status, title) => {
      refuseUpdate({ status, type: "", title, traceId: "t" });
      const wrapper = mount(CharacterForm, { props: editProps() });

      await save(wrapper);

      expect(wrapper.find(".form-error").exists()).toBe(false);
      expect(wrapper.text()).not.toContain("Не удалось сохранить персонажа");
      expect(answer(wrapper)).toEqual({ label: "Сохранить", inert: false });
    },
  );

  it("keeps the typed sheet and takes another press after a refusal", async () => {
    refuseUpdate({
      status: 403,
      type: "",
      title: "Мастер закрыл правку листов",
      traceId: "t",
    });
    const wrapper = mount(CharacterForm, { props: editProps() });
    await wrapper.find("input#character-name").setValue("Странник");
    await wrapper.find("input#spec-text-spec").setValue("Следопыт из Арнора");

    await save(wrapper);

    const typed = (selector: string) =>
      (wrapper.find(selector).element as HTMLInputElement).value;

    expect(typed("input#character-name")).toBe("Странник");
    expect(typed("input#spec-text-spec")).toBe("Следопыт из Арнора");
    expect(answer(wrapper)).toEqual({ label: "Сохранить", inert: false });
  });
});
