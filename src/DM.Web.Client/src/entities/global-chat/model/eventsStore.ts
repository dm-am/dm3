import { defineStore } from "pinia";
import { computed, ref } from "vue";
import type { GlobalChatEvent, GlobalChatEventSummary } from "./types";
import type {
  ApiResult,
  Envelope,
  GeneralError,
} from "@/shared/api/models/common";
import globalChatApi from "../api/globalChatApi";
import { createRequestGuard } from "@/shared/lib/utils/requestGuard";
import { createFreshness } from "@/shared/lib/utils/keyedCache";
import { NotificationType } from "@/shared/api/models/notifications";

// Apart from the window of messages, with which the events share nothing.
export const useGlobalChatEventsStore = defineStore("globalChatEvents", () => {
  const events = ref<GlobalChatEventSummary[]>([]);
  /** By event id, read on demand. */
  const eventDetails = ref<Record<string, GlobalChatEvent>>({});
  const eventDetailsLoading = ref<Record<string, boolean>>({});

  /** The server allows one at most. */
  const liveEvent = computed(
    () => events.value.find((e) => e.status === "Live") ?? null,
  );

  /** Soonest first; the live event has a banner of its own. */
  const upcomingEvents = computed(() =>
    events.value
      .filter((e) => e.status === "Scheduled")
      .sort((a, b) => (a.startsUtc < b.startsUtc ? -1 : 1)),
  );

  // The mount, the socket and a push all read the list, and the slowest answer must not stay.
  const eventsGuard = createRequestGuard();

  // Covers the read on open and the connection after it; a real outage falls outside.
  const EVENTS_FRESH_MS = 5000;

  const eventsFreshness = createFreshness(EVENTS_FRESH_MS);

  // An empty list says "no events" only once a read has answered with one.
  const eventsLoad = ref<"pending" | "loaded" | "failed">("pending");

  async function fetchEvents() {
    const request = eventsGuard.next();
    // Before the request, so a caller asking meanwhile does not ask again.
    eventsFreshness.touch();
    const { data, error: apiError } = await globalChatApi.getEvents();
    if (!eventsGuard.isCurrent(request)) return;
    if (apiError) {
      eventsFreshness.clear();
      eventsLoad.value = "failed";
      return;
    }
    events.value = data?.resources ?? [];
    eventsLoad.value = "loaded";
  }

  async function fetchEventsIfFresh() {
    if (eventsFreshness.isFresh()) return;
    await fetchEvents();
  }

  // Only a start or an end makes the list stale; any other push would cost a request per line.
  async function refreshEventsOnNotification(eventType: NotificationType) {
    if (
      eventType !== NotificationType.GlobalChatEventStarted &&
      eventType !== NotificationType.GlobalChatEventEnded
    ) {
      return;
    }
    await fetchEvents();
  }

  async function fetchEventDetails(
    id: string,
    force = false,
  ): Promise<GlobalChatEvent | null> {
    if (!force && eventDetails.value[id]) return eventDetails.value[id];
    if (eventDetailsLoading.value[id]) return null;
    eventDetailsLoading.value[id] = true;
    try {
      const { data, error: apiError } = await globalChatApi.getEvent(id);
      if (apiError || !data?.resource) return null;
      eventDetails.value[id] = data.resource;
      return data.resource;
    } finally {
      eventDetailsLoading.value[id] = false;
    }
  }

  // The list is reread too: the status and the count the strip shows live only there.
  async function runEventAction(
    id: string,
    act: () => Promise<ApiResult<Envelope<GlobalChatEvent>>>,
  ): Promise<{ error: GeneralError | null }> {
    const { data, error } = await act();
    if (error) return { error };
    if (data?.resource) eventDetails.value[id] = data.resource;
    await fetchEvents();
    return { error: null };
  }

  const joinEvent = (id: string) =>
    runEventAction(id, () => globalChatApi.joinEvent(id));

  const leaveEvent = (id: string) =>
    runEventAction(id, () => globalChatApi.leaveEvent(id));

  const startEvent = (id: string) =>
    runEventAction(id, () => globalChatApi.startEvent(id));

  const endEvent = (id: string) =>
    runEventAction(id, () => globalChatApi.endEvent(id));

  return {
    events,
    eventsLoad,
    eventDetails,
    liveEvent,
    upcomingEvents,
    fetchEvents,
    fetchEventsIfFresh,
    refreshEventsOnNotification,
    fetchEventDetails,
    joinEvent,
    leaveEvent,
    startEvent,
    endEvent,
  };
});
