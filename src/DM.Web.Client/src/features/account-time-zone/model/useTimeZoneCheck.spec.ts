/**
 * @vitest-environment jsdom
 */

import { describe, expect, it, vi, beforeEach } from "vitest";
import { flushPromises } from "@vue/test-utils";
import { createPinia, setActivePinia } from "pinia";
import { personalApi } from "@/entities/user";
import { useAuthStore } from "@/shared/stores/auth";
import { useToast } from "@/shared/lib/composables/useToast";
import { Theme } from "@/shared/api/models/personal";
import {
  accountTimeZone,
  setAccountTimeZone,
} from "@/shared/lib/utils/timeZone";
import { useTimeZoneCheck } from "./useTimeZoneCheck";

const DEVICE = "Europe/Berlin";
const ASKED_KEY = "dm3-time-zone-asked";

vi.mock("@/shared/lib/utils/timeZone", async () => {
  const actual = await vi.importActual<
    typeof import("@/shared/lib/utils/timeZone")
  >("@/shared/lib/utils/timeZone");
  return { ...actual, deviceTimeZone: () => DEVICE };
});

// Wrapped as the API wraps a single resource; an account with no zone comes
// without the field, since the API leaves a null out.
function accountKeeps(timeZoneId?: string) {
  vi.spyOn(personalApi, "getMyPreferences").mockResolvedValue({
    data: {
      resource: {
        theme: Theme.Light,
        pageSize: 10,
        ...(timeZoneId === undefined ? {} : { timeZoneId }),
      },
    },
    error: null,
  } as unknown as Awaited<ReturnType<typeof personalApi.getMyPreferences>>);
}

type Written = Awaited<ReturnType<typeof personalApi.updateMyPreferences>>;

function accountTakes() {
  return vi
    .spyOn(personalApi, "updateMyPreferences")
    .mockResolvedValue({ data: null, error: null } as Written);
}

// Status 0 is a dead connection; the interceptor is off the path, so the status alone decides.
function accountRefuses(status: number, title = "") {
  return vi.spyOn(personalApi, "updateMyPreferences").mockResolvedValue({
    data: null,
    error: { type: "", title, status, traceId: "" },
  } as Written);
}

async function press() {
  const { toasts } = useToast();
  toasts.value[0].action?.();
  await flushPromises();
  return useToast().toasts.value;
}

async function run(signedIn = true) {
  setActivePinia(createPinia());
  useAuthStore().user = signedIn
    ? ({ id: "1", username: "reader" } as never)
    : null;

  useTimeZoneCheck();
  await flushPromises();
  return useToast().toasts.value;
}

beforeEach(() => {
  vi.restoreAllMocks();
  localStorage.clear();
  setAccountTimeZone(null);
  const { toasts, dismiss } = useToast();
  [...toasts.value].forEach((toast) => dismiss(toast.id));
});

describe("the time zone offer", () => {
  it("offers the device zone when the account keeps another one", async () => {
    accountKeeps("Europe/Moscow");

    const toasts = await run();

    expect(toasts).toHaveLength(1);
    expect(toasts[0].message).toBe(
      "Часовой пояс не совпадает с устройством, [синхронизировать]?",
    );
    expect(toasts[0].action).toBeTypeOf("function");
  });

  // An account with no zone keeps UTC, which is a zone like any other here.
  it("speaks up for an account that names no zone at all", async () => {
    accountKeeps();

    expect(await run()).toHaveLength(1);
  });

  it("says nothing when the account is already in this zone", async () => {
    accountKeeps(DEVICE);

    expect(await run()).toHaveLength(0);
  });

  it("says nothing to a reader who is not signed in", async () => {
    const read = vi.spyOn(personalApi, "getMyPreferences");

    expect(await run(false)).toHaveLength(0);
    expect(read).not.toHaveBeenCalled();
  });

  it("writes the device zone when the reader takes the offer", async () => {
    accountKeeps("Europe/Moscow");
    const write = accountTakes();

    await run();
    const toasts = await press();

    expect(write).toHaveBeenCalledWith({ timeZoneId: DEVICE });
    expect(localStorage.getItem(ASKED_KEY)).toBe(DEVICE);
    expect(toasts.filter((toast) => toast.type !== "info")).toHaveLength(0);
  });

  it("asks again at the next start when the press was refused", async () => {
    accountKeeps("Europe/Moscow");
    accountRefuses(500);

    await run();
    await press();

    expect(localStorage.getItem(ASKED_KEY)).toBeNull();

    const { toasts, dismiss } = useToast();
    [...toasts.value].forEach((toast) => dismiss(toast.id));
    expect(await run()).toHaveLength(1);
  });

  it.each([0, 401, 429, 500])(
    "says nothing of its own about a refusal the interceptor announced (%i)",
    async (status) => {
      accountKeeps("Europe/Moscow");
      accountRefuses(status);

      await run();
      const toasts = await press();

      expect(toasts.filter((toast) => toast.type === "error")).toHaveLength(0);
    },
  );

  it("relays the server's own sentence for a refusal nobody announced", async () => {
    accountKeeps("Europe/Moscow");
    accountRefuses(404, "Аккаунт не найден");

    await run();
    const toasts = await press();

    expect(toasts.map((toast) => [toast.type, toast.message])).toContainEqual([
      "error",
      "Аккаунт не найден",
    ]);
  });

  it("names the setting when the server refused without a word", async () => {
    accountKeeps("Europe/Moscow");
    accountRefuses(404);

    await run();
    const toasts = await press();

    expect(toasts.map((toast) => [toast.type, toast.message])).toContainEqual([
      "error",
      "Не удалось сменить часовой пояс",
    ]);
  });

  it("asks once per zone", async () => {
    accountKeeps("Europe/Moscow");

    await run();
    const { toasts, dismiss } = useToast();
    [...toasts.value].forEach((toast) => dismiss(toast.id));

    expect(await run()).toHaveLength(0);
  });

  it("asks again when the device has moved somewhere else", async () => {
    accountKeeps("Europe/Moscow");
    localStorage.setItem(ASKED_KEY, "Asia/Tokyo");

    expect(await run()).toHaveLength(1);
  });
});

describe("the zone of the account on the page", () => {
  const ZONE_KEY = "dm3-account-time-zone";

  it("is the zone the preferences name", async () => {
    accountKeeps("Asia/Tokyo");

    await run();

    expect(accountTimeZone.value).toBe("Asia/Tokyo");
    expect(localStorage.getItem(ZONE_KEY)).toBe("Asia/Tokyo");
  });

  it("is UTC for an account that names none, as the letters read it", async () => {
    accountKeeps();

    await run();

    expect(accountTimeZone.value).toBe("UTC");
  });

  it("is read when the offer has nothing to say", async () => {
    accountKeeps(DEVICE);
    localStorage.setItem(ASKED_KEY, DEVICE);

    expect(await run()).toHaveLength(0);
    expect(accountTimeZone.value).toBe(DEVICE);
  });

  it("is the one the last visit left while the preferences are on the way", async () => {
    localStorage.setItem(ZONE_KEY, "Asia/Tokyo");
    vi.spyOn(personalApi, "getMyPreferences").mockReturnValue(
      new Promise(() => {}),
    );

    await run();

    expect(accountTimeZone.value).toBe("Asia/Tokyo");
  });

  it("is nobody's for a guest, and a stored one is forgotten", async () => {
    localStorage.setItem(ZONE_KEY, "Asia/Tokyo");

    await run(false);

    expect(accountTimeZone.value).toBeNull();
    expect(localStorage.getItem(ZONE_KEY)).toBeNull();
  });

  it("goes with the sign-out", async () => {
    accountKeeps("Asia/Tokyo");
    await run();

    useAuthStore().user = null;
    await flushPromises();

    expect(accountTimeZone.value).toBeNull();
  });

  it("follows the account to the device's zone when the offer is taken", async () => {
    accountKeeps("Europe/Moscow");
    accountTakes();

    await run();
    expect(accountTimeZone.value).toBe("Europe/Moscow");
    await press();

    expect(accountTimeZone.value).toBe(DEVICE);
  });
});
