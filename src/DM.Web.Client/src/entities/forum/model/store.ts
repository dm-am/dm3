import { defineStore, storeToRefs } from "pinia";
import { ref, type Ref } from "vue";
import type { Board, BoardId, Topic } from "./types";
import { createRequestGuard } from "@/shared/lib/utils/requestGuard";
import forumApi from "../api/forumApi";
import { useAuthStore } from "@/shared/stores";
import { useApiList } from "@/shared/lib/composables/useApiResource";
import { usePaging } from "@/shared/lib/composables/usePaging";
import { useTopicListing } from "./useTopicListing";
import { useSelectedTopic } from "./useSelectedTopic";
import { useTopicComments } from "./useTopicComments";
import type { RequestOptions } from "@/shared/api/interceptor";

/** The board on screen: a board already read stands in until its own read lands. */
function boardSelection(boards: Ref<Board[] | null>) {
  const selectedBoard = ref<Board | null>(null);

  // Apart from the topic's guard: the shell selects the board while the leaf selects the topic.
  const boardGuard = createRequestGuard();

  async function trySelectBoard(id: BoardId) {
    const requestId = boardGuard.next();
    const localBoard = boards.value?.find((f) => f.id === id);
    if (localBoard) selectedBoard.value = localBoard;

    const { error, data } = await forumApi.getBoard(id);
    // A newer selection owns the state; a stale one reports ok as a no-op.
    if (!boardGuard.isCurrent(requestId)) return true;
    if (error) return false;

    selectedBoard.value = data?.resource ?? null;
    return true;
  }

  /** The status tells a missing board from a failure, which are two error pages. */
  async function trySelectBoardByAlias(
    alias: string,
    refusal?: RequestOptions,
  ): Promise<{ ok: boolean; status?: number }> {
    const requestId = boardGuard.next();
    const localBoard = boards.value?.find((f) => f.alias === alias);
    if (localBoard) selectedBoard.value = localBoard;

    const { error, data } = await forumApi.getBoard(alias as BoardId, refusal);
    if (!boardGuard.isCurrent(requestId)) return { ok: true };
    if (error) return { ok: false, status: error.status };

    selectedBoard.value = data?.resource ?? null;
    return { ok: true };
  }

  return { selectedBoard, trySelectBoard, trySelectBoardByAlias };
}

export const useBoardsStore = defineStore("boards", () => {
  const { user: currentUser } = storeToRefs(useAuthStore());
  const { pageSize } = usePaging();

  const {
    data: boards,
    loading: boardsLoading,
    error: boardsError,
    fetch: fetchBoards,
  } = useApiList<Board>(() => forumApi.getBoards(), { cacheMs: 300_000 });

  const {
    data: news,
    error: newsError,
    fetch: fetchNews,
  } = useApiList<Topic>(() => forumApi.getNews(), { cacheMs: 300_000 });

  const { selectedBoard, trySelectBoard, trySelectBoardByAlias } =
    boardSelection(boards);

  const listing = useTopicListing({ selectedBoard, pageSize });
  const topic = useSelectedTopic({
    selectedBoard,
    currentUser,
    trySelectBoard,
    invalidateTopics: listing.invalidateTopics,
  });
  const discussion = useTopicComments({
    selectedTopic: topic.selectedTopic,
    currentUser,
    pageSize,
  });

  return {
    boards,
    boardsLoading,
    boardsError,
    fetchBoards,
    selectedBoard,
    trySelectBoard,
    trySelectBoardByAlias,
    news,
    newsError,
    fetchNews,
    ...listing,
    ...topic,
    ...discussion,
  };
});
