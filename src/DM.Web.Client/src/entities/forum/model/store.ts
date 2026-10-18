import { defineStore, storeToRefs } from "pinia";
import { ref } from "vue";
import type {
  Comment,
  CommentId,
  Board,
  BoardId,
  Topic,
  TopicId,
  TopicsQuery,
  CommentsQuery,
} from "./types";
import type {
  GeneralError,
  ListEnvelope,
  User,
} from "@/shared/api/models/common";
import { markRemoved } from "@/shared/api/models/common";
import { unwrapResource } from "@/shared/api";
import { createRequestGuard } from "@/shared/lib/utils/requestGuard";
import {
  describeFailure,
  describeRateLimit,
  requestNotSent,
} from "@/shared/lib/errors";
import forumApi from "../api/forumApi";
import { useAuthStore } from "@/shared/stores";
import { useApiList } from "@/shared/lib/composables/useApiResource";
import { usePaging } from "@/shared/lib/composables/usePaging";
import {
  createKeyedCache,
  stableCacheKey,
} from "@/shared/lib/utils/keyedCache";

/**
 * What the strip over the board listing says when the rows never arrived. It
 * is also the sentence for a request that got no answer at all: there is no
 * problem document to read in that case, and none for a throw either.
 */
const TOPICS_LOAD_FAILURE = "Не удалось загрузить топики";

/**
 * The listing's own wording for a failed read.
 *
 * Whatever the API named is relayed as the API worded it. A wait is the one
 * exception: the title that comes with a 429 names the refusal without the
 * number, and the number is the part a reader acts on, so the shared sentence
 * carries it instead. That sentence is also the one the interceptor would have
 * raised, so taking the refusal over costs the reader nothing.
 */
function listingFailure(error: GeneralError): string {
  return error.status === 429
    ? describeRateLimit(error.retryAfterSeconds)
    : describeFailure(error, TOPICS_LOAD_FAILURE);
}

/**
 * The reads that fill the board listing take their own refusals over.
 *
 * TopicsList stands a strip where the rows would be, so the failure is already
 * on screen and in the place the reader is looking; the interceptor's plate put
 * the same refusal over it on a 403, a 429 and a 5xx, and one failed load read
 * as two. With these flags the strip answers alone.
 *
 * An expired session and a dead connection are deliberately not in here. The
 * interceptor speaks for both, because neither is about this board — one is
 * about the reader's session, the other about the road to the site, and only
 * the interceptor can offer the other address.
 *
 * Only searchTopics passes them. The other reads of the same endpoint read the
 * listing again after a pin or a reorder and show nothing at all about a
 * failure, since the rows already on screen stay there: the interceptor is the
 * only voice they have, and this flag would take it away.
 */
const LISTING_OWNS_REFUSAL = {
  ownsRefusal: true,
  ownsRateLimit: true,
} as const;

/** The refusal flags a read of the board listing hands the API client. */
type ListingRefusalFlags = Parameters<typeof forumApi.getTopics>[2];

/**
 * The read that opens a board's page takes its own refusals over.
 *
 * The page (ForumPage) stands the error page of the status in place of the
 * board when the read fails, a 429 and a 5xx included, so the refusal is
 * already on screen; the interceptor's plate put it there a second time, and a
 * guest refused with a 429 read "Упс! Слишком много запросов!" with "Слишком
 * много попыток" in the corner over it. With these flags the page answers
 * alone.
 *
 * The page hands them to trySelectBoardByAlias. The other reads of a board
 * leave them out: the board read again after a sign-in (TopicsList) and the
 * board of a topic (trySelectTopic) show nothing at all about a failure, and
 * the interceptor is their only voice.
 */
export const BOARD_PAGE_OWNS_REFUSAL = {
  ownsRefusal: true,
  ownsRateLimit: true,
} as const;

/** The refusal flags a read of a board hands the API client. */
type BoardRefusalFlags = Parameters<typeof forumApi.getBoard>[1];

export const useBoardsStore = defineStore("boards", () => {
  const { user: currentUser } = storeToRefs(useAuthStore());
  const { topicsPerPage, commentsPerPage } = usePaging();

  // Boards are static - 5 minute cache
  const boardsResource = useApiList<Board>(() => forumApi.getBoards(), {
    cacheMs: 300_000,
  });
  const boards = boardsResource.data;
  const boardsLoading = boardsResource.loading;
  const boardsError = boardsResource.error;
  const fetchBoards = boardsResource.fetch;

  // News rarely changes - 5 minute cache
  const newsResource = useApiList<Topic>(() => forumApi.getNews(), {
    cacheMs: 300_000,
  });
  const news = newsResource.data;
  const newsError = newsResource.error;
  const fetchNews = newsResource.fetch;

  const selectedBoard = ref<Board | null>(null);

  // The counter every "select board/topic" flow runs on. Each new selection
  // takes an id; continuations resumed after `await` ask whether theirs is
  // still the current one and bail if it is not. Without this, a slow
  // getBoard/getTopic response from board A lands AFTER the user already
  // navigated to board B and silently overwrites selectedBoard (the classic
  // "clicked Для новичков, still see Общий" bug).
  //
  // Board and topic selection use SEPARATE guards: on a direct navigation to
  // a topic URL, the persistent ForumPage shell selects the board while the
  // TopicPage leaf selects the topic — concurrently. A shared counter made
  // whichever finished second invalidate the other, so the topic silently
  // failed to load until the user re-entered from the board (the bug where
  // /forum/alias/num opened blank). Independent guards let both complete.
  //
  // Three instances of the shared primitive rather than three hand-written
  // counters: the counter was spelled out here, in useApiResource and in four
  // entity stores, and the spelling is where a `!==` becomes a `===`.
  const boardGuard = createRequestGuard();
  const topicGuard = createRequestGuard();

  async function trySelectBoard(id: BoardId) {
    const requestId = boardGuard.next();
    const localBoard = boards.value?.find((f) => f.id === id);
    if (localBoard) selectedBoard.value = localBoard;

    const { error, data } = await forumApi.getBoard(id);
    // Stale continuation: commit nothing, report success as a no-op (same
    // contract as trySelectBoardByAlias / trySelectTopicByNumber below).
    if (!boardGuard.isCurrent(requestId)) return true;
    if (error) return false;

    selectedBoard.value = data?.resource ?? null;
    return true;
  }

  /**
   * Resolve a board by its URL alias.
   *
   * Returns `{ ok, status }` instead of a bare boolean so callers can tell a
   * missing board (404) apart from a transient failure (network/500) and map
   * each to the right error page instead of flattening everything to 404.
   * The returned object is still truthy, so old call sites doing
   * `if (await trySelectBoardByAlias(...))` keep compiling (though they lose
   * the extra status info) — new call sites should destructure `{ ok }`.
   *
   * @param refusal BOARD_PAGE_OWNS_REFUSAL from a caller that shows the
   * failure itself, the page that opens the board; left out, the read goes
   * with no flags at all and the interceptor speaks for it.
   */
  async function trySelectBoardByAlias(
    alias: string,
    refusal?: BoardRefusalFlags,
  ): Promise<{ ok: boolean; status?: number }> {
    const requestId = boardGuard.next();
    const localBoard = boards.value?.find((f) => f.alias === alias);
    if (localBoard) selectedBoard.value = localBoard;

    const { error, data } = await forumApi.getBoard(alias as BoardId, refusal);
    // Stale continuation: a newer selection started while this response was
    // in flight. Commit nothing and report ok so the (equally stale) caller
    // treats it as a no-op instead of raising an error page over the newer
    // navigation's state.
    if (!boardGuard.isCurrent(requestId)) return { ok: true };
    if (error) return { ok: false, status: error.status };

    selectedBoard.value = data?.resource ?? null;
    return { ok: true };
  }

  const attachedTopics = ref<Topic[] | null>(null);
  const topics = ref<ListEnvelope<Topic> | null>(null);
  const topicsLoading = ref(false);
  // The sentence the strip prints, not a bare "something went wrong": the
  // strip is the one carrier of this failure now, and a flag has no seconds to
  // hand a reader waiting out a rate limit. `null` is "no failure" — the one
  // reader of this, TopicsList, tests it for truth exactly as it tested the
  // flag.
  const topicsError = ref<string | null>(null);

  // The entry holds both lists: the attached topics belong to the same board
  // page and expire with it.
  const topicsCache = createKeyedCache<{
    data: ListEnvelope<Topic>;
    attached: Topic[] | null;
  }>({ ttlMs: 30_000 });

  // Only the latest searchTopics call may commit results/error/loading state.
  // A slower response for a board the user already left must never overwrite
  // the newer board's rows.
  const topicsGuard = createRequestGuard();

  /**
   * The query of the listing on screen, as TopicsList last asked for it. A pin
   * reads that listing again, its filters and its page included: read without
   * them, the refresh put every topic of the board under a search that still
   * stood in its field.
   */
  let listingQuery: TopicsQuery | null = null;

  /**
   * Search topics with filters. Single source of truth for loading topics.
   * Called by TopicsList via paramsKey watcher.
   */
  async function searchTopics(query: TopicsQuery) {
    listingQuery = query;
    await readListing(query, LISTING_OWNS_REFUSAL);
  }

  /**
   * Reads the listing on screen again, past the cache, after a change of the
   * pinned topics. The cache is dropped for every listing: a fresh entry is
   * handed out without a request for half a minute, so a board left and opened
   * again showed the pins as they were before the change.
   */
  async function rereadListing() {
    invalidateTopics();
    await readListing(listingQuery ?? {});
  }

  /**
   * One read of the board listing, committed only while it is the latest.
   *
   * @param flags The refusal flags of a read whose failure the listing prints
   * itself. Without them the interceptor speaks for a failure, and the
   * listing keeps what it shows and says nothing.
   */
  async function readListing(query: TopicsQuery, flags?: ListingRefusalFlags) {
    if (!selectedBoard.value) return;
    const requestId = topicsGuard.next();

    // Apply user's page size preference
    const size = query.size ?? topicsPerPage.value;
    const fullQuery = { ...query, size };
    const boardAlias = selectedBoard.value.alias as BoardId;
    const cacheKey = stableCacheKey({ board: boardAlias, ...fullQuery });

    // Return cached if fresh
    const fresh = topicsCache.get(cacheKey);
    if (fresh) {
      topics.value = fresh.data;
      attachedTopics.value = fresh.attached;
      return;
    }

    // Show stale while revalidating
    const stale = topicsCache.getStale(cacheKey);
    if (stale) {
      topics.value = stale.data;
      attachedTopics.value = stale.attached;
    }

    topicsLoading.value = true;
    topicsError.value = null;
    try {
      const effectiveSortBy = query.sortBy ?? "lastActivity";
      const effectiveSortOrder = query.sortOrder ?? "desc";
      const hasFilters =
        !!query.search ||
        (query.authors && query.authors.length > 0) ||
        !!query.createdFromUtc ||
        !!query.createdToUtc ||
        effectiveSortBy !== "lastActivity" ||
        effectiveSortOrder !== "desc";

      let fetchedAttached: Topic[] | null = null;
      let fetchedTopics: ListEnvelope<Topic> | null = null;
      // The problem document itself, not a flag: the strip prints what the
      // server said, so the refusal has to survive down to the commit.
      let failure: GeneralError | null = null;

      if (hasFilters) {
        // With filters: single unified query (attached mixed with regular)
        const { data, error } = await forumApi.getTopics(
          boardAlias,
          fullQuery,
          flags,
        );
        failure = error;
        fetchedAttached = null;
        fetchedTopics = data ?? null;
      } else {
        // No filters: fetch attached separately, show at top
        const [attachedResult, regularResult] = await Promise.all([
          forumApi.getTopics(boardAlias, { isAttached: true }, flags),
          forumApi.getTopics(
            boardAlias,
            { ...fullQuery, isAttached: false },
            flags,
          ),
        ]);
        // The regular read fills the rows, so its refusal is the one the strip
        // is about; the pinned read answers only when it failed alone.
        failure = regularResult.error ?? attachedResult.error;
        fetchedAttached = attachedResult.data?.resources ?? null;
        fetchedTopics = regularResult.data ?? null;
      }

      // Fresh data is still valid for ITS cache key, so cache it even when
      // the visible commit below is skipped as stale.
      if (!failure && fetchedTopics) {
        topicsCache.set(cacheKey, {
          data: fetchedTopics,
          attached: fetchedAttached,
        });
      }

      // Stale continuation: a newer read started while this one was in
      // flight — the newer call owns the visible state.
      if (!topicsGuard.isCurrent(requestId)) return;

      // On failure surface the error and keep any already-shown rows
      // (stale-while-revalidate) instead of blanking the list — never
      // present a failed load as fake-empty. A read without the flags had its
      // failure said by the interceptor, over rows that are still there.
      if (failure) {
        if (flags) topicsError.value = listingFailure(failure);
        return;
      }

      attachedTopics.value = fetchedAttached;
      topics.value = fetchedTopics;
    } finally {
      // Only the latest request may clear the flag: a stale finally must
      // not hide the spinner while the newer request is still loading.
      if (topicsGuard.isCurrent(requestId)) topicsLoading.value = false;
    }
  }

  /**
   * Reorder pinned topics (moderator action)
   */
  async function reorderPinnedTopics(topicIds: string[]) {
    // A problem document, not a JS Error: the caller reads this out with
    // notifyFailure exactly as it reads a refusal that came from the API.
    if (!selectedBoard.value) return { error: requestNotSent };

    const { error } = await forumApi.reorderPinnedTopics(
      selectedBoard.value.alias as BoardId,
      topicIds,
    );
    if (error) return { error };

    // The cached listings hold the block in its old order, and a board opened
    // again within half a minute would stand it so.
    invalidateTopics();

    // Refresh attached topics to reflect new order. A failed refresh keeps the
    // rows already held, stood in the order the server now keeps — the same
    // rule searchTopics states above: the reorder itself landed, and the block
    // in its old order would say the opposite.
    const { data } = await forumApi.getTopics(
      selectedBoard.value.alias as BoardId,
      { isAttached: true },
    );
    if (data) {
      attachedTopics.value = data.resources;
    } else if (attachedTopics.value) {
      const held = new Map(
        attachedTopics.value.map((topic) => [String(topic.id), topic]),
      );
      attachedTopics.value = topicIds
        .map((id) => held.get(id))
        .filter((topic): topic is Topic => topic !== undefined);
    }

    return { data: true };
  }

  /**
   * Toggle topic pin status (moderator action)
   */
  async function togglePinTopic(topicId: string) {
    if (!selectedBoard.value) return { error: requestNotSent };

    // Find topic to get current pin status
    const topic = [
      ...(attachedTopics.value ?? []),
      ...(topics.value?.resources ?? []),
    ].find((t) => t.id === topicId);

    if (!topic) return { error: requestNotSent };

    const newStatus = !topic.isAttached;
    const { error } = await forumApi.updateTopic(topicId as TopicId, {
      isAttached: newStatus,
    });
    if (error) return { error };

    // The listing on screen, read again with its filters and its page. A
    // failed read keeps what is on screen instead of emptying the board: the
    // pin itself landed, and "Топиков пока нет" would be a lie about it.
    await rereadListing();

    return { data: newStatus };
  }

  const selectedTopic = ref<Topic | null>(null);
  async function trySelectTopic(id: TopicId) {
    const requestId = topicGuard.next();
    if (selectedTopic.value?.id !== id) selectedTopic.value = null;
    const { data } = await forumApi.getTopic(id);
    if (!topicGuard.isCurrent(requestId)) return; // stale: newer selection won
    const topic = data?.resource;
    if (!topic) return;

    selectedTopic.value = topic;
    await trySelectBoard(topic.board.id);
  }

  /**
   * Resolve a topic by its board alias + per-board topic number.
   *
   * Returns `{ ok, status }` (same contract as trySelectBoardByAlias) so the
   * caller can map 403/404/410/500 to the right ErrorPage without firing a
   * second request just to read the status code. The returned object is
   * still truthy under `if (...)`, so existing boolean-truthiness call sites
   * keep working unchanged.
   */
  async function trySelectTopicByNumber(
    boardAlias: string,
    topicNumber: number,
  ): Promise<{ ok: boolean; status?: number }> {
    const requestId = topicGuard.next();
    selectedTopic.value = null;
    const { data, error } = await forumApi.getTopicByNumber(
      boardAlias,
      topicNumber,
    );
    // Stale continuation (user already navigated elsewhere): keep hands off
    // selectedTopic/selectedBoard and report ok so the (equally stale)
    // caller treats it as a no-op instead of raising an error page.
    if (!topicGuard.isCurrent(requestId)) return { ok: true };
    const topic = data?.resource;
    if (!topic) return { ok: false, status: error?.status };

    selectedTopic.value = topic;
    selectedBoard.value = topic.board;
    return { ok: true };
  }

  const comments = ref<ListEnvelope<Comment> | null>(null);
  const commentsLoading = ref(false);
  const commentsError = ref(false);

  const commentsCache = createKeyedCache<ListEnvelope<Comment>>({
    ttlMs: 30_000,
  });

  /**
   * Search comments with filters. Single source of truth for loading comments.
   * Called by CommentsList via watcher.
   */
  async function searchComments(query: CommentsQuery) {
    if (!selectedTopic.value) return;

    const size = query.size ?? commentsPerPage.value;
    const fullQuery: CommentsQuery = { ...query, size };
    const topicId = selectedTopic.value.id!;
    const cacheKey = stableCacheKey({ topic: topicId, ...fullQuery });

    // Return cached if fresh
    const fresh = commentsCache.get(cacheKey);
    if (fresh) {
      comments.value = fresh;
      return;
    }

    // Show stale while revalidating. The stale entry is keyed by the query, so a
    // change of page, filter or sort has none — and assigning null in that case
    // cleared the list the reader was looking at, which is the very thing this
    // block exists to avoid. What is on screen stays until the answer arrives, the
    // way the topic list above does it.
    const stale = commentsCache.getStale(cacheKey);
    if (stale) {
      comments.value = stale;
    }

    commentsLoading.value = true;
    commentsError.value = false;
    try {
      const { data, error } = await forumApi.getComments(topicId, fullQuery);

      // On failure surface the error and keep any stale rows already shown
      // instead of blanking to a fake-empty list.
      if (error) {
        commentsError.value = true;
        return;
      }

      comments.value = data ?? null;

      // Update cache
      if (data) {
        commentsCache.set(cacheKey, data);
      }
    } finally {
      commentsLoading.value = false;
    }
  }

  async function createComment(text: string) {
    if (!selectedTopic.value) return { error: requestNotSent };

    const { data, error } = await forumApi.createComment(
      selectedTopic.value.id!,
      { text },
    );
    if (error) return { error };

    // Add the new comment to the list. Out of the envelope: pushed as it
    // came, the answer put an object with no text and no author into the list,
    // and the comment showed as an empty block until the page was reloaded.
    const created = unwrapResource<Comment>(data);
    if (created && comments.value) {
      comments.value.resources.push(created);
      // Update paging info
      if (comments.value.paging) {
        comments.value.paging.total = (comments.value.paging.total || 0) + 1;
      }
    }
    // Update topic's comment count
    if (selectedTopic.value) {
      (selectedTopic.value as any).commentsCount =
        (selectedTopic.value.commentsCount || 0) + 1;
    }

    return { data };
  }

  /**
   * Edit and delete patch the list from the server's answer, never from the
   * request having been sent: `Api` resolves on a refusal too, so an unread
   * `error` is a comment struck through on screen and untouched on the
   * server. The error is handed back so the page that asked can name it.
   */
  async function updateComment(id: string, text: string) {
    const { data, error } = await forumApi.updateComment(id as CommentId, {
      text,
    });
    if (!error) {
      const updated = unwrapResource<Comment>(data);
      if (updated && comments.value) {
        const index = comments.value.resources.findIndex((c) => c.id === id);
        if (index !== -1) {
          comments.value.resources[index] = updated;
        }
      }
    }
    return { error };
  }

  /** `options` rides to the request: the confirmation that asks prints a refusal. */
  async function deleteComment(
    id: string,
    options?: Parameters<typeof forumApi.deleteComment>[1],
  ) {
    const { error } = await forumApi.deleteComment(id as CommentId, options);
    if (!error && comments.value) {
      const index = comments.value.resources.findIndex((c) => c.id === id);
      if (index !== -1) {
        comments.value.resources[index] = markRemoved(
          comments.value.resources[index],
        );
      }
    }
    return { error };
  }

  /**
   * Like a comment. The error is handed back, as updateComment hands it: a
   * refused like leaves the heart as it was, and only the item that was
   * pressed can say so.
   */
  async function likeComment(id: string) {
    const { data, error } = await forumApi.postCommentLike(id as CommentId);
    const liker = unwrapResource<User>(data);
    if (liker && comments.value && currentUser.value) {
      const index = comments.value.resources.findIndex((c) => c.id === id);
      if (index !== -1) {
        const comment = comments.value.resources[index];
        const existingLikes =
          comment.likes || ([] as unknown as Comment["likes"]);
        comments.value.resources[index] = {
          ...comment,
          likes: [...existingLikes, liker] as Comment["likes"],
        };
      }
    }
    return { error };
  }

  /** Take the reader's like back; the error is handed back as for a like. */
  async function unlikeComment(id: string) {
    const { error } = await forumApi.deleteCommentLike(id as CommentId);
    if (!error && comments.value && currentUser.value) {
      const index = comments.value.resources.findIndex((c) => c.id === id);
      if (index !== -1) {
        const comment = comments.value.resources[index];
        if (comment.likes) {
          comments.value.resources[index] = {
            ...comment,
            likes: comment.likes.filter(
              (u) => u.username !== currentUser.value?.username,
            ) as Comment["likes"],
          };
        }
      }
    }
    return { error };
  }

  async function likeTopic(id: string) {
    const { data, error } = await forumApi.postTopicLike(id as TopicId);
    // Out of the envelope. The cast below hid the shape from the compiler, so
    // the wrapper went into the list of likes and the tooltip beside the
    // counter read "undefined оценил(а) это" while the number itself moved.
    const liker = unwrapResource<User>(data);
    if (liker && selectedTopic.value && selectedTopic.value.id === id) {
      const existingLikes =
        selectedTopic.value.likes || ([] as unknown as Topic["likes"]);
      selectedTopic.value = {
        ...selectedTopic.value,
        likes: [...existingLikes, liker] as Topic["likes"],
      };
    }
    return { error };
  }

  async function unlikeTopic(id: string) {
    const { error } = await forumApi.deleteTopicLike(id as TopicId);
    if (
      !error &&
      selectedTopic.value &&
      selectedTopic.value.id === id &&
      currentUser.value
    ) {
      if (selectedTopic.value.likes) {
        selectedTopic.value = {
          ...selectedTopic.value,
          likes: selectedTopic.value.likes.filter(
            (u) => u.username !== currentUser.value?.username,
          ) as Topic["likes"],
        };
      }
    }
    return { error };
  }

  /**
   * Edit a topic's title and/or first-post text (author while open, or
   * moderator), and close or reopen it, pin or unpin it (board moderator,
   * administrator) in the same request. Commits the server-rendered result to
   * selectedTopic.
   */
  async function updateTopicContent(
    id: string,
    patch: {
      title?: string;
      description?: string;
      isClosed?: boolean;
      isAttached?: boolean;
    },
  ) {
    const { data, error } = await forumApi.updateTopic(id as TopicId, patch);
    if (error) return { error };
    if (data?.resource && selectedTopic.value?.id === id) {
      selectedTopic.value = data.resource;
    }
    // The title, the lock and the pin all show in the board listing, and its
    // cache would hand the old ones back for half a minute.
    invalidateTopics();
    return { data: data?.resource };
  }

  /**
   * Forgets every cached board listing. A hit inside the TTL is returned
   * without revalidation, so after a topic is created or removed the board
   * would otherwise show the old list for up to half a minute.
   */
  function invalidateTopics(): void {
    topicsCache.clear();
  }

  /**
   * Create a topic on a board. Lives here rather than in the page because the
   * listing cache lives here: called through the API client directly, the new
   * topic was missing from the board it was just posted to.
   */
  async function createTopic(
    boardAlias: string,
    topic: { title: string; text: string },
  ) {
    const result = await forumApi.createTopic(boardAlias as BoardId, topic);
    if (!result.error) invalidateTopics();
    return result;
  }

  /**
   * Delete a topic (author or moderator); clears it from selection.
   *
   * @param options The refusal flags of a window that shows the failure itself.
   */
  async function deleteTopic(
    id: string,
    options?: Parameters<typeof forumApi.deleteTopic>[1],
  ) {
    const { error } = await forumApi.deleteTopic(id as TopicId, options);
    if (error) return { error };
    if (selectedTopic.value?.id === id) selectedTopic.value = null;
    invalidateTopics();
    return { data: true };
  }

  return {
    boards,
    boardsLoading,
    boardsError,
    fetchBoards,
    selectedBoard,
    trySelectBoard,
    trySelectBoardByAlias,
    attachedTopics,
    topics,
    topicsLoading,
    topicsError,
    searchTopics,
    reorderPinnedTopics,
    togglePinTopic,
    news,
    newsError,
    fetchNews,
    trySelectTopic,
    trySelectTopicByNumber,
    selectedTopic,
    searchComments,
    comments,
    commentsLoading,
    commentsError,
    createComment,
    updateComment,
    deleteComment,
    likeComment,
    unlikeComment,
    likeTopic,
    unlikeTopic,
    updateTopicContent,
    createTopic,
    deleteTopic,
    invalidateTopics,
  };
});
