/**
 * @vitest-environment jsdom
 */

// The grant form answers in its strip what the server rejected by name (the
// award is already there, the user does not exist), and every other refusal.
import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import {
  enableAutoUnmount,
  flushPromises,
  type VueWrapper,
} from "@vue/test-utils";
import { useToast } from "@/shared/lib/composables/useToast";
import ModerationAwardsSeries from "./ModerationAwardsSeries.vue";
import { mountPage, windows } from "@/shared/lib/testing/mountPage";

const api = vi.hoisted(() => {
  const listed = <T>(resources: T[]) =>
    Promise.resolve({ data: { resources }, error: null });
  const series = [
    {
      id: "s-1",
      contestType: 0,
      number: 7,
      year: 2026,
      topicUrl: "/forum/topic/7",
      isActive: true,
    },
  ];
  const awardTypes = [
    { id: "at-1", title: "Победитель", description: "первое место" },
  ];
  return {
    getAwardTypes: () => listed(awardTypes),
    getContestSeries: () => listed(series),
    getModerationAwardTypes: () => listed(awardTypes),
    getModerationContestSeries: () => listed(series),
    grantUserAward: vi.fn(),
    getContestSeriesAwards: vi.fn(),
    updateContestSeries: vi.fn(),
    revokeUserAward: vi.fn(),
  };
});

const searchUsers = vi.hoisted(() => vi.fn());

// The network only: the catalogs, the name field and the windows are the real ones.
vi.mock("@/entities/achievement/api/achievementApi", () => ({ default: api }));
vi.mock("@/entities/user/api/userApi", () => ({ default: { searchUsers } }));

enableAutoUnmount(afterEach);

async function render() {
  const { wrapper } = await mountPage(
    ModerationAwardsSeries,
    "/moderation/awards/series/s-1",
    {
      routes: [
        ["moderation-awards-series", "/moderation/awards/series/:id"],
        ["moderation-awards", "/moderation/awards"],
        ["profile", "/users/:username"],
      ],
    },
  );
  await flushPromises();
  return wrapper;
}

/** The name picked from what the field offers for it. */
async function pickUser(wrapper: VueWrapper, username: string) {
  const field = wrapper.get("#grant-username");
  await field.trigger("focus");
  await field.setValue(username);
  const offered = await vi.waitFor(() =>
    wrapper.get(".user-autocomplete .autocomplete-item"),
  );
  await offered.trigger("mousedown");
}

/** The page with its lists read and the grant form filled in. */
async function filled() {
  const wrapper = await render();
  await pickUser(wrapper, "Гримуар");
  await wrapper.get("#grant-award-type").setValue("at-1");
  return wrapper;
}

const pickedUser = (wrapper: VueWrapper) =>
  wrapper.find(".user-autocomplete .selected-user .user-username");

const grantButton = (wrapper: VueWrapper) =>
  wrapper.findAll("button").find((button) => button.text() === "Выдать")!;

async function grant(wrapper: VueWrapper) {
  await grantButton(wrapper).trigger("click");
  await flushPromises();
}

/** What the corner of the screen says, if anything. */
const plates = () => useToast().toasts.value.map((toast) => toast.message);

function clearPlates() {
  const { toasts, dismiss } = useToast();
  for (const toast of [...toasts.value]) dismiss(toast.id);
}

beforeEach(() => {
  clearPlates();
  vi.clearAllMocks();
  searchUsers.mockResolvedValue({
    data: { resources: [{ id: "u-1", username: "Гримуар" }] },
    error: null,
  });
});

describe("the contest series grant form", () => {
  beforeEach(() => {
    api.getContestSeriesAwards.mockResolvedValue({
      data: { resources: [] },
      error: null,
    });
    api.grantUserAward.mockResolvedValue({ data: null, error: null });
  });

  it("sends the grant the two fields describe", async () => {
    const wrapper = await filled();
    await grant(wrapper);

    expect(api.grantUserAward).toHaveBeenCalledWith("Гримуар", {
      awardTypeId: "at-1",
      contestSeriesId: "s-1",
      workUrl: null,
    });
    expect(wrapper.find(".form-error").exists()).toBe(false);
    // A grant that went through clears the form for the next one.
    expect(pickedUser(wrapper).exists()).toBe(false);
  });

  // The reason is about the fields above the strip; the fallback must not win over it.
  it("answers a rejected grant in the form, in the server's words", async () => {
    api.grantUserAward.mockResolvedValue({
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

    // What was picked stays, so the moderator corrects one field.
    expect(pickedUser(wrapper).text()).toBe("Гримуар");
    expect(grantButton(wrapper).attributes("disabled")).toBeUndefined();
  });

  it("says a refused grant in the strip", async () => {
    api.grantUserAward.mockResolvedValue({
      data: null,
      error: { type: "Failure", status: 500, title: "Ошибка сервера" },
    });
    const wrapper = await filled();
    await grant(wrapper);

    expect(wrapper.get(".form-error").text()).toBe("Ошибка сервера");
    expect(plates()).toEqual([]);
    expect(grantButton(wrapper).attributes("disabled")).toBeUndefined();
  });

  it.each([0, 401])(
    "leaves a grant refused with status %i to the interceptor",
    async (status) => {
      api.grantUserAward.mockResolvedValue({
        data: null,
        error: { type: "Failure", status, title: "Ошибка сервера" },
      });
      const wrapper = await filled();
      await grant(wrapper);

      expect(api.grantUserAward).toHaveBeenCalled();
      expect(wrapper.find(".form-error").exists()).toBe(false);
      expect(plates()).toEqual([]);
      // The grant never happened, so the form still offers it.
      expect(grantButton(wrapper).attributes("disabled")).toBeUndefined();
    },
  );

  it("takes the refusal off the screen when the grant is sent again", async () => {
    api.grantUserAward.mockResolvedValue({
      data: null,
      error: { type: "Conflict", status: 409, title: "Уже выдана" },
    });
    const wrapper = await filled();
    await grant(wrapper);
    expect(wrapper.find(".form-error").exists()).toBe(true);

    api.grantUserAward.mockResolvedValue({ data: null, error: null });
    await grant(wrapper);

    expect(wrapper.find(".form-error").exists()).toBe(false);
  });
});

// The grants list and the revoke read this award.
const award = {
  id: "a-1",
  type: { iconName: "trophy", title: "Победитель" },
  user: { username: "Гримуар" },
  awardedUtc: "2026-09-01T10:00:00Z",
};

describe("the list of grants", () => {
  const EMPTY_SERIES = "Награды в этой серии еще не выдавались";

  const refusedList = (status: number, title = "") => ({
    data: null,
    error: { type: "", title, status, traceId: "t" },
  });

  beforeEach(() => {
    api.grantUserAward.mockResolvedValue({ data: null, error: null });
  });

  it("prints why the list did not arrive where the list would stand, in the server's words", async () => {
    api.getContestSeriesAwards.mockResolvedValue(
      refusedList(404, "Серия не найдена"),
    );

    const wrapper = await render();

    const line = wrapper.get(".form-error");
    expect(line.text()).toBe("Серия не найдена");
    expect(line.attributes("role")).toBe("alert");
    expect(wrapper.text()).not.toContain(EMPTY_SERIES);
    expect(plates()).toEqual([]);
  });

  it("falls back to its own sentence when the server named nothing", async () => {
    api.getContestSeriesAwards.mockResolvedValue(refusedList(404));

    const wrapper = await render();

    expect(wrapper.get(".form-error").text()).toBe(
      "Не удалось загрузить выданные награды",
    );
  });

  it("says a refused load where the list stands, and claims no empty series", async () => {
    api.getContestSeriesAwards.mockResolvedValue(
      refusedList(500, "Ошибка сервера"),
    );

    const wrapper = await render();

    expect(wrapper.get(".form-error").text()).toBe("Ошибка сервера");
    expect(wrapper.text()).not.toContain(EMPTY_SERIES);
    expect(plates()).toEqual([]);
  });

  it.each([0, 401])(
    "leaves a load refused with status %i to the interceptor, and claims no empty series",
    async (status) => {
      api.getContestSeriesAwards.mockResolvedValue(
        refusedList(status, "Ошибка сервера"),
      );

      const wrapper = await render();

      expect(wrapper.find(".form-error").exists()).toBe(false);
      expect(wrapper.text()).not.toContain(EMPTY_SERIES);
      expect(plates()).toEqual([]);
    },
  );

  it("claims an empty series only when the server answered one", async () => {
    api.getContestSeriesAwards.mockResolvedValue({
      data: { resources: [] },
      error: null,
    });

    const wrapper = await render();

    expect(wrapper.text()).toContain(EMPTY_SERIES);
    expect(wrapper.find(".form-error").exists()).toBe(false);
  });

  it("takes the refusal off the screen when the list is read again", async () => {
    api.getContestSeriesAwards
      .mockResolvedValueOnce(refusedList(404))
      .mockResolvedValueOnce({ data: { resources: [award] }, error: null });
    const wrapper = await render();
    expect(wrapper.find(".form-error").exists()).toBe(true);

    // A grant that went through re-reads the list.
    await pickUser(wrapper, "Гримуар");
    await wrapper.get("#grant-award-type").setValue("at-1");
    await grant(wrapper);

    expect(wrapper.find(".form-error").exists()).toBe(false);
    expect(wrapper.get(".grant-row").text()).toContain("Победитель");
  });
});

// The confirmation runs the revoke: it prints a refusal in itself and closes on success alone.
describe("revoking an award", () => {
  beforeEach(() => {
    api.getContestSeriesAwards.mockResolvedValue({
      data: { resources: [award] },
      error: null,
    });
  });

  async function revoke(answer: unknown) {
    api.revokeUserAward.mockResolvedValue(answer);
    const wrapper = await render();
    await wrapper.get(".grant-row .text-button").trigger("click");
    await windows().get(".dialog-btn-submit").trigger("click");
    await flushPromises();
    return wrapper;
  }

  it("takes the row off with the window's flags and closes", async () => {
    const wrapper = await revoke({ data: null, error: null });

    expect(api.revokeUserAward).toHaveBeenCalledWith("Гримуар", "a-1", {
      ownsRefusal: true,
      ownsRateLimit: true,
    });
    expect(wrapper.find(".grant-row").exists()).toBe(false);
    expect(windows().find(".dialog-container").exists()).toBe(false);
  });

  it("prints a refused revoke in the window and keeps the row", async () => {
    const wrapper = await revoke({
      data: null,
      error: { type: "", title: "", status: 404, traceId: "t" },
    });

    expect(windows().get(".dialog-refusal").text()).toBe(
      "Не удалось отозвать награду",
    );
    expect(windows().find(".dialog-container").exists()).toBe(true);
    expect(wrapper.find(".grant-row").exists()).toBe(true);
    expect(plates()).toEqual([]);
  });
});

// A refused "Сохранить" says so on the line over the button, and the form stays as typed.
describe("saving the series", () => {
  beforeEach(() => {
    api.getContestSeriesAwards.mockResolvedValue({
      data: { resources: [] },
      error: null,
    });
  });

  const saveButton = (wrapper: VueWrapper) =>
    wrapper.findAll("button").find((button) => button.text() === "Сохранить")!;

  it("prints a refused save under the fields, in the server's words", async () => {
    api.updateContestSeries.mockResolvedValue({
      data: null,
      error: {
        type: "BadRequest",
        title: "Адрес топика записан с ошибкой",
        status: 400,
        traceId: "t",
      },
    });
    const wrapper = await render();
    await wrapper.get("#series-topic-url").setValue("/forum/topic/x");

    await saveButton(wrapper).trigger("click");
    await flushPromises();

    expect(wrapper.get(".edit-refusal").text()).toBe(
      "Адрес топика записан с ошибкой",
    );
    expect(
      (wrapper.get("#series-topic-url").element as HTMLInputElement).value,
    ).toBe("/forum/topic/x");
    expect(plates()).toEqual([]);
  });

  it("says nothing on the line after a save the server took", async () => {
    api.updateContestSeries.mockResolvedValue({ data: null, error: null });
    const wrapper = await render();

    await saveButton(wrapper).trigger("click");
    await flushPromises();

    expect(api.updateContestSeries).toHaveBeenCalledTimes(1);
    expect(wrapper.find(".edit-refusal").exists()).toBe(false);
  });
});
