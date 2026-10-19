/** The second-factor section over one state of the factor, for its two specs. */
import { vi } from "vitest";
import { flushPromises, mount } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { accountApi } from "@/entities/user";
import { useToast } from "@/shared/lib/composables/useToast";
import type { TwoFactorStatus } from "@/shared/api/models/account";
import AccountTwoFactorSection from "./AccountTwoFactorSection.vue";

const toast = useToast();

/** Everything the site is currently saying in a floating message. */
export const saidOutLoud = () => toast.toasts.value.map((one) => one.message);

export const options = () => ({
  global: { plugins: [createPinia()] },
});

export const OFF: TwoFactorStatus = {
  enabled: false,
  recoveryCodesLeft: 0,
};

export const ON: TwoFactorStatus = {
  enabled: true,
  enabledUtc: "2026-05-01T10:00:00Z",
  lastVerifiedUtc: "2026-08-20T18:30:00Z",
  recoveryCodesLeft: 7,
};

/** Mounts the section over one state of the factor. */
export async function withStatus(status: Partial<TwoFactorStatus>, base = OFF) {
  vi.spyOn(accountApi, "getTwoFactorStatus").mockResolvedValue({
    data: { resource: { ...base, ...status } },
    error: null,
  } as never);

  const wrapper = mount(AccountTwoFactorSection, options());
  await flushPromises();
  return wrapper;
}

export const codes = () =>
  Array.from({ length: 10 }, (_, index) => `ABCD${index}EFGHIJKLMNO`);

/** Opens the reveal and gets as far as the issued secret. */
export async function issued() {
  const wrapper = await withStatus({});
  await wrapper.find("button").trigger("click");

  vi.spyOn(accountApi, "setupTwoFactor").mockResolvedValue({
    data: {
      resource: {
        secret: "JBSWY3DPEHPK3PXP",
        otpAuthUri: "otpauth://totp/DM:reader?secret=JBSWY3DPEHPK3PXP",
      },
    },
    error: null,
  } as never);

  await wrapper.find("#two-factor-password").setValue("correct horse");
  await wrapper.find("form").trigger("submit");
  await flushPromises();
  return wrapper;
}

/**
 * Confirms the issued secret with a code; `reread` is the answer the state is
 * read again with.
 */
export async function confirmed(
  reread: unknown = { data: { resource: ON }, error: null },
) {
  const wrapper = await issued();

  vi.spyOn(accountApi, "confirmTwoFactor").mockResolvedValue({
    data: { resource: { codes: codes() } },
    error: null,
  } as never);
  vi.spyOn(accountApi, "getTwoFactorStatus").mockResolvedValue(reread as never);

  await wrapper.find("#two-factor-code").setValue("123456");
  await wrapper.find("form").trigger("submit");
  await flushPromises();
  return wrapper;
}

/**
 * A fresh store, no mocks left from the test before, no floating message, and
 * a clipboard, which jsdom has none of. Returns the stand-in for its write.
 */
export function resetSection() {
  setActivePinia(createPinia());
  vi.restoreAllMocks();
  const write = vi.fn().mockResolvedValue(undefined);
  Object.assign(navigator, { clipboard: { writeText: write } });
  [...toast.toasts.value].forEach((one) => toast.dismiss(one.id));
  return write;
}
