/**
 * @vitest-environment jsdom
 */

/**
 * The grant form of a contest series, and the one question it has to answer by
 * itself: why this grant was not accepted.
 *
 * A refusal has one carrier. The response interceptor speaks for the statuses
 * it knows more about than a press does - an expired session, a refused
 * action, a rate limit, a dead server, a request that never arrived - and it
 * relays the server's own sentence while doing it. The strip under this form
 * used to print that same sentence a second time, so a moderator who pressed
 * "Выдать" against a broken server read "Ошибка сервера" in the form with
 * "Ошибка сервера" standing in a plate above it.
 *
 * What is left for the form is the grant the server rejected by name - the
 * award is already on this user, the user does not exist - and those the form
 * must still carry, because they are about the two fields directly above it.
 */
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import {
  enableAutoUnmount,
  flushPromises,
  mount,
  type VueWrapper,
} from "@vue/test-utils";
import Button from "@/shared/ui/Button/Button.vue";
import { useToast } from "@/shared/lib/composables/useToast";
import ModerationAwardsSeries from "./ModerationAwardsSeries.vue";

const grantUserAward = vi.hoisted(() => vi.fn());
const getContestSeriesAwards = vi.hoisted(() => vi.fn());
const updateContestSeries = vi.hoisted(() => vi.fn());
const revokeUserAward = vi.hoisted(() => vi.fn());

/**
 * The catalogue this page reads the series and the award types out of. Both
 * slices are already loaded here: what is under test is the press, not the
 * lazy load behind it.
 */
vi.mock("@/entities/achievement", async (importOriginal) => {
  const { ref } = await import("vue");
  const { awardMetal, metalStyle } =
    await importOriginal<typeof import("@/entities/achievement")>();
  const adminSeries = ref([
    {
      id: "s-1",
      contestType: 0,
      number: 7,
      year: 2026,
      topicUrl: "/forum/topic/7",
      isActive: true,
    },
  ]);
  const awardTypes = ref([
    { id: "at-1", title: "Победитель", description: "первое место" },
  ]);
  return {
    achievementApi: {
      grantUserAward,
      getContestSeriesAwards,
      updateContestSeries,
      revokeUserAward,
    },
    useContestSeries: () => ({
      adminSeries,
      awardTypes,
      load: vi.fn().mockResolvedValue(undefined),
      loadAdmin: vi.fn().mockResolvedValue(undefined),
      reload: vi.fn().mockResolvedValue(undefined),
    }),
    formatContestSeriesTitle: (_type: number, number: number) =>
      `Литературный конкурс ${number}`,
    // The tier-to-metal mapping is pure; the page draws the grant icons with it.
    awardMetal,
    metalStyle,
  };
});

/** The name field is an autocomplete with its own spec; here it is an input. */
vi.mock("@/entities/user", () => ({
  UserAutocomplete: {
    props: ["modelValue", "id", "placeholder"],
    emits: ["update:modelValue"],
    template: `<input :id="id" class="username-stub" :value="modelValue"
      @input="$emit('update:modelValue', $event.target.value)" />`,
  },
  UserLink: {
    props: ["user"],
    template: `<span class="user-stub">{{ user.username }}</span>`,
  },
}));

/** The gate has its own spec; here the viewer is a moderator of awards. */
vi.mock("./lib/useRoleGate", async () => {
  const { ref } = await import("vue");
  return { useRoleGate: () => ({ hasAccess: ref(true), deniedText: "" }) };
});

const push = vi.hoisted(() => vi.fn());

vi.mock("vue-router", () => ({
  useRoute: () => ({ params: { id: "s-1" } }),
  useRouter: () => ({ push }),
}));

enableAutoUnmount(afterEach);

/**
 * The real Button, not a stub: the press is what this spec is about, and the
 * control decides for itself when it stops answering.
 */
function render() {
  return mount(ModerationAwardsSeries, {
    global: {
      components: { Button },
      stubs: {
        SecondaryText: true,
        BlockTitle: true,
        // The confirmation of a revoke is the real window, drawn in place.
        Teleport: true,
        GameIcon: true,
      },
    },
  });
}

/** The page with its lists read and the grant form filled in. */
async function filled() {
  const wrapper = render();
  await flushPromises();
  await wrapper.get(".username-stub").setValue("Гримуар");
  await wrapper.get("#grant-award-type").setValue("at-1");
  return wrapper;
}

/** The control the grant is sent with. */
const grantButton = (wrapper: VueWrapper) =>
  wrapper.findAll("button").find((button) => button.text() === "Выдать")!;

async function grant(wrapper: VueWrapper) {
  await grantButton(wrapper).trigger("click");
  await flushPromises();
}

/** What the corner of the screen says, if anything. */
const plates = () => useToast().toasts.value.map((toast) => toast.message);

describe("the contest series grant form", () => {
  beforeEach(() => {
    const { toasts, dismiss } = useToast();
    for (const toast of [...toasts.value]) dismiss(toast.id);
    vi.clearAllMocks();
    getContestSeriesAwards.mockResolvedValue({
      data: { resources: [] },
      error: null,
    });
    grantUserAward.mockResolvedValue({ data: null, error: null });
  });

  it("sends the grant the two fields describe", async () => {
    const wrapper = await filled();
    await grant(wrapper);

    expect(grantUserAward).toHaveBeenCalledWith("Гримуар", {
      awardTypeId: "at-1",
      contestSeriesId: "s-1",
      workUrl: null,
    });
    expect(wrapper.find(".form-error").exists()).toBe(false);
  });

  /**
   * The one class of failure this form owns. The server names the reason and
   * it is about the fields directly above the strip, so the fallback sentence
   * must not win over it.
   */
  it("answers a rejected grant in the form, in the server's words", async () => {
    grantUserAward.mockResolvedValue({
      data: null,
      error: {
        type: "Conflict",
        status: 409,
        title: "Эта награда у пользователя уже есть",
      },
    });
    const wrapper = await filled();
    await grant(wrapper);

    expect(wrapper.get(".form-error").text()).toBe(
      "Эта награда у пользователя уже есть",
    );
    expect(plates()).toEqual([]);

    // The form is still standing, with what was typed into it: the moderator
    // corrects one field rather than filling both again.
    expect(
      (wrapper.get(".username-stub").element as HTMLInputElement).value,
    ).toBe("Гримуар");
    expect(grantButton(wrapper).attributes("disabled")).toBeUndefined();
  });

  /**
   * The statuses the response interceptor used to plate - a refused action, a
   * rate limit, a dead server - stand in the strip under the form now, and the
   * plate is taken down (refusalHere; the owner's rule of 26 September 2026).
   * An expired session and a dead connection are about the reader and keep the
   * plate; the strip says nothing about them.
   *
   * No plate is raised in these tests either: the interceptor lives in the
   * axios instance and the api is mocked away here.
   */
  it.each([
    [403, "Ошибка сервера"],
    [429, "Слишком много попыток, попробуйте позже"],
    [500, "Ошибка сервера"],
    [503, "Ошибка сервера"],
  ])(
    "says a grant refused with status %i in the strip",
    async (status, line) => {
      grantUserAward.mockResolvedValue({
        data: null,
        error: { type: "Failure", status, title: "Ошибка сервера" },
      });
      const wrapper = await filled();
      await grant(wrapper);

      expect(wrapper.get(".form-error").text()).toBe(line);
      expect(plates()).toEqual([]);
      expect(grantButton(wrapper).attributes("disabled")).toBeUndefined();
    },
  );

  it.each([0, 401])(
    "leaves a grant refused with status %i to the interceptor",
    async (status) => {
      grantUserAward.mockResolvedValue({
        data: null,
        error: { type: "Failure", status, title: "Ошибка сервера" },
      });
      const wrapper = await filled();
      await grant(wrapper);

      expect(grantUserAward).toHaveBeenCalled();
      expect(wrapper.find(".form-error").exists()).toBe(false);
      expect(plates()).toEqual([]);
      // Silent about the failure, but not about the grant: it never happened,
      // so the form still offers it.
      expect(grantButton(wrapper).attributes("disabled")).toBeUndefined();
    },
  );

  it("takes the refusal off the screen when the grant is sent again", async () => {
    grantUserAward.mockResolvedValue({
      data: null,
      error: { type: "Conflict", status: 409, title: "Уже выдана" },
    });
    const wrapper = await filled();
    await grant(wrapper);
    expect(wrapper.find(".form-error").exists()).toBe(true);

    grantUserAward.mockResolvedValue({ data: null, error: null });
    await grant(wrapper);

    expect(wrapper.find(".form-error").exists()).toBe(false);
  });
});

/**
 * The list of grants under the form, and why it did not arrive.
 *
 * A failed load is the state of the block and not a plate over it: there is
 * no list underneath to go back to, and a plate in the corner over an empty
 * block explains nothing (UI_STANDARDS, "Всплывающие сообщения"). The api
 * method has no seam for `ownsRefusal`, so the block keeps quiet on the
 * statuses the interceptor announces - and claims no empty series for them
 * either, because "еще не выдавались" over a dead server is a lie - and prints
 * the rest where the list would stand, in the server's words.
 *
 * The grant form is never pressed with a refusal of its own here, so the one
 * `.form-error` on the page is the list's.
 */
describe("the list of grants", () => {
  const EMPTY_SERIES = "Награды в этой серии еще не выдавались";

  const refusedList = (status: number, title = "") => ({
    data: null,
    error: { type: "", title, status, traceId: "t" },
  });

  const award = {
    id: "a-1",
    type: { iconName: "trophy", title: "Победитель" },
    user: { username: "Гримуар" },
    awardedUtc: "2026-09-01T10:00:00Z",
  };

  /** The page with the empty state readable: the stub in `render` swallows it. */
  function renderReadable() {
    return mount(ModerationAwardsSeries, {
      global: {
        components: { Button },
        stubs: {
          SecondaryText: { template: "<p class='secondary'><slot /></p>" },
          BlockTitle: true,
          // The confirmation of a revoke is the real window, drawn in place.
          Teleport: true,
          GameIcon: true,
        },
      },
    });
  }

  beforeEach(() => {
    const { toasts, dismiss } = useToast();
    for (const toast of [...toasts.value]) dismiss(toast.id);
    getContestSeriesAwards.mockReset();
    grantUserAward.mockReset();
    grantUserAward.mockResolvedValue({ data: null, error: null });
  });

  it("prints why the list did not arrive where the list would stand, in the server's words", async () => {
    getContestSeriesAwards.mockResolvedValue(
      refusedList(404, "Серия не найдена"),
    );

    const wrapper = renderReadable();
    await flushPromises();

    const line = wrapper.get(".form-error");
    expect(line.text()).toBe("Серия не найдена");
    expect(line.attributes("role")).toBe("alert");
    expect(wrapper.text()).not.toContain(EMPTY_SERIES);
    expect(plates()).toEqual([]);
  });

  it("falls back to its own sentence when the server named nothing", async () => {
    getContestSeriesAwards.mockResolvedValue(refusedList(404));

    const wrapper = renderReadable();
    await flushPromises();

    expect(wrapper.get(".form-error").text()).toBe(
      "Не удалось загрузить выданные награды",
    );
  });

  it.each([
    [403, "Ошибка сервера"],
    [429, "Слишком много попыток, попробуйте позже"],
    [500, "Ошибка сервера"],
    [503, "Ошибка сервера"],
  ])(
    "says a load refused with status %i where the list stands, and claims no empty series",
    async (status, line) => {
      getContestSeriesAwards.mockResolvedValue(
        refusedList(status, "Ошибка сервера"),
      );

      const wrapper = renderReadable();
      await flushPromises();

      expect(wrapper.get(".form-error").text()).toBe(line);
      expect(wrapper.text()).not.toContain(EMPTY_SERIES);
      expect(plates()).toEqual([]);
    },
  );

  it.each([0, 401])(
    "leaves a load refused with status %i to the interceptor, and claims no empty series",
    async (status) => {
      getContestSeriesAwards.mockResolvedValue(
        refusedList(status, "Ошибка сервера"),
      );

      const wrapper = renderReadable();
      await flushPromises();

      expect(wrapper.find(".form-error").exists()).toBe(false);
      expect(wrapper.text()).not.toContain(EMPTY_SERIES);
      expect(plates()).toEqual([]);
    },
  );

  it("claims an empty series only when the server answered one", async () => {
    getContestSeriesAwards.mockResolvedValue({
      data: { resources: [] },
      error: null,
    });

    const wrapper = renderReadable();
    await flushPromises();

    expect(wrapper.text()).toContain(EMPTY_SERIES);
    expect(wrapper.find(".form-error").exists()).toBe(false);
  });

  it("takes the refusal off the screen when the list is read again", async () => {
    getContestSeriesAwards
      .mockResolvedValueOnce(refusedList(404))
      .mockResolvedValueOnce({ data: { resources: [award] }, error: null });
    const wrapper = renderReadable();
    await flushPromises();
    expect(wrapper.find(".form-error").exists()).toBe(true);

    // A grant that went through re-reads the list.
    await wrapper.get(".username-stub").setValue("Гримуар");
    await wrapper.get("#grant-award-type").setValue("at-1");
    await grant(wrapper);

    expect(wrapper.find(".form-error").exists()).toBe(false);
    expect(wrapper.get(".grant-row").text()).toContain("Победитель");
  });
});

/**
 * Revoking an award. The confirmation runs the request itself: it waits for
 * the answer, prints a refusal in itself and closes on success alone, and the
 * request carries the flags that keep the interceptor quiet about it.
 */
describe("revoking an award", () => {
  const award = {
    id: "a-1",
    type: { iconName: "trophy", title: "Победитель" },
    user: { username: "Гримуар" },
    awardedUtc: "2026-09-01T10:00:00Z",
  };

  beforeEach(() => {
    const { toasts, dismiss } = useToast();
    for (const toast of [...toasts.value]) dismiss(toast.id);
    revokeUserAward.mockReset();
    getContestSeriesAwards.mockReset();
    getContestSeriesAwards.mockResolvedValue({
      data: { resources: [award] },
      error: null,
    });
  });

  async function revoke(answer: unknown) {
    revokeUserAward.mockResolvedValue(answer);
    const wrapper = render();
    await flushPromises();
    await wrapper.get(".grant-row .text-button").trigger("click");
    await wrapper.get(".dialog-btn-submit").trigger("click");
    await flushPromises();
    return wrapper;
  }

  it("takes the row off with the window's flags and closes", async () => {
    const wrapper = await revoke({ data: null, error: null });

    expect(revokeUserAward).toHaveBeenCalledWith("Гримуар", "a-1", {
      ownsRefusal: true,
      ownsRateLimit: true,
    });
    expect(wrapper.find(".grant-row").exists()).toBe(false);
    expect(wrapper.find(".dialog-container").exists()).toBe(false);
  });

  it("prints a refused revoke in the window and keeps the row", async () => {
    const wrapper = await revoke({
      data: null,
      error: { type: "", title: "", status: 404, traceId: "t" },
    });

    expect(wrapper.get(".dialog-refusal").text()).toBe(
      "Не удалось отозвать награду",
    );
    expect(wrapper.find(".dialog-container").exists()).toBe(true);
    expect(wrapper.find(".grant-row").exists()).toBe(true);
    expect(plates()).toEqual([]);
  });
});
