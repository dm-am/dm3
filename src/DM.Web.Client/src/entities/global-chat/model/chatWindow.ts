import { ref } from "vue";
import type { GlobalChatMessage } from "./types";
import type { CursorPaging } from "@/shared/api/models/common";
import globalChatApi from "../api/globalChatApi";
import { createRequestGuard } from "@/shared/lib/utils/requestGuard";
import { fromSiteInput } from "@/shared/lib/utils/datetime";

const MAX_MESSAGES = 500;

// A short answer means the server had nothing more: a quiet chat, not a drifted window.
export const LATEST_PAGE_SIZE = 50;

const LOAD_FAILURE = "Не удалось загрузить сообщения";

export function newChatWindow() {
  return {
    messages: ref<GlobalChatMessage[]>([]),
    loading: ref(false),
    error: ref<string | null>(null),
    loadingBefore: ref(false),
    loadingAfter: ref(false),
    hasMoreBefore: ref(true),
    hasMoreAfter: ref(false),
    highlightedMessageId: ref<string | null>(null),
    // Apart from `error`, so a failed page leaves the messages on screen.
    errorBefore: ref<string | null>(null),
    errorAfter: ref<string | null>(null),
    prevCursor: ref<string | null>(null),
    nextCursor: ref<string | null>(null),
    reanchorBefore: ref<string | null>(null),
    // False when the jump fell back to the latest messages, which are not the picked day.
    landedOnRequestedDate: ref(false),
    messagesById: new Map<string, GlobalChatMessage>(),
    // Bumped on every replacement of the window: an answer for the old one must not merge in.
    windowGuard: createRequestGuard(),
  };
}

export type ChatWindow = ReturnType<typeof newChatWindow>;

export function syncMessagesMap(w: ChatWindow) {
  w.messagesById.clear();
  for (const msg of w.messages.value) {
    w.messagesById.set(msg.id, msg);
  }
}

// The old cursor would skip what the trim dropped, so the next scroll up re-anchors on a message.
export function trimOldMessages(w: ChatWindow) {
  if (w.messages.value.length > MAX_MESSAGES) {
    w.messages.value = w.messages.value.slice(-MAX_MESSAGES);
    w.hasMoreBefore.value = true;
    w.prevCursor.value = null;
    w.reanchorBefore.value = w.messages.value[0]?.id ?? null;
    syncMessagesMap(w);
  }
}

export function showPage(
  w: ChatWindow,
  resources: GlobalChatMessage[],
  paging: CursorPaging | null | undefined,
) {
  w.windowGuard.next();
  w.messages.value = resources;
  w.prevCursor.value = paging?.prevCursor ?? null;
  w.nextCursor.value = paging?.nextCursor ?? null;
  w.hasMoreBefore.value = paging?.hasPrev ?? false;
  w.hasMoreAfter.value = paging?.hasNext ?? false;
  syncMessagesMap(w);
}

export async function fetchMessages(w: ChatWindow) {
  w.loading.value = true;
  w.error.value = null;
  try {
    const { data, error: apiError } = await globalChatApi.getMessages({
      limit: LATEST_PAGE_SIZE,
    });
    if (apiError) {
      w.error.value = LOAD_FAILURE;
      return;
    }
    showPage(w, data?.resources || [], data?.paging);
  } finally {
    w.loading.value = false;
  }
}

export async function fetchMoreBefore(w: ChatWindow) {
  if (w.loadingBefore.value || !w.hasMoreBefore.value) return;
  if (!w.prevCursor.value && !w.reanchorBefore.value) return;
  w.loadingBefore.value = true;
  w.errorBefore.value = null;
  try {
    const { data, error: apiError } = w.prevCursor.value
      ? await globalChatApi.getMessagesBefore(
          w.prevCursor.value,
          LATEST_PAGE_SIZE,
        )
      : await globalChatApi.getMessagesAround(
          w.reanchorBefore.value!,
          LATEST_PAGE_SIZE,
        );
    if (apiError) {
      // hasMoreBefore stays, so the sentinel can retry.
      w.errorBefore.value = LOAD_FAILURE;
      return;
    }
    // A re-anchored page is centred on a message on screen, so its newer half goes.
    const older = data
      ? data.resources.filter((message) => !w.messagesById.has(message.id))
      : [];

    if (older.length > 0) {
      w.messages.value = [...older, ...w.messages.value];
      w.prevCursor.value = data?.paging?.prevCursor ?? null;
      w.hasMoreBefore.value = data?.paging?.hasPrev ?? false;
      w.reanchorBefore.value = null;
      syncMessagesMap(w);
    } else {
      w.hasMoreBefore.value = false;
    }
  } finally {
    w.loadingBefore.value = false;
  }
}

export async function fetchMoreAfter(w: ChatWindow) {
  if (w.loadingAfter.value || !w.hasMoreAfter.value || !w.nextCursor.value)
    return;
  w.loadingAfter.value = true;
  w.errorAfter.value = null;
  try {
    const { data, error: apiError } = await globalChatApi.getMessagesAfter(
      w.nextCursor.value,
      LATEST_PAGE_SIZE,
    );
    if (apiError) {
      w.errorAfter.value = LOAD_FAILURE;
      return;
    }
    if (data && data.resources.length > 0) {
      w.messages.value = [...w.messages.value, ...data.resources];
      w.nextCursor.value = data.paging?.nextCursor ?? null;
      w.hasMoreAfter.value = data.paging?.hasNext ?? false;
      syncMessagesMap(w);
      trimOldMessages(w);
    } else {
      w.hasMoreAfter.value = false;
    }
  } finally {
    w.loadingAfter.value = false;
  }
}

export async function navigateToMessage(w: ChatWindow, messageId: string) {
  w.loading.value = true;
  w.error.value = null;
  try {
    const { data, error: apiError } = await globalChatApi.getMessagesAround(
      messageId,
      LATEST_PAGE_SIZE,
    );
    if (apiError) {
      w.error.value = LOAD_FAILURE;
      return;
    }
    if (data && data.resources.length > 0) {
      showPage(w, data.resources, data.paging);
      w.highlightedMessageId.value = messageId;
    }
  } finally {
    w.loading.value = false;
  }
}

export async function navigateToDate(w: ChatWindow, date: string) {
  w.loading.value = true;
  w.error.value = null;
  w.landedOnRequestedDate.value = false;
  try {
    // Midnight on the site's clock, the zone the chat's dates are shown in.
    const timestampUtc = fromSiteInput(`${date}T00:00`);
    if (!timestampUtc) {
      await fetchMessages(w);
      return;
    }
    const { data, error: apiError } = await globalChatApi.getMessagesNearDate(
      timestampUtc,
      LATEST_PAGE_SIZE,
    );
    if (apiError) {
      w.error.value = LOAD_FAILURE;
      return;
    }
    if (data && data.resources.length > 0) {
      showPage(w, data.resources, data.paging);
      // No highlight: the feed's history window finds the first message of the day.
      w.landedOnRequestedDate.value = true;
    } else {
      await fetchMessages(w);
    }
  } catch {
    await fetchMessages(w);
  } finally {
    w.loading.value = false;
  }
}

export async function jumpToLatest(w: ChatWindow) {
  await fetchMessages(w);
  w.highlightedMessageId.value = null;
}

export function clearHighlight(w: ChatWindow) {
  w.highlightedMessageId.value = null;
}
