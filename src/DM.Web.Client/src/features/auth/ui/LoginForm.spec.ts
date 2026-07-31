/**
 * @vitest-environment jsdom
 */

/**
 * The form reported success for every failure that was not a 400. A banned
 * account got the dialog closed, one toast reading "Недостаточно прав для этого
 * действия", and a header still offering "Вход | Регистрация" — while the
 * reason the server gave reached nobody.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { flushPromises, mount } from "@vue/test-utils";
import { defineComponent, h } from "vue";
import type { BadRequestError, GeneralError } from "@/shared/api/models/common";
import Form from "@/shared/ui/Form/Form.vue";
import FormField from "@/shared/ui/Form/FormField.vue";
import { signIn } from "@/entities/user";
import LoginForm from "./LoginForm.vue";

vi.mock("@/entities/user", () => ({ signIn: vi.fn() }));

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

/** The clock the form reads to measure how long it has been on screen. */
let now = 0;

async function submitCredentials() {
  const wrapper = mount(LoginForm, {
    global: { components: { Dialog: DialogStub, Form, FormField } },
  });

  // Past the minimum fill time, so the submit goes out without waiting it out.
  now += 10_000;

  await wrapper.find("#email").setValue("reader@dm.am");
  await wrapper.find("#password").setValue("correct horse");
  await wrapper.find("form").trigger("submit");
  await flushPromises();

  return wrapper;
}

describe("LoginForm", () => {
  beforeEach(() => {
    now = 0;
    vi.spyOn(Date, "now").mockImplementation(() => now);
    vi.mocked(signIn).mockReset();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("shows the reason the server refused instead of reporting success", async () => {
    vi.mocked(signIn).mockResolvedValue(problem(403, "Аккаунт забанен"));

    const wrapper = await submitCredentials();

    expect(wrapper.emitted("success")).toBeUndefined();
    expect(wrapper.text()).toContain("Аккаунт забанен");
  });

  it("keeps showing what a rejected form got wrong", async () => {
    const rejected: BadRequestError = {
      ...problem(400),
      errors: { password: ["Неверная почта или пароль"] },
    };
    vi.mocked(signIn).mockResolvedValue(rejected);

    const wrapper = await submitCredentials();

    expect(wrapper.emitted("success")).toBeUndefined();
    expect(wrapper.text()).toContain("Неверная почта или пароль");
  });

  it("leaves a failure the interceptor announced to its toast", async () => {
    vi.mocked(signIn).mockResolvedValue(problem(429, "Too Many Requests"));

    const wrapper = await submitCredentials();

    expect(wrapper.emitted("success")).toBeUndefined();
    // The toast carries the retry-after value. Repeating the English title of
    // the rate-limit document on the form would say less, and say it twice.
    expect(wrapper.text()).not.toContain("Too Many Requests");
  });

  it("reports success once the session is established", async () => {
    vi.mocked(signIn).mockResolvedValue(null);

    const wrapper = await submitCredentials();

    expect(wrapper.emitted("success")).toHaveLength(1);
  });
});
