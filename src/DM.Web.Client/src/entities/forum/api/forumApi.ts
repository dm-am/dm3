import type {
  Envelope,
  ListEnvelope,
  User,
} from "@/shared/api/models/common";
import type {
  Comment,
  CommentId,
  Board,
  BoardId,
  Topic,
  TopicId,
  TopicsQuery,
  CommentsQuery,
} from "../model/types";
import { Api } from "@/shared/api";
import { BbRenderMode } from "@/shared/api";
import type { Patch, Post } from "@/shared/api/models";

// Well-known board aliases
const NEWS_BOARD_ALIAS = "news";

export default new (class ForumApi {
  public getBoards() {
    return Api.get<ListEnvelope<Board>>("boards");
  }

  public getBoard(id: BoardId) {
    return Api.get<Envelope<Board>>(`boards/${id}`);
  }

  public getNews() {
    // Fetch recent news for homepage filtering by date
    return Api.get<ListEnvelope<Topic>>(`boards/${NEWS_BOARD_ALIAS}/topics`, {
      take: 5,
    });
  }

  public getModerators(id: BoardId) {
    return Api.get<ListEnvelope<User>>(`boards/${id}/moderators`);
  }

  public getTopics(id: BoardId, q: TopicsQuery) {
    // Convert page number to skip/take for backend
    const pageSize = q.size ?? 20;
    const queryParams: Record<string, string | number | boolean | string[] | undefined> = {
      take: pageSize,
    };

    // Paging
    if (q.number && q.number > 1) {
      queryParams.skip = (q.number - 1) * pageSize;
    }

    // Filtering
    if (q.isAttached !== undefined) {
      queryParams.isAttached = q.isAttached;
    }
    if (q.search) {
      queryParams.search = q.search;
    }
    if (q.authors && q.authors.length > 0) {
      queryParams.authors = q.authors;
    }
    if (q.createdFromUtc) {
      queryParams.createdFromUtc = q.createdFromUtc;
    }
    if (q.createdToUtc) {
      queryParams.createdToUtc = q.createdToUtc;
    }

    // Sorting
    if (q.sortBy) {
      queryParams.sortBy = q.sortBy;
    }
    if (q.sortOrder) {
      queryParams.sortOrder = q.sortOrder;
    }

    return Api.get<ListEnvelope<Topic>>(`boards/${id}/topics`, queryParams);
  }

  public reorderPinnedTopics(boardId: BoardId, topicIds: string[]) {
    return Api.patch(`boards/${boardId}/topics/pinned/order`, { topicIds });
  }

  public updateTopic(id: TopicId, topic: Patch<Topic>) {
    return Api.patch<Envelope<Topic>>(`topics/${id}`, topic);
  }

  public createTopic(id: BoardId, topic: Post<Topic>) {
    return Api.post<Envelope<Topic>>(`boards/${id}/topics`, topic);
  }

  public getTopic(id: TopicId) {
    return Api.get<Envelope<Topic>>(`topics/${id}`);
  }

  public getTopicByNumber(boardAlias: string, topicNumber: number) {
    return Api.get<Envelope<Topic>>(`forum/${boardAlias}/${topicNumber}`);
  }

  public markBoardAsRead(id: BoardId) {
    return Api.delete(`boards/${id}/comments/unread`);
  }

  public markForumAsRead() {
    return Api.delete("forum/comments/unread");
  }

  public markTopicAsRead(id: TopicId) {
    return Api.delete(`topics/${id}/comments/unread`);
  }

  public getComments(id: TopicId, q: CommentsQuery) {
    // Convert page number to skip/take for backend
    const pageSize = q.size ?? 20;
    const queryParams: Record<string, string | number | string[] | undefined> = {
      take: pageSize,
    };

    // Paging
    if (q.number && q.number > 1) {
      queryParams.skip = (q.number - 1) * pageSize;
    }

    // Filtering
    if (q.search) {
      queryParams.search = q.search;
    }
    if (q.authors && q.authors.length > 0) {
      queryParams.authors = q.authors;
    }
    if (q.createdFromUtc) {
      queryParams.createdFromUtc = q.createdFromUtc;
    }
    if (q.createdToUtc) {
      queryParams.createdToUtc = q.createdToUtc;
    }

    // Sorting
    if (q.sortBy) {
      queryParams.sortBy = q.sortBy;
    }
    if (q.sortOrder) {
      queryParams.sortOrder = q.sortOrder;
    }

    return Api.get<ListEnvelope<Comment>>(`topics/${id}/comments`, queryParams);
  }

  public createComment(id: TopicId, comment: Post<Comment>) {
    return Api.post<Comment>(`topics/${id}/comments`, comment);
  }

  public updateComment(id: CommentId, comment: Patch<Comment>) {
    return Api.patch<Comment>(`topics/comments/${id}`, comment);
  }

  public deleteComment(id: CommentId) {
    return Api.delete(`topics/comments/${id}`);
  }

  public getCommentForUpdate(id: CommentId) {
    return Api.get<Comment>(
      `topics/comments/${id}`,
      undefined,
      BbRenderMode.Bb,
    );
  }

  public postCommentLike(id: CommentId) {
    return Api.post<User>(`topics/comments/${id}/likes`);
  }
  public deleteCommentLike(id: CommentId) {
    return Api.delete(`topics/comments/${id}/likes`);
  }

  public postTopicLike(id: TopicId) {
    return Api.post<User>(`topics/${id}/likes`);
  }
  public deleteTopicLike(id: TopicId) {
    return Api.delete(`topics/${id}/likes`);
  }
})();
