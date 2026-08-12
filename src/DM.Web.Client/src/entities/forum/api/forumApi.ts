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
import { Api, toCommentsQueryParams } from "@/shared/api";
import { RENDER_AUDIENCE } from "@/shared/api";
import type { Patch, Post } from "@/shared/api/models";

// Well-known board aliases
const NEWS_BOARD_ALIAS = "news";

/**
 * How many news cards the homepage block shows at once. Owner's rule: having
 * more than two at a time is too many. It caps the request and the render
 * alike — the block's freshness window (last seven days) decides WHICH news
 * qualify, never HOW MANY are shown.
 */
export const NEWS_WIDGET_LIMIT = 2;

export default new (class ForumApi {
  public getBoards() {
    return Api.get<ListEnvelope<Board>>("boards");
  }

  public getBoard(id: BoardId) {
    return Api.get<Envelope<Board>>(`boards/${id}`);
  }

  public getNews() {
    // Homepage news block. The sort key is explicit because the server's
    // default is last activity (TopicRepository), which floats an older topic
    // above a newer one the moment somebody comments on it — news are ordered
    // by publication, not by discussion. `take` here is the block's hard cap,
    // not a page size.
    return Api.get<ListEnvelope<Topic>>(`boards/${NEWS_BOARD_ALIAS}/topics`, {
      take: NEWS_WIDGET_LIMIT,
      sortBy: "created",
      sortOrder: "desc",
    });
  }

  public getModerators(id: BoardId) {
    return Api.get<ListEnvelope<User>>(`boards/${id}/moderators`);
  }

  public getTopics(id: BoardId, q: TopicsQuery) {
    return Api.get<ListEnvelope<Topic>>(
      `boards/${id}/topics`,
      this.buildTopicsParams(q),
    );
  }

  /**
   * Cross-board topic search — backs the user-profile "Topics" tab. The
   * caller is expected to set `q.authors = [username]` to scope results
   * to a specific user; access policy is enforced server-side so private
   * boards never leak into the response.
   *
   * Shares the same TopicsQuery → query-string mapping as `getTopics` so
   * both call sites stay in lock-step when a new filter is added.
   */
  public getAllTopics(q: TopicsQuery) {
    return Api.get<ListEnvelope<Topic>>("topics", this.buildTopicsParams(q));
  }

  /**
   * One place — and one place only — where TopicsQuery becomes
   * query-string parameters. The per-board and cross-board endpoints
   * share this builder so a new filter automatically propagates to both
   * without diverging.
   */
  private buildTopicsParams(q: TopicsQuery) {
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
    // `authorUsernames` on the wire, per the API query vocabulary; `authors`
    // is the name this query type and the route use.
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

  /**
   * The body is the whole pinned order of the board, so the verb is PUT: the
   * server refuses a list that is not the board's pinned topics, each named
   * once. The caller passes everything the pinned block holds.
   */
  public reorderPinnedTopics(boardId: BoardId, topicIds: string[]) {
    return Api.put(`boards/${boardId}/topics/pinned/order`, { topicIds });
  }

  public updateTopic(id: TopicId, topic: Patch<Topic>) {
    return Api.patch<Envelope<Topic>>(`topics/${id}`, topic);
  }

  /** Delete a topic (author or moderator; backend DELETE v1/topics/{id}). */
  public deleteTopic(id: TopicId) {
    return Api.delete(`topics/${id}`);
  }

  /**
   * Fetch a topic's first-post raw BBCode source for the editor (AuthorEdit
   * audience round-trips [private]/[mod] for the author).
   */
  public getTopicForUpdate(id: TopicId) {
    return Api.get<Envelope<Topic>>(
      `topics/${id}`,
      undefined,
      RENDER_AUDIENCE.AuthorEdit,
    );
  }

  /**
   * Create a new topic on a board.
   *
   * The wire body intentionally does NOT reuse `Post<Topic>` — the API DTO
   * (CreateTopicRequest on the server) takes `{ title, text }`, not
   * `{ title, description }` like the Topic resource's own field name.
   * Passing `description` here would silently fail to bind to the
   * server-side `Text` property (both required, non-empty).
   */
  public createTopic(id: BoardId, topic: { title: string; text: string }) {
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

  /**
   * Where this reader continues in the topic: the first comment he has not
   * read, or the topic's last comment when everything is read. Backs the
   * card's comments counter, which lands him on that comment instead of at
   * the top of the discussion. Addressed by alias and number, like the topic
   * itself, so the resolver needs no separate lookup of the topic id.
   */
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
    return Api.post<Comment>(`topics/${id}/comments`, comment);
  }

  public updateComment(id: CommentId, comment: Patch<Comment>) {
    return Api.patch<Envelope<Comment>>(`forum/comments/${id}`, comment);
  }

  public deleteComment(id: CommentId) {
    return Api.delete(`forum/comments/${id}`);
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
    return Api.post<User>(`topics/${id}/likes`);
  }
  public deleteTopicLike(id: TopicId) {
    return Api.delete(`topics/${id}/likes`);
  }
})();
