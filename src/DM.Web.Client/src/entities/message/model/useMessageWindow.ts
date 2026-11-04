import { computed, ref, type Ref } from "vue";
import type { CursorEnvelope, CursorPaging } from "@/shared/api/models/common";
import type { Chat, ChatId, Message, MessageId } from "./types";
import messagingApi from "../api/messagingApi";
import { createRequestGuard } from "@/shared/lib/utils/requestGuard";
import { fromSiteInput } from "@/shared/lib/utils/datetime";

const MAX_MESSAGES = 500;

const LOAD_FAILURE = "Не удалось загрузить сообщения";

// The server anchors "near" a moment on the first message at or after it.
const BEFORE_ANY_MESSAGE = new Date(0).toISOString();

// The calendar starts at the first message, asked once per correspondence unless the window holds it.
function firstMessage({
  messagesList,
  hasMoreBefore,
}: {
  messagesList: Ref<Message[]>;
  hasMoreBefore: Ref<boolean>;
}) {
  const askedFirstMessage = ref<string | null>(null);
  let firstMessageOf: ChatId | null = null;

  const firstMessageUtc = computed(() => {
    if (!hasMoreBefore.value && messagesList.value.length > 0) {
      return messagesList.value[0].createdUtc;
    }
    return askedFirstMessage.value;
  });

  function forgetFirstMessage() {
    askedFirstMessage.value = null;
    firstMessageOf = null;
  }

  async function fetchFirstMessage(chatId: ChatId) {
    if (!hasMoreBefore.value && messagesList.value.length > 0) return;
    if (firstMessageOf === chatId) return;
    firstMessageOf = chatId;
    const { data, error: apiError } = await messagingApi.getMessages(chatId, {
      nearTimestampUtc: BEFORE_ANY_MESSAGE,
      limit: 1,
    });
    // Another correspondence was opened meanwhile.
    if (firstMessageOf !== chatId) return;
    const first = data?.resources[0];
    if (apiError || !first) {
      firstMessageOf = null;
      return;
    }
    askedFirstMessage.value = first.createdUtc;
  }

  return { firstMessageUtc, forgetFirstMessage, fetchFirstMessage };
}

interface MessageWindowOptions {
  selectedChat: Ref<Chat | null>;
  pageSize: Ref<number>;
}

function newWindow({ selectedChat, pageSize }: MessageWindowOptions) {
  return {
    selectedChat,
    pageSize,
    messagesList: ref<Message[]>([]),
    loadingMessages: ref(false),
    loadingBefore: ref(false),
    // Apart from messagesError, so a failed page leaves the messages on screen.
    errorBefore: ref<string | null>(null),
    messagesError: ref<string | null>(null),
    hasMoreBefore: ref(false),
    hasMoreAfter: ref(false),
    currentCursor: ref<CursorPaging | null>(null),
    highlightedMessageId: ref<string | null>(null),
    loadingAfter: ref(false),
    errorAfter: ref<string | null>(null),
    // False when the jump fell back to another window, which is not the picked day.
    landedOnRequestedDate: ref(false),
    // Apart from the header's guard: the header and the window land separately.
    messagesGuard: createRequestGuard(),
  };
}

type MessageWindow = ReturnType<typeof newWindow>;

// Every replacement goes through here, so none keeps a cursor of the window before.
function takeWindow(
  w: MessageWindow,
  resources: Message[],
  paging: CursorPaging | null,
) {
  w.messagesList.value = resources;
  w.currentCursor.value = paging;
  w.hasMoreBefore.value = paging?.hasPrev ?? false;
  w.hasMoreAfter.value = paging?.hasNext ?? false;
}

// A whole new window; `take` runs only for the answer that is still current.
async function loadWindow(
  w: MessageWindow,
  chatId: ChatId,
  query: { aroundMessageId?: MessageId; nearTimestampUtc?: string },
  take: (page: CursorEnvelope<Message> | null) => void,
) {
  const requestId = w.messagesGuard.next();
  w.loadingMessages.value = true;
  // A failed page belongs to the window it happened in.
  w.errorBefore.value = null;
  w.errorAfter.value = null;
  try {
    const { data, error: apiError } = await messagingApi.getMessages(chatId, {
      ...query,
      limit: w.pageSize.value,
    });
    if (!w.messagesGuard.isCurrent(requestId)) return;
    if (apiError) {
      w.messagesError.value = LOAD_FAILURE;
      return;
    }
    w.messagesError.value = null;
    take(data);
  } finally {
    if (w.messagesGuard.isCurrent(requestId)) w.loadingMessages.value = false;
  }
}

function fetchMessages(w: MessageWindow, chatId: ChatId) {
  return loadWindow(w, chatId, {}, (page) =>
    takeWindow(w, page?.resources ?? [], page?.paging ?? null),
  );
}

// After a trim the cursor would skip what lies between it and the screen.
function pageAround(w: MessageWindow, chatId: ChatId, edge: Message) {
  return messagingApi.getMessages(chatId, {
    aroundMessageId: edge.id,
    limit: w.pageSize.value,
  });
}

function unheld(
  w: MessageWindow,
  page: CursorEnvelope<Message> | null | undefined,
) {
  const held = new Set(w.messagesList.value.map((m) => m.id));
  return (page?.resources ?? []).filter((m) => !held.has(m.id));
}

// Past MAX_MESSAGES the newest go, and with them the newer cursor.
function prependOlder(
  w: MessageWindow,
  page: CursorEnvelope<Message> | null | undefined,
) {
  const older = unheld(w, page);
  if (!page || older.length === 0) {
    w.hasMoreBefore.value = false;
    return;
  }
  w.messagesList.value = [...older, ...w.messagesList.value];
  w.currentCursor.value = {
    ...(w.currentCursor.value ?? page.paging),
    prevCursor: page.paging.prevCursor,
    hasPrev: page.paging.hasPrev,
  };
  w.hasMoreBefore.value = page.paging.hasPrev;
  if (w.messagesList.value.length > MAX_MESSAGES) {
    w.messagesList.value = w.messagesList.value.slice(0, MAX_MESSAGES);
    w.hasMoreAfter.value = true;
    w.currentCursor.value = {
      ...w.currentCursor.value,
      nextCursor: null,
      hasNext: true,
    };
  }
}

function appendNewer(
  w: MessageWindow,
  page: CursorEnvelope<Message> | null | undefined,
) {
  const newer = unheld(w, page);
  if (!page || newer.length === 0) {
    w.hasMoreAfter.value = false;
    return;
  }
  w.messagesList.value = [...w.messagesList.value, ...newer];
  w.currentCursor.value = {
    ...(w.currentCursor.value ?? page.paging),
    nextCursor: page.paging.nextCursor,
    hasNext: page.paging.hasNext,
  };
  w.hasMoreAfter.value = page.paging.hasNext;
  if (w.messagesList.value.length > MAX_MESSAGES) {
    w.messagesList.value = w.messagesList.value.slice(-MAX_MESSAGES);
    w.hasMoreBefore.value = true;
    w.currentCursor.value = {
      ...w.currentCursor.value,
      prevCursor: null,
      hasPrev: true,
    };
  }
}

async function fetchMoreBefore(w: MessageWindow) {
  const chat = w.selectedChat.value;
  const cursor = w.currentCursor.value?.prevCursor;
  const edge = w.messagesList.value[0];
  if (w.loadingBefore.value || !w.hasMoreBefore.value || !chat) return;
  if (!cursor && !edge) return;

  const heldWindow = w.messagesGuard.current();
  w.loadingBefore.value = true;
  w.errorBefore.value = null;
  try {
    const { data, error: apiError } = cursor
      ? await messagingApi.getMessagesBefore(chat.id, cursor, w.pageSize.value)
      : await pageAround(w, chat.id, edge);

    if (!w.messagesGuard.isCurrent(heldWindow)) return;
    if (apiError) {
      // hasMoreBefore stays, so the sentinel can retry.
      w.errorBefore.value = LOAD_FAILURE;
      return;
    }
    prependOlder(w, data);
  } finally {
    w.loadingBefore.value = false;
  }
}

async function fetchMoreAfter(w: MessageWindow) {
  const chat = w.selectedChat.value;
  const cursor = w.currentCursor.value?.nextCursor;
  const edge = w.messagesList.value[w.messagesList.value.length - 1];
  if (w.loadingAfter.value || !w.hasMoreAfter.value || !chat) return;
  if (!cursor && !edge) return;

  const heldWindow = w.messagesGuard.current();
  w.loadingAfter.value = true;
  w.errorAfter.value = null;
  try {
    const { data, error: apiError } = cursor
      ? await messagingApi.getMessagesAfter(chat.id, cursor, w.pageSize.value)
      : await pageAround(w, chat.id, edge);

    if (!w.messagesGuard.isCurrent(heldWindow)) return;
    if (apiError) {
      w.errorAfter.value = LOAD_FAILURE;
      return;
    }
    appendNewer(w, data);
  } finally {
    w.loadingAfter.value = false;
  }
}

async function jumpToLatest(w: MessageWindow) {
  if (!w.selectedChat.value) return;
  await fetchMessages(w, w.selectedChat.value.id);
  w.highlightedMessageId.value = null;
}

function navigateToMessage(
  w: MessageWindow,
  chatId: ChatId,
  messageId: MessageId,
) {
  return loadWindow(w, chatId, { aroundMessageId: messageId }, (page) => {
    if (page && page.resources.length > 0) {
      takeWindow(w, page.resources, page.paging ?? null);
      w.highlightedMessageId.value = messageId;
    }
  });
}

// Midnight on the site's clock, the zone the feed dates its messages in.
async function navigateToDate(w: MessageWindow, chatId: ChatId, date: string) {
  w.landedOnRequestedDate.value = false;
  const nearTimestampUtc = fromSiteInput(`${date}T00:00`);
  if (!nearTimestampUtc) return fetchMessages(w, chatId);
  const empty = await readDay(w, chatId, nearTimestampUtc);
  if (empty) await fetchMessages(w, chatId);
}

/** True when the server answered with nothing; a failure keeps the window and names itself. */
async function readDay(
  w: MessageWindow,
  chatId: ChatId,
  nearTimestampUtc: string,
): Promise<boolean> {
  let empty = false;
  await loadWindow(w, chatId, { nearTimestampUtc }, (page) => {
    if (!page?.resources.length) {
      empty = true;
      return;
    }
    takeWindow(w, page.resources, page.paging ?? null);
    w.landedOnRequestedDate.value = true;
  });
  return empty;
}

function clearHighlight(w: MessageWindow) {
  w.highlightedMessageId.value = null;
}

function clearWindow(w: MessageWindow) {
  takeWindow(w, [], null);
  w.highlightedMessageId.value = null;
  w.messagesError.value = null;
  w.errorBefore.value = null;
  w.errorAfter.value = null;
}

function emptyWindow(w: MessageWindow) {
  w.messagesGuard.next();
  clearWindow(w);
}

function resetWindow(w: MessageWindow, forgetFirstMessage: () => void) {
  clearWindow(w);
  w.landedOnRequestedDate.value = false;
  forgetFirstMessage();
}

export function useMessageWindow(options: MessageWindowOptions) {
  const w = newWindow(options);
  const { firstMessageUtc, forgetFirstMessage, fetchFirstMessage } =
    firstMessage(w);

  return {
    messagesList: w.messagesList,
    loadingMessages: w.loadingMessages,
    loadingBefore: w.loadingBefore,
    loadingAfter: w.loadingAfter,
    errorBefore: w.errorBefore,
    errorAfter: w.errorAfter,
    messagesError: w.messagesError,
    hasMoreBefore: w.hasMoreBefore,
    hasMoreAfter: w.hasMoreAfter,
    highlightedMessageId: w.highlightedMessageId,
    landedOnRequestedDate: w.landedOnRequestedDate,
    firstMessageUtc,
    fetchMessages: (chatId: ChatId) => fetchMessages(w, chatId),
    fetchMoreBefore: () => fetchMoreBefore(w),
    fetchMoreAfter: () => fetchMoreAfter(w),
    fetchFirstMessage,
    jumpToLatest: () => jumpToLatest(w),
    navigateToMessage: (chatId: ChatId, messageId: MessageId) =>
      navigateToMessage(w, chatId, messageId),
    navigateToDate: (chatId: ChatId, date: string) =>
      navigateToDate(w, chatId, date),
    clearHighlight: () => clearHighlight(w),
    // The store's own, kept out of what it exposes.
    forgetFirstMessage,
    emptyWindow: () => emptyWindow(w),
    resetWindow: () => resetWindow(w, forgetFirstMessage),
  };
}
