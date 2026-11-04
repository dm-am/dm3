import { vi } from "vitest";
import { flushPromises, mount } from "@vue/test-utils";
import type { ApiResult, BadRequestError } from "@/shared/api/models/common";
import type { EmailAvailability } from "@/shared/api/models/account";
import RegistrationForm from "./RegistrationForm.vue";
import { checkEmail, recover, register } from "./authForms.doubles";
import { AuthDialogStub } from "./authForms.fixtures";

export type CheckEmailAnswer = ApiResult<EmailAvailability>;

export function heldCheck() {
  let answer: ((value: CheckEmailAnswer) => void) | undefined;
  checkEmail.mockImplementation(
    () =>
      new Promise<CheckEmailAnswer>((resolve) => {
        answer = resolve;
      }),
  );
  return (value: CheckEmailAnswer) => answer?.(value);
}

export const fieldProblem = (field: string, code: string): BadRequestError => ({
  type: "",
  title: "Проверьте введенные данные",
  status: 400,
  traceId: "trace",
  errors: { [field]: [code] },
});

export const clock = { now: 0 };

export function mountForm() {
  return mount(RegistrationForm, {
    global: { stubs: { AuthDialog: AuthDialogStub } },
  });
}

export type Wrapper = ReturnType<typeof mountForm>;

// Types the address and waits out the check the blur starts.
export async function askAbout(email = "reader@dm.am") {
  const wrapper = mountForm();
  await wrapper.find("#registration-email").setValue(email);
  await wrapper.find("#registration-email").trigger("blur");
  await flushPromises();
  return wrapper;
}

export const control = (wrapper: Wrapper, caption: string) =>
  wrapper.findAll("button").find((button) => button.text() === caption);

export async function toPasswordStep(wrapper: Wrapper, email = "reader@dm.am") {
  await wrapper.find("#registration-email").setValue(email);
  await wrapper.find("form").trigger("submit");
  await flushPromises();
  clock.now += 10_000;
}

export async function acceptRules(wrapper: Wrapper) {
  await wrapper.find('a[href="/rules"]').trigger("click");
  await wrapper.find("#registration-rules").setValue(true);
}

export async function submitRegistration(password = "correct horse battery") {
  const wrapper = mountForm();
  await toPasswordStep(wrapper);

  await wrapper.find("#registration-password").setValue(password);
  await acceptRules(wrapper);
  await wrapper.find("form").trigger("submit");
  await flushPromises();

  return wrapper;
}

export function resetRegistrationForm() {
  clock.now = 0;
  sessionStorage.clear();
  vi.spyOn(Date, "now").mockImplementation(() => clock.now);
  checkEmail.mockReset();
  checkEmail.mockResolvedValue({
    data: { isAvailable: true },
    error: null,
  });
  register.mockReset();
  recover.mockReset();
  return () => {
    vi.restoreAllMocks();
  };
}
