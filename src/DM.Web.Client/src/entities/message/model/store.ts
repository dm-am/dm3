import { defineStore, storeToRefs } from "pinia";
import { ref, computed } from "vue";
import type {
  ChatUnreadCount,
  ListEnvelope,
  CursorPaging,
} from "@/shared/api/models/common";
import type { Chat, ChatId, Message, MessageId } from "./types";
import type { Username } from "@/shared/api/models/common";
import messagingApi from "../api/messagingApi";
import { unwrapResource } from "@/shared/api";
import { useAuthStore } from "@/shared/stores";
import { usePaging } from "@/shared/lib/composables/usePaging";
import { createRequestGuard } from "@/shared/lib/utils/requestGuard";
import { startStopwatch, type Stopwatch } from "@/shared/lib/utils/keyedCache";
import { fromSiteInput } from "@/shared/lib/utils/datetime";

const MAX_MESSAGES = 500;

/**
 * An instant before any message: the server anchors a window "near" a moment
 * on the first message at or after it, so near this one it is the first
 * message of the correspondence.
 */
const BEFORE_ANY_MESSAGE = new Date(0).toISOString();

export const useMessagingStore = defineStore("messaging", () => {
  const { user: currentUser } = storeToRefs(useAuthStore());
  // Both sizes are the reader's own. The message window used to hold a constant
  // of its own, so the "messages per page" preference was saved and moved
  // nothing.
  const { entitiesPerPage, messagesPerPage } = usePaging();

  // Error state for messaging operations
  const error = ref<string | null>(null);

  // Chats list
  const chats = ref<ListEnvelope<Chat> | null>(null);
  const loadingChats = ref(false);

  async function fetchChats(number: number = 1) {
    loadingChats.value = true;
    try {
      const take = entitiesPerPage.value;
      const { data, error: err } = await messagingApi.getChats({
        number,
        take,
      });
      if (err) {
        error.value = "Не удалось загрузить переписки";
        chats.value = null;
        return;
      }
      error.value = null;
      chats.value = data ?? null;
    } finally {
      loadingChats.value = false;
    }
  }

  // Selected chat
  const selectedChat = ref<Chat | null>(null);
  const loadingChat = ref(false);
  /**
   * The server answered that there is no such correspondence for this reader
   * (404). The only state in which the page may say "Переписка не найдена":
   * a correspondence still on its way, and one the network failed to bring,
   * are not missing, and saying so sent the reader back to the list over a
   * conversation that was there.
   */
  const chatMissing = ref(false);
  /** The correspondence did not load for another reason; the page offers a retry. */
  const chatError = ref<string | null>(null);

  // One token per slice, as in the game details store: the conversation header
  // and the message window are fetched by separate calls and land separately,
  // so a single counter would let each cancel the other.
  //
  // Two quick moves between conversations put two of each on the wire, and the
  // first answer arriving last used to overwrite the state — the reader saw a
  // conversation they had navigated away from, while mark-as-read had already
  // been sent for it.
  const chatGuard = createRequestGuard();
  const messagesGuard = createRequestGuard();

  async function selectChat(id: ChatId) {
    const requestId = chatGuard.next();
    // The page of a correspondence stays mounted from one to the next, and
    // the first day asked of the one before is not this one's.
    if (selectedChat.value?.id !== id) forgetFirstMessage();
    loadingChat.value = true;
    chatMissing.value = false;
    chatError.value = null;
    try {
      const { data, error: err } = await messagingApi.getChat(id);
      if (!chatGuard.isCurrent(requestId)) return;
      // The failure is the correspondence's own, not the list's: `error` is
      // what the list of correspondences shows under its heading.
      if (err) {
        selectedChat.value = null;
        if (err.status === 404) chatMissing.value = true;
        else chatError.value = "Не удалось загрузить переписку";
        return;
      }
      selectedChat.value = data ?? null;
      chatMissing.value = !data;
    } finally {
      if (chatGuard.isCurrent(requestId)) loadingChat.value = false;
    }
  }

  async function selectDirectChat(username: Username) {
    const requestId = chatGuard.next();
    loadingChat.value = true;
    try {
      const { data, error: err } =
        await messagingApi.getOrCreateDirectChat(username);
      // The caller navigates by the id it gets back, so a stale answer still
      // returns its own chat — it just does not touch the store on the way.
      if (!chatGuard.isCurrent(requestId)) return data ?? null;
      if (err) {
        error.value = "Не удалось загрузить прямую переписку";
        selectedChat.value = null;
        return null;
      }
      error.value = null;
      selectedChat.value = data ?? null;
      return data ?? null;
    } finally {
      if (chatGuard.isCurrent(requestId)) loadingChat.value = false;
    }
  }

  // Messages in selected chat (flat array for easier manipulation)
  const messagesList = ref<Message[]>([]);
  const loadingMessages = ref(false);
  const loadingBefore = ref(false);
  /** Failure of the last "load older" page, kept apart from `error` (the whole
   * view's failure) so a refused page-2 request leaves the rendered messages
   * alone; surfaced at the top sentinel with a retry, same as the global
   * chat's errorBefore. */
  const errorBefore = ref<string | null>(null);
  /**
   * Failure of the window itself (the latest page, the page around a message).
   * The messages already on screen stay; the feed shows the red box with a
   * retry where it would otherwise call an unread correspondence empty.
   */
  const messagesError = ref<string | null>(null);
  const hasMoreBefore = ref(false);
  const hasMoreAfter = ref(false);
  const currentCursor = ref<CursorPaging | null>(null);
  const highlightedMessageId = ref<string | null>(null);
  const loadingAfter = ref(false);
  /** Failure of the last "load newer" page, at the bottom sentinel with a retry. */
  const errorAfter = ref<string | null>(null);

  /**
   * The window as a page of the server laid it: the messages, both cursors and
   * whether there is more on either side. Every replacement of the window goes
   * through here, so none of them keeps a cursor of the window before it.
   */
  function takeWindow(resources: Message[], paging: CursorPaging | null) {
    messagesList.value = resources;
    currentCursor.value = paging;
    hasMoreBefore.value = paging?.hasPrev ?? false;
    hasMoreAfter.value = paging?.hasNext ?? false;
  }

  // Fetch initial messages (latest messages)
  async function fetchMessages(chatId: ChatId) {
    const requestId = messagesGuard.next();
    loadingMessages.value = true;
    // A fresh window carries no failed page with it: the error belongs to the
    // window it happened in, and keeping it would park the new one's sentinel.
    errorBefore.value = null;
    errorAfter.value = null;
    try {
      const { data, error: apiError } = await messagingApi.getMessages(chatId, {
        limit: messagesPerPage.value,
      });

      if (!messagesGuard.isCurrent(requestId)) return;
      if (apiError) {
        messagesError.value = "Не удалось загрузить сообщения";
        return;
      }
      messagesError.value = null;
      takeWindow(data?.resources ?? [], data?.paging ?? null);
    } finally {
      if (messagesGuard.isCurrent(requestId)) loadingMessages.value = false;
    }
  }

  /**
   * The page around the message at an edge of the window, for when the cursor
   * of that side is gone. A trim takes the messages the cursor was cut
   * against, so after one the cursor would skip whatever lies between it and
   * what stays on screen; the edge message asks for the page that touches the
   * window instead, and the part of it already held is left out by the caller.
   */
  function pageAround(chatId: ChatId, edge: Message) {
    return messagingApi.getMessages(chatId, {
      aroundMessageId: edge.id,
      limit: messagesPerPage.value,
    });
  }

  // Load older messages (scroll up)
  async function fetchMoreBefore() {
    const chat = selectedChat.value;
    const cursor = currentCursor.value?.prevCursor;
    const edge = messagesList.value[0];
    if (loadingBefore.value || !hasMoreBefore.value || !chat) return;
    if (!cursor && !edge) return;

    // A page that lands after the window was replaced (a date, a message, the
    // latest) belongs to the window before it.
    const heldWindow = messagesGuard.current();
    loadingBefore.value = true;
    errorBefore.value = null;
    try {
      const { data, error: apiError } = cursor
        ? await messagingApi.getMessagesBefore(
            chat.id,
            cursor,
            messagesPerPage.value,
          )
        : await pageAround(chat.id, edge);

      if (!messagesGuard.isCurrent(heldWindow)) return;
      if (apiError) {
        // hasMoreBefore stays as it was. Clearing it on a failure would tell
        // the list the history had ended: the sentinel unmounts, and the rest
        // of the correspondence is unreachable until the chat is reopened —
        // with nothing on screen saying why. The sentinel keeps its place and
        // offers the retry instead.
        errorBefore.value = "Не удалось загрузить сообщения";
        return;
      }

      const held = new Set(messagesList.value.map((m) => m.id));
      const older = (data?.resources ?? []).filter((m) => !held.has(m.id));
      if (data && older.length > 0) {
        // Prepend older messages
        messagesList.value = [...older, ...messagesList.value];
        currentCursor.value = {
          ...(currentCursor.value ?? data.paging),
          prevCursor: data.paging.prevCursor,
          hasPrev: data.paging.hasPrev,
        };
        hasMoreBefore.value = data.paging.hasPrev;
        // Trim excess messages from the end to prevent unbounded growth. The
        // cursor of the newer side was cut against what is dropped here, so
        // it goes: the next page down is asked around the new last message.
        if (messagesList.value.length > MAX_MESSAGES) {
          messagesList.value = messagesList.value.slice(0, MAX_MESSAGES);
          hasMoreAfter.value = true;
          currentCursor.value = {
            ...currentCursor.value,
            nextCursor: null,
            hasNext: true,
          };
        }
      } else {
        hasMoreBefore.value = false;
      }
    } finally {
      loadingBefore.value = false;
    }
  }

  /**
   * Load newer messages (scroll down): the way from a day or a message the
   * reader landed on back toward the latest ones. The window keeps no more
   * than MAX_MESSAGES, so the oldest go once it grows past that, and the
   * cursor of the older side with them.
   */
  async function fetchMoreAfter() {
    const chat = selectedChat.value;
    const cursor = currentCursor.value?.nextCursor;
    const edge = messagesList.value[messagesList.value.length - 1];
    if (loadingAfter.value || !hasMoreAfter.value || !chat) return;
    if (!cursor && !edge) return;

    const heldWindow = messagesGuard.current();
    loadingAfter.value = true;
    errorAfter.value = null;
    try {
      const { data, error: apiError } = cursor
        ? await messagingApi.getMessagesAfter(
            chat.id,
            cursor,
            messagesPerPage.value,
          )
        : await pageAround(chat.id, edge);

      if (!messagesGuard.isCurrent(heldWindow)) return;
      if (apiError) {
        // As on the older side: the sentinel stays and offers the retry.
        errorAfter.value = "Не удалось загрузить сообщения";
        return;
      }

      const held = new Set(messagesList.value.map((m) => m.id));
      const newer = (data?.resources ?? []).filter((m) => !held.has(m.id));
      if (data && newer.length > 0) {
        messagesList.value = [...messagesList.value, ...newer];
        currentCursor.value = {
          ...(currentCursor.value ?? data.paging),
          nextCursor: data.paging.nextCursor,
          hasNext: data.paging.hasNext,
        };
        hasMoreAfter.value = data.paging.hasNext;
        if (messagesList.value.length > MAX_MESSAGES) {
          messagesList.value = messagesList.value.slice(-MAX_MESSAGES);
          hasMoreBefore.value = true;
          currentCursor.value = {
            ...currentCursor.value,
            prevCursor: null,
            hasPrev: true,
          };
        }
      } else {
        hasMoreAfter.value = false;
      }
    } finally {
      loadingAfter.value = false;
    }
  }

  // Jump to latest messages
  async function jumpToLatest() {
    if (!selectedChat.value) return;
    await fetchMessages(selectedChat.value.id);
    highlightedMessageId.value = null;
  }

  // Load the window around a specific message (jump-to-context from search).
  // Mirrors the global-chat store's navigateToMessage: fetches with
  // aroundMessageId, replaces the list, and flags the message for the view to
  // scroll/highlight.
  async function navigateToMessage(chatId: ChatId, messageId: MessageId) {
    const requestId = messagesGuard.next();
    loadingMessages.value = true;
    errorBefore.value = null;
    errorAfter.value = null;
    try {
      const { data, error: apiError } = await messagingApi.getMessages(chatId, {
        aroundMessageId: messageId,
        limit: messagesPerPage.value,
      });
      if (!messagesGuard.isCurrent(requestId)) return;
      if (apiError) {
        messagesError.value = "Не удалось загрузить сообщения";
        return;
      }
      messagesError.value = null;
      if (data && data.resources.length > 0) {
        takeWindow(data.resources, data.paging ?? null);
        highlightedMessageId.value = messageId as unknown as string;
      }
    } finally {
      if (messagesGuard.isCurrent(requestId)) loadingMessages.value = false;
    }
  }

  /**
   * navigateToDate stood the feed near the day it was asked for. False when the
   * correspondence has no message at all, when the day is not a day, or when
   * the request failed: the window is then the latest one (or, after a
   * failure, the one that was there), and the page must not present it as the
   * picked day.
   */
  const landedOnRequestedDate = ref(false);

  /**
   * The window of a day ("Перейти к дате"), the global chat's: the messages
   * around the first one written at or after midnight of the day on the site's
   * clock, the clock the feed dates them by, about half of the window before
   * it. The server anchors on the last message before the moment when nothing
   * comes after it, so a day with no message of its own still has a window:
   * the feed stands at its top, the page finds the day's first message in it
   * where there is one (useChatHistoryWindow.loadDate).
   */
  async function navigateToDate(chatId: ChatId, date: string) {
    landedOnRequestedDate.value = false;
    const nearTimestampUtc = fromSiteInput(`${date}T00:00`);
    // Not a day at all (?date= typed by hand): the latest messages.
    if (!nearTimestampUtc) return fetchMessages(chatId);
    const empty = await readDay(chatId, nearTimestampUtc);
    if (empty) await fetchMessages(chatId);
  }

  /**
   * Takes the window near the day. Answers true when the server answered with
   * nothing at all; a failure is not an empty window, it keeps the window
   * there was and names itself.
   */
  async function readDay(
    chatId: ChatId,
    nearTimestampUtc: string,
  ): Promise<boolean> {
    const requestId = messagesGuard.next();
    loadingMessages.value = true;
    errorBefore.value = null;
    errorAfter.value = null;
    try {
      const { data, error: apiError } = await messagingApi.getMessages(chatId, {
        nearTimestampUtc,
        limit: messagesPerPage.value,
      });
      if (!messagesGuard.isCurrent(requestId)) return false;
      if (apiError) {
        messagesError.value = "Не удалось загрузить сообщения";
        return false;
      }
      messagesError.value = null;
      const resources = data?.resources ?? [];
      if (!resources.length) return true;
      takeWindow(resources, data?.paging ?? null);
      landedOnRequestedDate.value = true;
      return false;
    } finally {
      if (messagesGuard.isCurrent(requestId)) loadingMessages.value = false;
    }
  }

  /**
   * When the first message of the open correspondence was written: the
   * calendar of "Перейти к дате" offers the days from it to today. A window
   * that reaches the start of the history holds it already; otherwise it is
   * asked of the server once per correspondence, near an instant before any
   * message, which is where the server finds the first one.
   */
  const askedFirstMessage = ref<string | null>(null);
  let firstMessageOf: ChatId | null = null;

  const firstMessageUtc = computed(() => {
    if (!hasMoreBefore.value && messagesList.value.length > 0) {
      return messagesList.value[0].createdUtc;
    }
    return askedFirstMessage.value;
  });

  /** The first message asked of no correspondence; an answer on its way is dropped. */
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
    const first = data?.resources?.[0];
    if (apiError || !first) {
      // Asked again next time: the calendar stays open-ended until then.
      firstMessageOf = null;
      return;
    }
    askedFirstMessage.value = first.createdUtc;
  }

  function clearHighlight() {
    highlightedMessageId.value = null;
  }

  // Interlocutor (the other participant in a chat)
  const interlocutor = computed(() => {
    if (!selectedChat.value) return null;
    const participants = selectedChat.value.participants;
    if (!participants || participants.length === 0) return null;

    // If only one participant, that's the interlocutor (API might not include current user)
    if (participants.length === 1) {
      return participants[0];
    }

    // If current user is known, find the other participant (case-insensitive)
    if (currentUser.value?.username) {
      const currentUsername = currentUser.value.username.toLowerCase();
      const other = participants.find(
        (p) => p.username.toLowerCase() !== currentUsername,
      );
      if (other) return other;
    }

    // Fallback: return the first participant that's not obviously the current user
    return participants[0];
  });

  /**
   * Total unread messages over every conversation, as the server counts them.
   *
   * It used to be added up here from `chats`, which holds one page: the badge
   * left out every unread conversation below that page, and it stood at zero
   * for as long as the list had not been loaded.
   */
  const totalUnreadCount = ref(0);

  // Debounce state for fetchUnreadCount (prevents request flood from SignalR)
  let fetchUnreadDebounceTimer: ReturnType<typeof setTimeout> | null = null;
  const FETCH_UNREAD_DEBOUNCE_MS = 2000; // 2 seconds

  // Ceiling on the debounce, the same one the bell keeps. Every deferred call
  // used to restart the wait, so a stream of pushes spaced closer than the
  // debounce postponed the request for as long as it lasted, and the badge
  // stood still exactly while there was most to report. The wait is now
  // bounded: whatever the traffic, the count is re-read at least this often.
  // What is left of the ceiling is asked of the shared stopwatch rather than
  // measured here, so elapsed time is decided in the one place the client keeps
  // that arithmetic.
  const FETCH_UNREAD_MAX_WAIT_MS = 10000; // 10 seconds
  let deferredSince: Stopwatch | null = null;

  /**
   * Fetch the unread messages count.
   * @param immediate - If true, fetches immediately (for app start). If false, debounces (for SignalR).
   */
  async function fetchUnreadCount(immediate = false) {
    if (!currentUser.value) return;

    // Clear any pending debounced fetch
    if (fetchUnreadDebounceTimer) {
      clearTimeout(fetchUnreadDebounceTimer);
      fetchUnreadDebounceTimer = null;
    }

    const doFetch = async () => {
      deferredSince = null;
      const { data } = await messagingApi.getUnreadCount();
      // Enveloped, as every new single-resource body is; unwrapResource is the
      // one place that read is written.
      const counted = unwrapResource<ChatUnreadCount>(data);
      if (counted) {
        totalUnreadCount.value = counted.count;
      }
    };

    if (immediate) {
      await doFetch();
    } else {
      // Debounce: wait before fetching (batches rapid SignalR messages),
      // but never past the ceiling counted from the first deferred call.
      if (deferredSince === null) deferredSince = startStopwatch();
      const delay = Math.min(
        FETCH_UNREAD_DEBOUNCE_MS,
        deferredSince.remainingMs(FETCH_UNREAD_MAX_WAIT_MS),
      );
      fetchUnreadDebounceTimer = setTimeout(doFetch, delay);
    }
  }

  // Mark chat as read
  async function markAsRead(chatId: ChatId) {
    await messagingApi.markAsRead(chatId);
    // The badge is the server's number now, and nothing re-reads it after a
    // receipt: while it was summed from `chats`, zeroing the entry below took
    // it down by itself. Without this the header went on counting the messages
    // the reader had just opened, until the next push or reconnect.
    const known =
      chats.value?.resources.find((c) => c.id === chatId) ??
      (selectedChat.value?.id === chatId ? selectedChat.value : null);
    const read = known?.unreadMessagesCount ?? 0;
    totalUnreadCount.value = Math.max(0, totalUnreadCount.value - read);
    // Update local state
    if (selectedChat.value?.id === chatId) {
      (selectedChat.value as Chat).unreadMessagesCount =
        0 as Chat["unreadMessagesCount"];
    }
    if (chats.value) {
      const chat = chats.value.resources.find((c) => c.id === chatId);
      if (chat) {
        (chat as Chat).unreadMessagesCount = 0 as Chat["unreadMessagesCount"];
      }
    }
  }

  /** What the chat had unread for the reader, wherever the store holds it. */
  function unreadOf(chatId: ChatId): number {
    const known =
      chats.value?.resources.find((c) => c.id === chatId) ??
      (selectedChat.value?.id === chatId ? selectedChat.value : null);
    return known?.unreadMessagesCount ?? 0;
  }

  /**
   * Clears the history of a chat for the reader. The server keeps the moment
   * and no longer gives him what was written up to it, and the unread of it is
   * read: the window, the last message of the row and the count go with it
   * here. The window has nothing older to page into, the server would answer
   * nothing.
   *
   * Takes the options of a window that prints the refusal itself.
   */
  async function clearHistory(
    chatId: ChatId,
    options?: { ownsRefusal?: boolean; ownsRateLimit?: boolean },
  ) {
    const { error: apiError } = await messagingApi.clearHistory(
      chatId,
      options,
    );
    if (apiError) return { error: apiError };
    totalUnreadCount.value = Math.max(
      0,
      totalUnreadCount.value - unreadOf(chatId),
    );
    const cleared = (chat: Chat) => {
      chat.lastMessage = undefined;
      chat.unreadMessagesCount = 0 as Chat["unreadMessagesCount"];
    };
    const row = chats.value?.resources.find((c) => c.id === chatId);
    if (row) cleared(row as Chat);
    if (selectedChat.value?.id === chatId) {
      cleared(selectedChat.value as Chat);
      messagesGuard.next();
      takeWindow([], null);
      highlightedMessageId.value = null;
      messagesError.value = null;
      errorBefore.value = null;
      errorAfter.value = null;
    }
    return { error: null };
  }

  /**
   * Leaves a group chat: it goes out of the reader's list and his count. The
   * page that showed it goes back to the list.
   *
   * Takes the options of a window that prints the refusal itself.
   */
  async function leaveChat(
    chatId: ChatId,
    options?: { ownsRefusal?: boolean; ownsRateLimit?: boolean },
  ) {
    const { error: apiError } = await messagingApi.leaveChat(chatId, options);
    if (apiError) return { error: apiError };
    totalUnreadCount.value = Math.max(
      0,
      totalUnreadCount.value - unreadOf(chatId),
    );
    if (chats.value) {
      const kept = chats.value.resources.filter((c) => c.id !== chatId);
      if (kept.length !== chats.value.resources.length) {
        chats.value = {
          ...chats.value,
          resources: kept,
          paging: chats.value.paging && {
            ...chats.value.paging,
            total: Math.max(0, chats.value.paging.total - 1),
          },
        };
      }
    }
    return { error: null };
  }

  // Send message
  const sending = ref(false);

  /** Returns the error when the send failed, so the caller can restore the text. */
  async function sendMessage(chatId: ChatId, text: string) {
    if (!text.trim()) return { error: null };
    sending.value = true;
    try {
      const { data, error } = await messagingApi.sendMessage(chatId, text);
      if (error) return { error };
      // Out of the envelope. Taken as it came, the sent message went into the
      // correspondence and into the chat list preview as an object with no
      // text, no author and no date: the reply a person had just written
      // showed as an empty line in both places until the page was reloaded.
      const sent = unwrapResource<Message>(data);
      if (sent) {
        messagesList.value.push(sent);
        // Update last message in chat list
        if (chats.value) {
          const chat = chats.value.resources.find((c) => c.id === chatId);
          if (chat) {
            (chat as Chat).lastMessage = sent as typeof chat.lastMessage;
          }
        }
        if (selectedChat.value?.id === chatId) {
          (selectedChat.value as Chat).lastMessage =
            sent as Chat["lastMessage"];
        }
      }
    } finally {
      sending.value = false;
    }
    return { error: null };
  }

  // Update message
  async function updateMessage(id: string, text: string) {
    const { data, error } = await messagingApi.updateMessage(id as MessageId, {
      text,
    });
    // Out of the envelope: the edited message was replaced by the wrapper,
    // so saving an edit blanked the very line that was edited.
    const updated = unwrapResource<Message>(data);
    if (!error && updated) {
      const idx = messagesList.value.findIndex((m) => m.id === id);
      if (idx !== -1) {
        messagesList.value[idx] = updated;
      }
    }
    return { data: updated, error };
  }

  // Delete message - mark as removed instead of filtering
  async function deleteMessage(id: string) {
    const { error } = await messagingApi.deleteMessage(id as MessageId);
    if (!error) {
      const idx = messagesList.value.findIndex((m) => m.id === id);
      if (idx !== -1) {
        messagesList.value[idx] = {
          ...messagesList.value[idx],
          isRemoved: true as unknown as Message["isRemoved"],
        };
      }
    }
    return { error };
  }

  // Like/unlike message
  async function likeMessage(id: string) {
    const { data, error } = await messagingApi.likeMessage(id as MessageId);
    // Out of the envelope: liking a message replaced it with the wrapper, so
    // the like erased the text of the message it was given to.
    const liked = unwrapResource<Message>(data);
    if (!error && liked) {
      const idx = messagesList.value.findIndex((m) => m.id === id);
      if (idx !== -1) {
        messagesList.value[idx] = liked;
      }
    }
    return { data: liked, error };
  }

  async function unlikeMessage(id: string) {
    const { error } = await messagingApi.unlikeMessage(id as MessageId);
    if (!error && currentUser.value) {
      const idx = messagesList.value.findIndex((m) => m.id === id);
      if (idx !== -1) {
        // Remove current user from likes locally since backend returns 204 No Content
        const currentUsername = currentUser.value.username;
        messagesList.value[idx] = {
          ...messagesList.value[idx],
          likes: messagesList.value[idx].likes.filter(
            (u) => u.username !== currentUsername,
          ) as Message["likes"],
        };
      }
    }
    return { error };
  }

  // Check if current user liked a message
  function isLikedByCurrentUser(message: Message): boolean {
    if (!currentUser.value || !message.likes) return false;
    return message.likes.some(
      (u) => u.username === currentUser.value!.username,
    );
  }

  // Clear selected chat
  function clearSelection() {
    selectedChat.value = null;
    chatMissing.value = false;
    chatError.value = null;
    messagesError.value = null;
    messagesList.value = [];
    currentCursor.value = null;
    hasMoreBefore.value = false;
    hasMoreAfter.value = false;
    errorBefore.value = null;
    errorAfter.value = null;
    highlightedMessageId.value = null;
    landedOnRequestedDate.value = false;
    forgetFirstMessage();
  }

  return {
    // Error state
    error,

    // Chats
    chats,
    loadingChats,
    fetchChats,
    fetchUnreadCount,
    totalUnreadCount,

    // Selected chat
    selectedChat,
    loadingChat,
    chatMissing,
    chatError,
    selectChat,
    selectDirectChat,
    interlocutor,
    markAsRead,
    clearHistory,
    leaveChat,
    clearSelection,

    // Messages
    messagesList,
    loadingMessages,
    loadingBefore,
    loadingAfter,
    errorBefore,
    errorAfter,
    messagesError,
    hasMoreBefore,
    hasMoreAfter,
    highlightedMessageId,
    landedOnRequestedDate,
    firstMessageUtc,
    fetchMessages,
    fetchMoreBefore,
    fetchMoreAfter,
    fetchFirstMessage,
    jumpToLatest,
    navigateToMessage,
    navigateToDate,
    clearHighlight,
    sending,
    sendMessage,
    updateMessage,
    deleteMessage,

    // Likes
    likeMessage,
    unlikeMessage,
    isLikedByCurrentUser,
  };
});
