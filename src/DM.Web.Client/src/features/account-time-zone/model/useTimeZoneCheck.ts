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

// Only the reader writes the account's zone: a login that moved it would let a stolen session restamp it.
// The answer is kept per device, not per account; a plate, because neither zone is on the reader's screen.
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

    // No auto-dismiss: the offer is worth nothing if it runs out unseen.
    toast.info(
      "Часовой пояс не совпадает с устройством, [синхронизировать]?",
      0,
      () => adopt(device),
    );
    write(device);
  }

  // A refused press is not an answer: the mark comes off and the next start asks again.
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

  // Local storage is a convenience: a browser that refuses it is asked again.
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
      // The offer comes back next time.
    }
  }

  function forget() {
    try {
      localStorage.removeItem(DISMISSED_KEY);
    } catch {
      // A browser that refuses storage never kept the mark.
    }
  }

  // The first frame of a signed-in visit in the zone the last one left.
  const stored = storedAccountTimeZone();
  if (auth.isAuthenticated && stored) setAccountTimeZone(stored);

  check();
  watch(() => auth.isAuthenticated, check);
}
