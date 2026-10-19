import { vi } from "vitest";
import { flushPromises, mount } from "@vue/test-utils";
import type { BadRequestError } from "@/shared/api/models/common";
import LoginForm from "./LoginForm.vue";
import {
  completeSecondFactor,
  push,
  recover,
  signIn,
  toast,
} from "./authForms.doubles";
import { problem } from "./authForms.fixtures";

export const refused = (
  status: number,
  title = "",
  retryAfterSeconds?: number,
) =>
  ({
    stage: "refused",
    failure: problem(status, title, retryAfterSeconds),
  }) as const;

export function wrongPair() {
  const failure: BadRequestError = {
    ...problem(400),
    errors: { password: ["Неверная почта или пароль"] },
  };
  return { stage: "refused", failure } as const;
}

const clock = { now: 0 };

// Attached: focus and selection are only observable in a document.
export const mountForm = (prefillEmail?: string) =>
  mount(LoginForm, {
    props: prefillEmail === undefined ? {} : { prefillEmail },
    attachTo: document.body,
  });

export async function submitCredentials(email = "reader@dm.am") {
  const wrapper = mountForm();
  await flushPromises();

  clock.now += 10_000;

  await wrapper.find("#email").setValue(email);
  await wrapper.find("#password").setValue("correct horse");
  await wrapper.find("form").trigger("submit");
  await flushPromises();

  return wrapper;
}

export async function atSecondFactor() {
  signIn.mockResolvedValue({ stage: "secondFactor" });
  return submitCredentials();
}

// No submit: a whole code starts its own check.
export async function typeCode(
  wrapper: Awaited<ReturnType<typeof atSecondFactor>>,
  code: string,
) {
  await wrapper.find("#two-factor-code").setValue(code);
  await flushPromises();
}

// Found by its label: a search over the card takes whichever row comes first.
export function linkBeside(
  wrapper: Awaited<ReturnType<typeof submitCredentials>>,
  label: string,
) {
  const row = wrapper
    .findAll(".auth-label-row")
    .find((candidate) => candidate.find(".auth-label").text() === label);

  if (!row) throw new Error(`нет строки лейбла "${label}"`);
  return row.find(".auth-action");
}

export function resetLoginForm() {
  clock.now = 0;
  vi.spyOn(Date, "now").mockImplementation(() => clock.now);
  signIn.mockReset();
  completeSecondFactor.mockReset();
  recover.mockReset();
  push.mockReset();
  toast.error.mockReset();
  toast.warning.mockReset();
  return () => {
    vi.restoreAllMocks();
  };
}
