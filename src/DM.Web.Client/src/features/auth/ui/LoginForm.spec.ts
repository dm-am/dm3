/**
 * @vitest-environment jsdom
 */

/**
 * The form reported success for every failure that was not a 400. A banned
 * account got the dialog closed, one toast reading "Недостаточно прав для этого
 * действия", and a header still offering "Вход | Регистрация" — while the
 * reason the server gave reached nobody.
 *
 * The second factor is the same trap in a different disguise, and worse: there
 * the server answers 200. "Now enter the code" is a successful reply with a
 * stage on it and no viewer in it, so a form that reads "no error, therefore a
 * session" closes over a guest and drops a login that was half done. Asserted
 * below on all three stages, because the stage is now the whole answer.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { flushPromises, mount } from "@vue/test-utils";
import { defineComponent, h } from "vue";
import type { BadRequestError, GeneralError } from "@/shared/api/models/common";
import Form from "@/shared/ui/Form/Form.vue";
import FormField from "@/shared/ui/Form/FormField.vue";
import { signIn, completeSecondFactor } from "@/entities/user";
import LoginForm from "./LoginForm.vue";

vi.mock("@/entities/user", () => ({
  signIn: vi.fn(),
  completeSecondFactor: vi.fn(),
}));

/** The dialog frame is a vue-final-modal wrapper; the form inside it is the subject. */
const DialogStub = defineComponent({
  setup:
    (_props, { slots }) =>
    () =>
      h("div", slots.default?.()),
});

const problem = (status: number, title = ""): GeneralError => ({
  type: "",
  title,
  status,
  traceId: "trace",
});

const refused = (status: number, title = "") =>
  ({ stage: "refused", failure: problem(status, title) }) as const;

/** The clock the form reads to measure how long it has been on screen. */
let now = 0;

async function submitCredentials() {
  const wrapper = mount(LoginForm, {
    global: { components: { Dialog: DialogStub, Form, FormField } },
    // The focus move on the step swap is only observable in a live document.
    attachTo: document.body,
  });

  // Past the minimum fill time, so the submit goes out without waiting it out.
  now += 10_000;

  await wrapper.find("#email").setValue("reader@dm.am");
  await wrapper.find("#password").setValue("correct horse");
  await wrapper.find("form").trigger("submit");
  await flushPromises();

  return wrapper;
}

/** Carries the login to the second step and answers it with `code`. */
async function submitSecondFactor(code = "123456") {
  vi.mocked(signIn).mockResolvedValue({ stage: "secondFactor" });
  const wrapper = await submitCredentials();

  await wrapper.find("#two-factor-code").setValue(code);
  await wrapper.find("form").trigger("submit");
  await flushPromises();

  return wrapper;
}

describe("LoginForm", () => {
  beforeEach(() => {
    now = 0;
    vi.spyOn(Date, "now").mockImplementation(() => now);
    vi.mocked(signIn).mockReset();
    vi.mocked(completeSecondFactor).mockReset();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("shows the reason the server refused instead of reporting success", async () => {
    vi.mocked(signIn).mockResolvedValue(refused(403, "Аккаунт забанен"));

    const wrapper = await submitCredentials();

    expect(wrapper.emitted("success")).toBeUndefined();
    expect(wrapper.text()).toContain("Аккаунт забанен");
  });

  it("keeps showing what a rejected form got wrong", async () => {
    const rejected: BadRequestError = {
      ...problem(400),
      errors: { password: ["Неверная почта или пароль"] },
    };
    vi.mocked(signIn).mockResolvedValue({
      stage: "refused",
      failure: rejected,
    });

    const wrapper = await submitCredentials();

    expect(wrapper.emitted("success")).toBeUndefined();
    expect(wrapper.text()).toContain("Неверная почта или пароль");
  });

  it("leaves a failure the interceptor announced to its toast", async () => {
    vi.mocked(signIn).mockResolvedValue(refused(429, "Too Many Requests"));

    const wrapper = await submitCredentials();

    expect(wrapper.emitted("success")).toBeUndefined();
    // The toast carries the retry-after value. Repeating the English title of
    // the rate-limit document on the form would say less, and say it twice.
    expect(wrapper.text()).not.toContain("Too Many Requests");
  });

  it("reports success once the session is established", async () => {
    vi.mocked(signIn).mockResolvedValue({ stage: "signedIn" });

    const wrapper = await submitCredentials();

    expect(wrapper.emitted("success")).toHaveLength(1);
  });

  describe("second factor", () => {
    it("asks for the code instead of reporting a session", async () => {
      vi.mocked(signIn).mockResolvedValue({ stage: "secondFactor" });

      const wrapper = await submitCredentials();

      expect(wrapper.emitted("success")).toBeUndefined();
      expect(wrapper.text()).toContain("Подтверждение входа");
      expect(wrapper.find("#two-factor-code").exists()).toBe(true);
      // The password step is gone: this dialog now holds one field.
      expect(wrapper.find("#password").exists()).toBe(false);
    });

    // The step swaps inside a dialog that is already open, so the button the
    // reader pressed is gone and focus falls back to the document. Somebody on
    // a keyboard would have to find their way back into a form they never left.
    it("puts the cursor in the field it just asked for", async () => {
      vi.mocked(signIn).mockResolvedValue({ stage: "secondFactor" });

      const wrapper = await submitCredentials();

      expect(document.activeElement).toBe(
        wrapper.find("#two-factor-code").element,
      );
    });

    it("shows the one sentence the server answers every refusal with", async () => {
      vi.mocked(completeSecondFactor).mockResolvedValue(
        problem(400, "Код не подошел"),
      );

      const wrapper = await submitSecondFactor();

      expect(wrapper.emitted("success")).toBeUndefined();
      expect(wrapper.text()).toContain("Код не подошел");
    });

    it("reports success once the code is accepted", async () => {
      vi.mocked(completeSecondFactor).mockResolvedValue(null);

      const wrapper = await submitSecondFactor();

      expect(completeSecondFactor).toHaveBeenCalledWith("123456");
      expect(wrapper.emitted("success")).toHaveLength(1);
    });

    // The switch changes what is written under the field and nothing else: one
    // value goes to the server either way, and the server tells the two kinds
    // apart by shape.
    it("swaps the hint for the recovery format and back", async () => {
      vi.mocked(signIn).mockResolvedValue({ stage: "secondFactor" });
      const wrapper = await submitCredentials();

      expect(wrapper.text()).toContain("Шесть цифр");

      await wrapper.find(".field-action").trigger("click");
      expect(wrapper.text()).toContain("Шестнадцать символов");
      expect(wrapper.text()).toContain("Резервный код");

      await wrapper.find(".field-action").trigger("click");
      expect(wrapper.text()).toContain("Шесть цифр");
    });

    it("hands over the reader who has neither the phone nor the codes", async () => {
      vi.mocked(signIn).mockResolvedValue({ stage: "secondFactor" });
      const wrapper = await submitCredentials();

      await wrapper.find(".second-factor-help .field-action").trigger("click");

      expect(wrapper.emitted("cantPassSecondFactor")).toHaveLength(1);
    });
  });
});
