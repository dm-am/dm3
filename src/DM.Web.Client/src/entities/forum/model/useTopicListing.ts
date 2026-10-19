import { ref, type Ref } from "vue";
import type { Board, BoardId, Topic, TopicId, TopicsQuery } from "./types";
import type { GeneralError, ListEnvelope } from "@/shared/api/models/common";
import { createRequestGuard } from "@/shared/lib/utils/requestGuard";
import { describeFailureOrWait, requestNotSent } from "@/shared/lib/errors";
import forumApi from "../api/forumApi";
import {
  createKeyedCache,
  stableCacheKey,
} from "@/shared/lib/utils/keyedCache";
import { type RequestOptions, OWNS_REFUSAL } from "@/shared/api/interceptor";

// For a failure that carries no problem document to read.
const TOPICS_LOAD_FAILURE = "Не удалось загрузить топики";

export interface TopicListingOptions {
  /** The board on screen: its listing is the one read, and none without it. */
  selectedBoard: Ref<Board | null>;
  /** The reader's page size, for a query that names none of its own. */
  pageSize: Readonly<Ref<number>>;
}

function topicListingState() {
  return {
    attachedTopics: ref<Topic[] | null>(null),
    topics: ref<ListEnvelope<Topic> | null>(null),
    topicsLoading: ref(false),
    // The sentence itself: a flag has no seconds to hand a reader waiting out a limit.
    topicsError: ref<string | null>(null),

    // The pinned topics belong to the same board page and expire with it.
    topicsCache: createKeyedCache<{
      data: ListEnvelope<Topic>;
      attached: Topic[] | null;
    }>({ ttlMs: 30_000 }),

    topicsGuard: createRequestGuard(),
  };
}

type Listing = ReturnType<typeof topicListingState> & TopicListingOptions;

export function useTopicListing({
  selectedBoard,
  pageSize,
}: TopicListingOptions) {
  const state = topicListingState();
  const listing: Listing = { ...state, selectedBoard, pageSize };
  const { attachedTopics, topics, topicsLoading, topicsError } = state;

  // A pin rereads the listing with its filters and page, not the whole board.
  let listingQuery: TopicsQuery | null = null;

  async function searchTopics(query: TopicsQuery) {
    listingQuery = query;
    // Only here: the strip says the refusal where the rows would be.
    await readListing(listing, query, OWNS_REFUSAL);
  }

  // Drops the cache of every listing: a board opened again would show the old pins.
  async function rereadListing() {
    invalidateTopics();
    await readListing(listing, listingQuery ?? {});
  }

  function invalidateTopics(): void {
    state.topicsCache.clear();
  }

  // Here, beside the listing cache it has to drop.
  async function createTopic(
    boardAlias: string,
    topic: { title: string; text: string },
  ) {
    const result = await forumApi.createTopic(boardAlias as BoardId, topic);
    if (!result.error) invalidateTopics();
    return result;
  }

  const pins = topicPinChanges(listing, invalidateTopics, rereadListing);

  return {
    attachedTopics,
    topics,
    topicsLoading,
    topicsError,
    searchTopics,
    ...pins,
    createTopic,
    invalidateTopics,
  };
}

/** One read of the listing; without flags a failure is the interceptor's to say. */
async function readListing(
  {
    selectedBoard,
    pageSize,
    attachedTopics,
    topics,
    topicsLoading,
    topicsError,
    topicsCache,
    topicsGuard,
  }: Listing,
  query: TopicsQuery,
  flags?: RequestOptions,
) {
  if (!selectedBoard.value) return;
  const requestId = topicsGuard.next();

  const size = query.size ?? pageSize.value;
  const fullQuery = { ...query, size };
  const boardAlias = selectedBoard.value.alias as BoardId;
  const cacheKey = stableCacheKey({ board: boardAlias, ...fullQuery });

  const fresh = topicsCache.get(cacheKey);
  if (fresh) {
    topics.value = fresh.data;
    attachedTopics.value = fresh.attached;
    return;
  }

  const stale = topicsCache.getStale(cacheKey);
  if (stale) {
    topics.value = stale.data;
    attachedTopics.value = stale.attached;
  }

  topicsLoading.value = true;
  topicsError.value = null;
  try {
    const { fetchedAttached, fetchedTopics, failure } = await fetchListing(
      boardAlias,
      query,
      fullQuery,
      flags,
    );

    // Still valid for its own key when the commit below is skipped as stale.
    if (!failure && fetchedTopics) {
      topicsCache.set(cacheKey, {
        data: fetchedTopics,
        attached: fetchedAttached,
      });
    }

    if (!topicsGuard.isCurrent(requestId)) return;

    // The rows already shown stay rather than a fake empty list.
    if (failure) {
      if (flags)
        topicsError.value = describeFailureOrWait(failure, TOPICS_LOAD_FAILURE);
      return;
    }

    attachedTopics.value = fetchedAttached;
    topics.value = fetchedTopics;
  } finally {
    if (topicsGuard.isCurrent(requestId)) topicsLoading.value = false;
  }
}

interface FetchedListing {
  fetchedAttached: Topic[] | null;
  fetchedTopics: ListEnvelope<Topic> | null;
  failure: GeneralError | null;
}

/** With filters the pinned topics come mixed in, without them as a block apart. */
async function fetchListing(
  boardAlias: BoardId,
  query: TopicsQuery,
  fullQuery: TopicsQuery,
  flags?: RequestOptions,
): Promise<FetchedListing> {
  const effectiveSortBy = query.sortBy ?? "lastActivity";
  const effectiveSortOrder = query.sortOrder ?? "desc";
  const hasFilters =
    !!query.search ||
    (query.authors && query.authors.length > 0) ||
    !!query.createdFromUtc ||
    !!query.createdToUtc ||
    effectiveSortBy !== "lastActivity" ||
    effectiveSortOrder !== "desc";

  let fetchedAttached: Topic[] | null;
  let fetchedTopics: ListEnvelope<Topic> | null;
  let failure: GeneralError | null;

  if (hasFilters) {
    const { data, error } = await forumApi.getTopics(
      boardAlias,
      fullQuery,
      flags,
    );
    failure = error;
    fetchedAttached = null;
    fetchedTopics = data ?? null;
  } else {
    const [attachedResult, regularResult] = await Promise.all([
      forumApi.getTopics(boardAlias, { isAttached: true }, flags),
      forumApi.getTopics(
        boardAlias,
        { ...fullQuery, isAttached: false },
        flags,
      ),
    ]);
    // The strip is about the rows, so the pinned read answers only when it failed alone.
    failure = regularResult.error ?? attachedResult.error;
    fetchedAttached = attachedResult.data?.resources ?? null;
    fetchedTopics = regularResult.data ?? null;
  }

  return { fetchedAttached, fetchedTopics, failure };
}

function topicPinChanges(
  { selectedBoard, attachedTopics, topics }: Listing,
  invalidateTopics: () => void,
  rereadListing: () => Promise<void>,
) {
  async function reorderPinnedTopics(topicIds: string[]) {
    if (!selectedBoard.value) return { error: requestNotSent };

    const { error } = await forumApi.reorderPinnedTopics(
      selectedBoard.value.alias as BoardId,
      topicIds,
    );
    if (error) return { error };

    invalidateTopics();

    // The reorder landed, so a failed refresh stands the rows held in the new order.
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

  async function togglePinTopic(topicId: string) {
    if (!selectedBoard.value) return { error: requestNotSent };

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

    await rereadListing();

    return { data: newStatus };
  }

  return { reorderPinnedTopics, togglePinTopic };
}
