import {
  computed,
  nextTick,
  onMounted,
  onUnmounted,
  ref,
  watch,
  type Ref,
} from "vue";
import { roomApi, useGameDetailsStore } from "@/entities/game";
import { useAuthStore } from "@/entities/user";
import type { CursorPaging, Message } from "@/shared/api/models/common";
import { useViewerChange } from "@/shared/lib/composables/useViewerChange";
import {
  useGuardedRequest,
  type UseGuardedRequestReturn,
} from "@/shared/lib/composables/useGuardedRequest";
import { initBbcodeInteractive } from "@/shared/lib/bbcode/bbcodeInteractive";
import type { ChatRoomAdmission } from "./useChatRoomAdmission";

export const PAGE_SIZE = 50;

interface ChatRoomMessagesOptions {
  gameId: Ref<string>;
  roomNum: Ref<number>;
  chatRoomId: Ref<string | null>;
  container: Ref<HTMLElement | null>;
  admission: ChatRoomAdmission;
  ensureRooms: () => Promise<void>;
}

interface ChatRoomFeed extends ChatRoomMessagesOptions {
  gameStore: ReturnType<typeof useGameDetailsStore>;
  auth: ReturnType<typeof useAuthStore>;
  messages: Ref<Message[]>;
  paging: Ref<CursorPaging | null>;
  loading: Ref<boolean>;
  loadingOlder: Ref<boolean>;
  sending: Ref<boolean>;
  error: Ref<string | null>;
  // The guard drops a stale answer; the failure is the page's own, in apply.
  runLoad: UseGuardedRequestReturn["run"];
}

function scrollToBottom(feed: ChatRoomFeed) {
  nextTick(() => {
    const el = feed.container.value;
    if (el) el.scrollTop = el.scrollHeight;
  });
}

async function markRead(feed: ChatRoomFeed) {
  if (!feed.auth.user || !feed.chatRoomId.value) return;
  await roomApi.markChatRoomRead(feed.chatRoomId.value);
}

function loadInitial(feed: ChatRoomFeed): Promise<void> {
  const { admission, messages, paging, loading, error } = feed;
  loading.value = true;
  error.value = null;
  admission.forgetRefusals();
  messages.value = [];
  paging.value = null;

  return feed.runLoad(
    async () => {
      await feed.ensureRooms();
      // Not asked for a guest of a closed room: its 401 would read as a
      // session that ran out.
      const roomId = feed.chatRoomId.value;
      if (!roomId || !admission.roomOpen.value || !admission.readsRoom.value)
        return { data: null, error: null };
      const answer = await roomApi.getChatMessages(
        roomId,
        undefined,
        PAGE_SIZE,
      );
      return { data: answer, error: null };
    },
    (answer) => {
      loading.value = false;
      if (!answer) {
        // A failed list is said here: "not found" would blame the address.
        const { roomsError } = feed.gameStore;
        if (!admission.roomOpen.value && roomsError) error.value = roomsError;
        return;
      }
      const { data, error: err } = answer;
      if (err) {
        // A refusal is a closed room's rule working, not a failure.
        if (!admission.takeRefusal(err.status))
          error.value = "Не удалось загрузить сообщения";
        return;
      }
      messages.value = data?.resources ?? [];
      paging.value = data?.paging ?? null;
      scrollToBottom(feed);
      nextTick(() => initBbcodeInteractive(feed.container.value));
      markRead(feed);
    },
  );
}

async function loadOlder(feed: ChatRoomFeed) {
  const { messages, paging, loadingOlder, chatRoomId } = feed;
  if (
    loadingOlder.value ||
    !paging.value?.hasPrev ||
    !paging.value?.prevCursor ||
    !chatRoomId.value
  )
    return;
  loadingOlder.value = true;

  const container = feed.container.value;
  const heightBefore = container?.scrollHeight ?? 0;

  const { data, error: err } = await roomApi.getChatMessages(
    chatRoomId.value,
    paging.value.prevCursor,
    PAGE_SIZE,
  );
  if (!err && data && data.resources.length) {
    messages.value = [...data.resources, ...messages.value];
    paging.value = {
      ...paging.value,
      prevCursor: data.paging.prevCursor,
      hasPrev: data.paging.hasPrev,
    };
    nextTick(() => {
      if (container) {
        container.scrollTop = container.scrollHeight - heightBefore;
      }
      initBbcodeInteractive(feed.container.value);
    });
  } else if (!err) {
    paging.value = paging.value ? { ...paging.value, hasPrev: false } : null;
  }
  loadingOlder.value = false;
}

/** True when the server took the message. */
async function send(feed: ChatRoomFeed, text: string): Promise<boolean> {
  const { sending, chatRoomId } = feed;
  if (!text || sending.value || !chatRoomId.value) return false;
  sending.value = true;
  const { error: err } = await roomApi.sendChatMessage(chatRoomId.value, text);
  sending.value = false;
  return !err;
}

function useTopSentinel(feed: ChatRoomFeed, hasMoreBefore: Ref<boolean>) {
  const topSentinel = ref<HTMLElement | null>(null);
  let topObserver: IntersectionObserver | null = null;

  function setupObserver() {
    cleanupObserver();
    if (!feed.container.value || !topSentinel.value) return;
    topObserver = new IntersectionObserver(
      (entries) => {
        if (entries[0].isIntersecting) loadOlder(feed);
      },
      { root: feed.container.value, rootMargin: "100px 0px 0px 0px" },
    );
    topObserver.observe(topSentinel.value);
  }
  function cleanupObserver() {
    topObserver?.disconnect();
    topObserver = null;
  }

  watch([hasMoreBefore, feed.container], () => {
    nextTick(setupObserver);
  });

  onMounted(() => nextTick(setupObserver));
  onUnmounted(cleanupObserver);

  return topSentinel;
}

/** The messages of a chat room of a game, paged by cursor. */
export function useChatRoomMessages(options: ChatRoomMessagesOptions) {
  const feed: ChatRoomFeed = {
    ...options,
    gameStore: useGameDetailsStore(),
    auth: useAuthStore(),
    messages: ref<Message[]>([]),
    paging: ref<CursorPaging | null>(null),
    loading: ref(false),
    loadingOlder: ref(false),
    sending: ref(false),
    error: ref<string | null>(null),
    runLoad: useGuardedRequest({ message: "Не удалось загрузить сообщения" })
      .run,
  };

  const hasMoreBefore = computed(() => feed.paging.value?.hasPrev ?? false);

  // The list of rooms may answer after the first load asked.
  watch(feed.chatRoomId, (id) => {
    if (id && !feed.messages.value.length && !feed.loading.value)
      loadInitial(feed);
  });

  // Signing in or out does not reload the page.
  useViewerChange(() => loadInitial(feed));

  const topSentinel = useTopSentinel(feed, hasMoreBefore);

  watch([feed.gameId, feed.roomNum], () => loadInitial(feed), {
    immediate: true,
  });

  return {
    messages: feed.messages,
    paging: feed.paging,
    loading: feed.loading,
    loadingOlder: feed.loadingOlder,
    sending: feed.sending,
    error: feed.error,
    hasMoreBefore,
    topSentinel,
    loadInitial: () => loadInitial(feed),
    markRead: () => markRead(feed),
    scrollToBottom: () => scrollToBottom(feed),
    send: (text: string) => send(feed, text),
  };
}

export type ChatRoomMessages = ReturnType<typeof useChatRoomMessages>;
