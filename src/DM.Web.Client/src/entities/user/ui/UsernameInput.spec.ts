/**
 * @vitest-environment jsdom
 */

/**
 * What the name field says when the name cannot be had.
 *
 * It used to say it three ways - "Имя уже занято" when somebody holds the name,
 * "Имя зарезервировано" when somebody used to, "Имя недоступно" when the server
 * named no reason at all - and the activation page added a fourth of its own,
 * "Уже занято", for the same refusal arriving on submit. The reader does one
 * and the same thing about every one of them, and four wordings only made him
 * wonder which of the four he had run into.
 *
 * The one sentence says "занято" and not "используется": the server answers
 * "Taken" for a name a former user left behind as well as for one in use (it
 * used to say "Reserved" for the former), and "используется" asserts a live
 * owner, which is false for the former.
 *
 * The one refusal that is not about the owner keeps its own words: a forbidden
 * character is fixed by typing a different character, and the server spells
 * that reason as its own enum member rather than in the lowercase this
 * component was written against.
 */
import { describe, expect, it, vi, beforeEach } from "vitest";
import { flushPromises, mount } from "@vue/test-utils";
import { accountApi } from "@/entities/user";
import UsernameInput from "./UsernameInput.vue";

const TAKEN = "Это имя занято";

/**
 * Put a name in the field and let the check come back with the reason given.
 *
 * The field is enabled rather than typed into: the component is controlled, so
 * a keystroke in a test only emits an update its caller has to hand back, and
 * the check drops an answer whose name no longer matches the prop. Re-enabling
 * asks the same question about the name already in the field.
 */
const answer = async (reason: string | undefined) => {
  vi.spyOn(accountApi, "checkUsername").mockResolvedValue({
    data: { isAvailable: false, reason },
    error: null,
  } as never);

  const wrapper = mount(UsernameInput, {
    props: { modelValue: "reader", disabled: true },
  });

  await wrapper.setProps({ disabled: false });
  await flushPromises();

  return wrapper;
};

describe("поле имени, когда имя недоступно", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it.each([
    ["занято", "Taken"],
    ["недоступно без объяснения", undefined],
  ])("говорит одной фразой, когда имя %s", async (_, reason) => {
    const wrapper = await answer(reason);

    expect(wrapper.get(".username-status").text()).toBe(TAKEN);
    for (const retired of [
      "Имя уже занято",
      "Имя зарезервировано",
      "Имя недоступно",
      "уже используется",
    ]) {
      expect(wrapper.text()).not.toContain(retired);
    }
  });

  it.each([
    ["как его пишет сервер", "InvalidFormat"],
    ["в прежнем написании", "invalid_format"],
  ])("оставляет отказ по символам своими словами, %s", async (_, reason) => {
    const wrapper = await answer(reason);

    expect(wrapper.get(".username-status").text()).toBe("Недопустимый символ");
  });
});
