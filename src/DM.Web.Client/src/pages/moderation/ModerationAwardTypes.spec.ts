/**
 * @vitest-environment jsdom
 */

// "Скрыть" and "Вернуть" answer a refusal on a line under the row they stand in.
import { describe, it, expect, vi, afterEach } from "vitest";
import { enableAutoUnmount, flushPromises, mount } from "@vue/test-utils";
import { achievementApi } from "@/entities/achievement";
import ModerationAwardTypes from "./ModerationAwardTypes.vue";

const reload = vi.hoisted(() => vi.fn());

vi.mock("@/entities/achievement", async () => {
  const { ref } = await import("vue");
  return {
    achievementApi: { updateAwardType: vi.fn() },
    awardMetal: () => null,
    metalStyle: () => ({}),
    useContestSeries: () => ({
      adminAwardTypes: ref([
        {
          id: "at-1",
          code: "winner",
          iconName: "trophy",
          title: "Победитель",
          description: "первое место",
          tier: 1,
          sortOrder: 0,
          isActive: true,
        },
        {
          id: "at-2",
          code: "second",
          iconName: "medal",
          title: "Призер",
          description: "второе место",
          tier: 2,
          sortOrder: 1,
          isActive: false,
        },
      ]),
      loadAdmin: vi.fn().mockResolvedValue(undefined),
      reload,
    }),
  };
});

vi.mock("vue-final-modal", () => ({
  useModal: () => ({ open: vi.fn(), close: vi.fn() }),
}));

enableAutoUnmount(afterEach);
afterEach(() => vi.clearAllMocks());

function render() {
  return mount(ModerationAwardTypes, {
    global: { stubs: { "page-title": true, GameIcon: true } },
  });
}

const actionOf = (
  row: ReturnType<ReturnType<typeof render>["get"]>,
  label: string,
) => row.findAll(".type-actions button").find((b) => b.text() === label)!;

describe("the catalogue of award types", () => {
  it("prints a refused hide under its row and reads nothing again", async () => {
    vi.mocked(achievementApi.updateAwardType).mockResolvedValue({
      data: null,
      error: { type: "Conflict", title: "", status: 409, traceId: "t" },
    } as never);
    const wrapper = render();
    const [first, second] = wrapper.findAll(".type-row");

    await actionOf(first, "Скрыть").trigger("click");
    await flushPromises();

    expect(first.get(".type-refusal").text()).toBe(
      "Не удалось скрыть тип награды",
    );
    expect(second.find(".type-refusal").exists()).toBe(false);
    expect(reload).not.toHaveBeenCalled();
  });

  it("reads the catalogue again after a return the server took", async () => {
    vi.mocked(achievementApi.updateAwardType).mockResolvedValue({
      data: null,
      error: null,
    } as never);
    const wrapper = render();
    const second = wrapper.findAll(".type-row")[1];

    await actionOf(second, "Вернуть").trigger("click");
    await flushPromises();

    expect(achievementApi.updateAwardType).toHaveBeenCalledWith("at-2", {
      isActive: true,
    });
    expect(wrapper.find(".type-refusal").exists()).toBe(false);
    expect(reload).toHaveBeenCalledTimes(1);
  });

  it("names the order in Russian, as the achievements do", () => {
    const wrapper = render();

    expect(wrapper.get(".type-order").text()).toBe("порядок 0");
  });
});
