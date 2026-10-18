/**
 * @vitest-environment jsdom
 */

/**
 * The offer to move the account's time zone to the device's.
 *
 * Nothing but the reader writes that zone: a login that moved it would let a
 * stolen session restamp the message warning about it. The gates here are about
 * the offer appearing when, and only when, it has something true to say — and
 * about it being asked once, because a question already answered is noise.
 *
 * The last four are about the press that failed. The offer leaves the screen
 * on its press, so a refusal used to end in one red line with no way back on
 * this device: the mark said the question was answered. Now a refusal takes
 * the mark off, and the refusal itself is said once - by the interceptor for
 * the statuses it announces, by this module for the rest.
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

const paging = {
  postsPerPage: 10,
  commentsPerPage: 10,
  topicsPerPage: 10,
  messagesPerPage: 10,
  entitiesPerPage: 10,
};

/**
 * The account answers with this zone; null is the account that has none.
 *
 * Wrapped the way the API wraps a single resource. Answered bare, the stub let
 * through code that read the envelope as the answer and found no zone in it.
 */
function accountKeeps(timeZoneId: string | null) {
  vi.spyOn(personalApi, "getMyPreferences").mockResolvedValue({
    data: { resource: { theme: Theme.Light, paging, timeZoneId } },
    error: null,
  } as unknown as Awaited<ReturnType<typeof personalApi.getMyPreferences>>);
}

type Written = Awaited<ReturnType<typeof personalApi.updateMyPreferences>>;

/** The account takes the zone. */
function accountTakes() {
  return vi
    .spyOn(personalApi, "updateMyPreferences")
    .mockResolvedValue({ data: null, error: null } as Written);
}

/**
 * The account refuses the zone with this status and this title.
 *
 * Status 0 is a request that got no answer at all, the way the client spells
 * a dead connection. The interceptor is not on the path here - the api module
 * is stubbed - so "announced by the interceptor" is a question of the status
 * alone, which is how announcedByInterceptor answers it in the running client
 * too.
 */
function accountRefuses(status: number, title = "") {
  return vi.spyOn(personalApi, "updateMyPreferences").mockResolvedValue({
    data: null,
    error: { type: "", title, status, traceId: "" },
  } as Written);
}

/** Presses the offer and waits for the server's answer to land. */
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

describe("the time zone offer", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
    setAccountTimeZone(null);
    const { toasts, dismiss } = useToast();
    [...toasts.value].forEach((toast) => dismiss(toast.id));
  });

  it("offers the device zone when the account keeps another one", async () => {
    accountKeeps("Europe/Moscow");

    const toasts = await run();

    expect(toasts).toHaveLength(1);
    // The sentence names the setting rather than either zone, and the verb
    // between the brackets is what the reader presses.
    expect(toasts[0].message).toBe(
      "Часовой пояс не совпадает с устройством, [синхронизировать]?",
    );
    expect(toasts[0].action).toBeTypeOf("function");
  });

  // An account with no zone keeps UTC, which is a zone like any other here.
  it("speaks up for an account that names no zone at all", async () => {
    accountKeeps(null);

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

  // One press and the account keeps the zone this device is in. A reader who
  // wants some other zone has the field in the settings; this is the answer for
  // the one who moved.
  it("writes the device zone when the reader takes the offer", async () => {
    accountKeeps("Europe/Moscow");
    const write = accountTakes();

    await run();
    const toasts = await press();

    expect(write).toHaveBeenCalledWith({ timeZoneId: DEVICE });
    // A press that went through is the answer: the question stays closed on
    // this device, and nothing is said about it - the offer is gone, and the
    // zones now agree.
    expect(localStorage.getItem(ASKED_KEY)).toBe(DEVICE);
    expect(toasts.filter((toast) => toast.type !== "info")).toHaveLength(0);
  });

  // A press the server refused is not an answer. The offer left the screen on
  // the press, and with the mark still standing there was no second try on this
  // device short of the settings page.
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

  // One carrier per refusal: the interceptor has already spoken for these, and
  // a second sentence in the corner would be the same failure told twice.
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

  // The statuses nobody else announces are this module's to say, in the
  // server's own words when it had any, and naming the setting when it did not.
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

  // The offer is not a question the reader has to answer twice.
  it("asks once per zone", async () => {
    accountKeeps("Europe/Moscow");

    await run();
    const { toasts, dismiss } = useToast();
    [...toasts.value].forEach((toast) => dismiss(toast.id));

    expect(await run()).toHaveLength(0);
  });

  // A device somewhere else is a new question, and it has not been answered.
  it("asks again when the device has moved somewhere else", async () => {
    accountKeeps("Europe/Moscow");
    localStorage.setItem(ASKED_KEY, "Asia/Tokyo");

    expect(await run()).toHaveLength(1);
  });
});

/**
 * The clock the page shows a signed-in reader's time by is the account's
 * (siteTime), and this is what hands it over: read from the preferences at the
 * start and at a sign-in, dropped at the sign-out, and kept on the device for
 * the first frame of the next visit.
 */
describe("the zone of the account on the page", () => {
  const ZONE_KEY = "dm3-account-time-zone";

  beforeEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
    setAccountTimeZone(null);
    const { toasts, dismiss } = useToast();
    [...toasts.value].forEach((toast) => dismiss(toast.id));
  });

  it("is the zone the preferences name", async () => {
    accountKeeps("Asia/Tokyo");

    await run();

    expect(accountTimeZone.value).toBe("Asia/Tokyo");
    expect(localStorage.getItem(ZONE_KEY)).toBe("Asia/Tokyo");
  });

  it("is UTC for an account that names none, as the letters read it", async () => {
    accountKeeps(null);

    await run();

    expect(accountTimeZone.value).toBe("UTC");
  });

  // The offer was answered on this device long ago, and the zone is still
  // read: the page needs it whether there is anything to offer or not.
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
