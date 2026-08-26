/**
 * @vitest-environment jsdom
 */

/**
 * The mailed way back for somebody who lost both the phone and the codes.
 *
 * Two properties carry the whole screen. It answers the same for every address,
 * because the server does: a result screen that said "аккаунт не найден" would
 * turn this form into a way of asking which accounts carry a second factor. And
 * a failure is not a sent letter: reported as one, the reader waits for a
 * message nobody posted, which is the state this path exists to get them out
 * of.
 */
import { describe, expect, it, vi, beforeEach } from "vitest";
import { flushPromises, mount } from "@vue/test-utils";
import { defineComponent, h } from "vue";
import { createPinia, setActivePinia } from "pinia";
import Form from "@/shared/ui/Form/Form.vue";
import FormField from "@/shared/ui/Form/FormField.vue";
import { accountApi } from "@/entities/user";
import TwoFactorRemovalForm from "./TwoFactorRemovalForm.vue";

/** The dialog frame is a vue-final-modal wrapper; the form inside it is the subject. */
const DialogStub = defineComponent({
  setup:
    (_props, { slots }) =>
    () =>
      h("div", slots.default?.()),
});

async function submit(email = "reader@dm.am") {
  const wrapper = mount(TwoFactorRemovalForm, {
    global: {
      plugins: [createPinia()],
      components: { Dialog: DialogStub, Form, FormField },
    },
  });

  await wrapper.find("#two-factor-removal-email").setValue(email);
  await wrapper.find("form").trigger("submit");
  await flushPromises();
  return wrapper;
}

describe("the mailed removal request", () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.restoreAllMocks();
  });

  it("asks for the address and says what the letter will do", async () => {
    const wrapper = mount(TwoFactorRemovalForm, {
      global: {
        plugins: [createPinia()],
        components: { Dialog: DialogStub, Form, FormField },
      },
    });

    expect(wrapper.text()).toContain("Снятие второго фактора");
    expect(wrapper.text()).toContain("через семь дней");
    expect(wrapper.find("#two-factor-removal-email").exists()).toBe(true);
  });

  it("keeps the form up when the letter did not go out", async () => {
    vi.spyOn(accountApi, "requestTwoFactorRemoval").mockResolvedValue({
      data: null,
      error: { status: 400, title: "Неверный формат почты" },
    } as never);

    const wrapper = await submit();

    expect(wrapper.text()).not.toContain("Проверьте почту");
    expect(wrapper.text()).toContain("Неверный формат почты");
  });

  // This path is anonymous and sits on the address budget, so a rate limit is
  // the refusal it actually meets. The interceptor's toast carries the
  // retry-after value; repeating it under the field would say the same thing
  // twice and say it worse.
  it("leaves a rate limit to its toast and still does not claim success", async () => {
    vi.spyOn(accountApi, "requestTwoFactorRemoval").mockResolvedValue({
      data: null,
      error: { status: 429, title: "Too Many Requests" },
    } as never);

    const wrapper = await submit();

    expect(wrapper.text()).not.toContain("Проверьте почту");
    expect(wrapper.text()).not.toContain("Too Many Requests");
    expect(wrapper.find("#two-factor-removal-email").exists()).toBe(true);
  });

  // One screen for every address: whether an account exists, and whether it
  // carries a factor, is not this endpoint's to disclose.
  it("says the same thing whatever address was named", async () => {
    vi.spyOn(accountApi, "requestTwoFactorRemoval").mockResolvedValue({
      data: null,
      error: null,
    } as never);

    const wrapper = await submit("nobody@dm.am");

    expect(wrapper.text()).toContain("Проверьте почту");
    expect(wrapper.text()).not.toContain("не найден");
    expect(wrapper.find("#two-factor-removal-email").exists()).toBe(false);
  });
});
