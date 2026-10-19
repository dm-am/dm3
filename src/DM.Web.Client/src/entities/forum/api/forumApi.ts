import type { Envelope, ListEnvelope, User } from "@/shared/api/models/common";
import type {
  Comment,
  CommentId,
  Board,
  BoardId,
  FirstUnreadComment,
  Topic,
  TopicId,
  TopicsQuery,
  CommentsQuery,
} from "../model/types";
import { Api, RENDER_AUDIENCE, toCommentsQueryParams } from "@/shared/api";
import type { Patch, Post } from "@/shared/api/models";
import type { RequestOptions } from "@/shared/api/interceptor";

const NEWS_BOARD_ALIAS = "news";

// More than two news cards at a time is too many; freshness decides which, not how many.
export const NEWS_WIDGET_LIMIT = 2;

// Shared by the board and the cross-board search, so a new filter reaches both.
function buildTopicsParams(q: TopicsQuery) {
  const pageSize = q.size ?? 20;
  const queryParams: Record<
    string,
    string | number | boolean | string[] | undefined
  > = {
    take: pageSize,
  };

  if (q.number && q.number > 1) {
    queryParams.skip = (q.number - 1) * pageSize;
  }
  if (q.isAttached !== undefined) {
    queryParams.isAttached = q.isAttached;
  }
  if (q.search) {
    queryParams.search = q.search;
  }
  if (q.authors && q.authors.length > 0) {
    queryParams.authorUsernames = q.authors;
  }
  if (q.createdFromUtc) {
    queryParams.createdFromUtc = q.createdFromUtc;
  }
  if (q.createdToUtc) {
    queryParams.createdToUtc = q.createdToUtc;
  }
  if (q.sortBy) {
    queryParams.sortBy = q.sortBy;
  }
  if (q.sortOrder) {
    queryParams.sortOrder = q.sortOrder;
  }
  return queryParams;
}

export default new (class ForumApi {
  public getBoards() {
    return Api.get<ListEnvelope<Board>>("boards");
  }

  // The caller sets the flags per read: the other reads have only the interceptor's voice.
  public getBoard(id: BoardId, options?: RequestOptions) {
    return Api.get<Envelope<Board>>(
      `boards/${id}`,
      undefined,
      undefined,
      options,
    );
  }

  public getNews() {
    // The server sorts by last activity unless told; news go by publication.
    return Api.get<ListEnvelope<Topic>>(`boards/${NEWS_BOARD_ALIAS}/topics`, {
      take: NEWS_WIDGET_LIMIT,
      sortBy: "created",
      sortOrder: "desc",
    });
  }

  // The caller sets the flags per read: the pinned block refresh has only the interceptor's voice.
  public getTopics(id: BoardId, q: TopicsQuery, options?: RequestOptions) {
    return Api.get<ListEnvelope<Topic>>(
      `boards/${id}/topics`,
      buildTopicsParams(q),
      undefined,
      options,
    );
  }

  public getAllTopics(q: TopicsQuery) {
    return Api.get<ListEnvelope<Topic>>("topics", buildTopicsParams(q));
  }

  /** The whole pinned order of the board, each pinned topic named once. */
  public reorderPinnedTopics(boardId: BoardId, topicIds: string[]) {
    return Api.put(`boards/${boardId}/topics/pinned/order`, { topicIds });
  }

  public updateTopic(id: TopicId, topic: Patch<Topic>) {
    return Api.patch<Envelope<Topic>>(`topics/${id}`, topic);
  }

  public deleteTopic(id: TopicId, options?: RequestOptions) {
    return Api.delete(`topics/${id}`, undefined, options);
  }

  /** The raw BBCode for the editor: AuthorEdit keeps [private] and [mod] for the author. */
  public getTopicForUpdate(id: TopicId) {
    return Api.get<Envelope<Topic>>(
      `topics/${id}`,
      undefined,
      RENDER_AUDIENCE.AuthorEdit,
    );
  }

  // Not Post<Topic>: the request takes `text`, which the resource calls `description`.
  public createTopic(id: BoardId, topic: { title: string; text: string }) {
    return Api.post<Envelope<Topic>>(`boards/${id}/topics`, topic);
  }

  public getTopic(id: TopicId) {
    return Api.get<Envelope<Topic>>(`topics/${id}`);
  }

  public getTopicByNumber(boardAlias: string, topicNumber: number) {
    return Api.get<Envelope<Topic>>(`forum/${boardAlias}/${topicNumber}`);
  }

  public markForumAsRead() {
    return Api.delete("forum/comments/unread");
  }

  public markTopicAsRead(id: TopicId) {
    return Api.delete(`topics/${id}/comments/unread`);
  }

  /** The first unread comment, or the last one when everything is read. */
  public getFirstUnreadComment(boardAlias: string, topicNumber: number) {
    return Api.get<Envelope<FirstUnreadComment>>(
      `forum/${boardAlias}/${topicNumber}/comments/first-unread`,
    );
  }

  public getComments(id: TopicId, q: CommentsQuery) {
    return Api.get<ListEnvelope<Comment>>(
      `topics/${id}/comments`,
      toCommentsQueryParams(q),
    );
  }

  public createComment(id: TopicId, comment: Post<Comment>) {
    return Api.post<Envelope<Comment>>(`topics/${id}/comments`, comment);
  }

  public updateComment(id: CommentId, comment: Patch<Comment>) {
    return Api.patch<Envelope<Comment>>(`forum/comments/${id}`, comment);
  }

  public deleteComment(id: CommentId, options?: RequestOptions) {
    return Api.delete(`forum/comments/${id}`, undefined, options);
  }

  public getCommentForUpdate(id: CommentId) {
    return Api.get<Envelope<Comment>>(
      `forum/comments/${id}`,
      undefined,
      RENDER_AUDIENCE.AuthorEdit,
    );
  }

  public postCommentLike(id: CommentId) {
    return Api.post<Envelope<User>>(`forum/comments/${id}/likes`);
  }
  public deleteCommentLike(id: CommentId) {
    return Api.delete(`forum/comments/${id}/likes`);
  }

  public postTopicLike(id: TopicId) {
    return Api.post<Envelope<User>>(`topics/${id}/likes`);
  }
  public deleteTopicLike(id: TopicId) {
    return Api.delete(`topics/${id}/likes`);
  }
})();
