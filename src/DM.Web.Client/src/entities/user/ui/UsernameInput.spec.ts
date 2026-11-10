/**
 * @vitest-environment jsdom
 */

// One sentence for a name that cannot be had, and its own for a forbidden character.
import { describe, expect, it, vi, beforeEach, afterEach } from "vitest";
import { flushPromises, mount } from "@vue/test-utils";
import { accountApi } from "@/entities/user";
import { SEARCH_DEBOUNCE_MS } from "@/shared/lib/constants/search";
import UsernameInput from "./UsernameInput.vue";

const TAKEN = "Это имя занято";

const answer = async (reason: string | undefined) => {
  vi.useFakeTimers();
  vi.spyOn(accountApi, "checkUsername").mockResolvedValue({
    data: { isAvailable: false, reason },
    error: null,
  } as never);

  const wrapper = mount(UsernameInput, {
    props: { modelValue: "reader" },
  });

  await wrapper.get("input").setValue("reader");
  await vi.advanceTimersByTimeAsync(SEARCH_DEBOUNCE_MS);
  vi.useRealTimers();
  await flushPromises();

  return wrapper;
};

describe("поле имени, когда имя недоступно", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it.each([
    ["занято", "Taken"],
    ["недоступно без объяснения", undefined],
  ])("говорит одной фразой, когда имя %s", async (_, reason) => {
    const wrapper = await answer(reason);

    expect(wrapper.get(".username-status").text()).toBe(TAKEN);
  });

  it("оставляет отказ по символам своими словами, как его пишет сервер", async () => {
    const wrapper = await answer("InvalidFormat");

    expect(wrapper.get(".username-status").text()).toBe("Недопустимый символ");
  });
});
