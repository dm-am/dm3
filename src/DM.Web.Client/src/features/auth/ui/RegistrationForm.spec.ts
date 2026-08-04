/**
 * @vitest-environment jsdom
 */

/**
 * The form opened the "Проверьте почту" screen for every failure that was not a
 * 400 — a rate limit, a mail server that would not take the letter — and the
 * reader waited for a letter nobody had sent.
 */
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { flushPromises, mount } from "@vue/test-utils";
import { defineComponent, h } from "vue";
import type { GeneralError } from "@/shared/api/models/common";
import Form from "@/shared/ui/Form/Form.vue";
import FormField from "@/shared/ui/Form/FormField.vue";
import { accountApi, register } from "@/entities/user";
import RegistrationForm from "./RegistrationForm.vue";

vi.mock("vue-router", () => ({ useRouter: () => ({ push: vi.fn() }) }));
vi.mock("@/entities/user", () => ({
  register: vi.fn(),
  accountApi: { checkEmail: vi.fn() },
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

/** The clock the form reads to measure how long it has been on screen. */
let now = 0;

async function submitRegistration() {
  const wrapper = mount(RegistrationForm, {
    global: { components: { Dialog: DialogStub, Form, FormField } },
  });

  await wrapper.find("#email").setValue("reader@dm.am");
  await wrapper.find("form").trigger("submit");
  await flushPromises();

  // Past the minimum fill time, so the submit goes out instead of asking to wait.
  now += 10_000;

  await wrapper.find("#password").setValue("correct horse battery");
  await wrapper.find("form").trigger("submit");
  await flushPromises();

  return wrapper;
}

describe("RegistrationForm", () => {
  beforeEach(() => {
    now = 0;
    sessionStorage.clear();
    vi.spyOn(Date, "now").mockImplementation(() => now);
    vi.mocked(accountApi.checkEmail).mockReset();
    vi.mocked(accountApi.checkEmail).mockResolvedValue({
      data: { isAvailable: true },
      error: null,
    });
    vi.mocked(register).mockReset();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("does not promise a letter the server never took", async () => {
    vi.mocked(register).mockResolvedValue(problem(500, "Ошибка сервера"));

    const wrapper = await submitRegistration();

    expect(wrapper.emitted("success")).toBeUndefined();
    expect(sessionStorage.getItem("dm_pending_email")).toBeNull();
  });

  it("says what the server named when it refused the address", async () => {
    vi.mocked(register).mockResolvedValue(
      problem(409, "Почта уже зарегистрирована"),
    );

    const wrapper = await submitRegistration();

    expect(wrapper.emitted("success")).toBeUndefined();
    expect(wrapper.text()).toContain("Почта уже зарегистрирована");
  });

  it("hands the address on once the registration is pending", async () => {
    vi.mocked(register).mockResolvedValue(null);

    const wrapper = await submitRegistration();

    expect(wrapper.emitted("success")?.[0]).toEqual(["reader@dm.am"]);
    expect(sessionStorage.getItem("dm_pending_email")).toBe("reader@dm.am");
  });
});
