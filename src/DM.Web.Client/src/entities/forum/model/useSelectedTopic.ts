import { ref, type Ref } from "vue";
import type { Board, BoardId, Topic, TopicId } from "./types";
import type { User } from "@/shared/api/models/common";
import { asServed } from "@/shared/api/models";
import { unwrapResource } from "@/shared/api";
import { createRequestGuard } from "@/shared/lib/utils/requestGuard";
import forumApi from "../api/forumApi";
import type { RequestOptions } from "@/shared/api/interceptor";

export interface SelectedTopicOptions {
  /** The board on screen, taken from the topic a number resolves to. */
  selectedBoard: Ref<Board | null>;
  /** The reader, whose like is the one taken back. */
  currentUser: Ref<User | null>;
  /** Selects the board of a topic opened by its id. */
  trySelectBoard: (id: BoardId) => Promise<boolean>;
  /** Forgets the cached board listings, which show what an edit changed. */
  invalidateTopics: () => void;
}

/** The open topic, on a guard of its own beside the board selection's. */
export function useSelectedTopic({
  selectedBoard,
  currentUser,
  trySelectBoard,
  invalidateTopics,
}: SelectedTopicOptions) {
  const topicGuard = createRequestGuard();

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

  /** The status picks the error page without a second request. */
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
    // A stale reply reports ok, so its equally stale caller raises no error page.
    if (!topicGuard.isCurrent(requestId)) return { ok: true };
    const topic = data?.resource;
    if (!topic) return { ok: false, status: error?.status };

    selectedTopic.value = topic;
    selectedBoard.value = topic.board;
    return { ok: true };
  }

  return {
    selectedTopic,
    trySelectTopic,
    trySelectTopicByNumber,
    ...topicLikes(selectedTopic, currentUser),
    ...topicChanges(selectedTopic, invalidateTopics),
  };
}

function topicLikes(
  selectedTopic: Ref<Topic | null>,
  currentUser: Ref<User | null>,
) {
  async function likeTopic(id: string) {
    const { data, error } = await forumApi.postTopicLike(id as TopicId);
    const liker = unwrapResource<User>(data);
    if (liker && selectedTopic.value && selectedTopic.value.id === id) {
      const existingLikes = selectedTopic.value.likes || [];
      selectedTopic.value = {
        ...selectedTopic.value,
        likes: asServed([...existingLikes, liker]),
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
      selectedTopic.value = {
        ...selectedTopic.value,
        likes: asServed(
          selectedTopic.value.likes.filter(
            (u) => u.username !== currentUser.value?.username,
          ),
        ),
      };
    }
    return { error };
  }

  return { likeTopic, unlikeTopic };
}

function topicChanges(
  selectedTopic: Ref<Topic | null>,
  invalidateTopics: () => void,
) {
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
    // The listing shows the title, the lock and the pin, and caches them.
    invalidateTopics();
    return { data: data?.resource };
  }

  async function deleteTopic(id: string, options?: RequestOptions) {
    const { error } = await forumApi.deleteTopic(id as TopicId, options);
    if (error) return { error };
    if (selectedTopic.value?.id === id) selectedTopic.value = null;
    invalidateTopics();
    return { data: true };
  }

  return { updateTopicContent, deleteTopic };
}
