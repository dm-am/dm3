import { watch } from "vue";
import { personalApi } from "@/entities/user";
import { useAuthStore } from "@/shared/stores/auth";
import { unwrapResource } from "@/shared/api/envelope";
import type { Preferences } from "@/shared/api/models/personal";
import { useToast } from "@/shared/lib/composables/useToast";
import { notifyFailure } from "@/shared/lib/errors";
import {
  deviceTimeZone,
  setAccountTimeZone,
  storedAccountTimeZone,
  UTC,
} from "@/shared/lib/utils/timeZone";

const DISMISSED_KEY = "dm3-time-zone-asked";

/**
 * Hands the page the zone of the account, and says when the account's zone and
 * the device's have parted, offering to move the first to the second.
 *
 * The page shows every time of a signed-in reader in the account's zone (the
 * owner's word; siteTime in shared/lib/utils/datetime.ts), so the zone is read
 * from the preferences at every start and every sign-in, and dropped at
 * sign-out, when the device's clock takes over. The last one read is kept on
 * the device for the first frame of the next visit.
 *
 * Nothing but the reader writes that zone. A login that moved it would let a
 * stolen session restamp the very message warning about it, and an evening on
 * somebody else's computer abroad would do the same by accident. The cost is
 * the reader who moves for good, and this is what hands them the way back.
 *
 * The offer writes the device zone and nothing else. A reader who wants some
 * other zone has the field in the account settings; this is the answer for the
 * one who moved, and that answer is one press.
 *
 * Said once per zone. The answer is kept on the device, not the account: it is
 * about this browser, and another one may want a different zone. A press the
 * server refused is not an answer, and it takes that mark off again: see
 * `adopt`.
 *
 * WHY A PLATE. Neither zone is on any screen the reader may be standing on:
 * the account's lives in a field of the settings page, the device's in the
 * browser, and the mismatch is found at start, before anything was pressed.
 * A band under the header would carry the same sentence, but the site has no
 * such band, and a wrong hour in a letter is not the kind of news that pushes
 * every page down. A line beside the settings field is already there - the
 * device zone heads that list, named as such - and it reaches only the reader
 * who already knows the field exists, which the one who moved does not.
 */
export function useTimeZoneCheck() {
  const auth = useAuthStore();
  const toast = useToast();

  async function check() {
    if (!auth.isAuthenticated) {
      setAccountTimeZone(null);
      return;
    }

    const { data } = await personalApi.getMyPreferences();
    const preferences = unwrapResource<Preferences>(data);
    // Signed out while the request was out: the answer is for nobody now.
    if (!preferences || !auth.isAuthenticated) return;

    const account = preferences.timeZoneId || UTC;
    setAccountTimeZone(account);

    const device = deviceTimeZone();
    if (!device || read() === device) return;
    if (account === device) return;

    // No auto-dismiss: the offer is worth nothing if it runs out while the
    // reader is looking at something else.
    //
    // The sentence names the thing the way the account settings name it, the
    // field it offers to write being labelled "Часовой пояс" there. Neither
    // zone is spelled out: the database name is not what a clock shows
    // ("Europe/Moscow"), and an offset is a reading of the clock rather than
    // the setting, which is what the press changes.
    toast.info(
      "Часовой пояс не совпадает с устройством, [синхронизировать]?",
      0,
      () => adopt(device),
    );
    write(device);
  }

  /**
   * Writes the device zone to the account, and on a refusal reopens the
   * question instead of leaving a dead end.
   *
   * The offer left the screen the moment it was pressed: the container
   * dismisses a plate on its press, before the server has answered. Before
   * this, a refused press then left one red line and no second try anywhere
   * but the settings page, because the mark written at the offer said the
   * question was answered for good. A refused press is not an answer, so the
   * mark comes off, and the next start of the site or the next sign-in asks
   * again. That is the way back, and it costs no plate.
   *
   * No plate of its own with a retry press either. Every refusal this write
   * can meet in practice - a dead connection, an expired session, a rate
   * limit, a 5xx - is one the interceptor has already announced, and a second
   * plate here would be the same failure told twice; the ones left to this
   * function (a zone name over 64 characters, an account that is gone) do not
   * pass on a second press. And the moment a transient failure has passed is
   * exactly the next start: a session that expired is signed in again, and the
   * watch below asks on that very sign-in.
   */
  async function adopt(device: string) {
    const { error } = await personalApi.updateMyPreferences({
      timeZoneId: device,
    });
    if (!error) {
      setAccountTimeZone(device);
      return;
    }

    forget();
    notifyFailure(error, "Не удалось сменить часовой пояс");
  }

  /**
   * Local storage is a convenience and not a store: a browser that refuses it
   * asks again, which is the worse of the two failures and not a broken page.
   */
  function read(): string {
    try {
      return localStorage.getItem(DISMISSED_KEY) ?? "";
    } catch {
      return "";
    }
  }

  function write(zone: string) {
    try {
      localStorage.setItem(DISMISSED_KEY, zone);
    } catch {
      // Nothing to do: the offer comes back next time, and that is all.
    }
  }

  function forget() {
    try {
      localStorage.removeItem(DISMISSED_KEY);
    } catch {
      // A browser that refuses storage never kept the mark to begin with.
    }
  }

  // The first frame of a signed-in visit in the zone the last one left, not
  // in the device's clock until the preferences arrive.
  const stored = storedAccountTimeZone();
  if (auth.isAuthenticated && stored) setAccountTimeZone(stored);

  check();
  watch(() => auth.isAuthenticated, check);
}
