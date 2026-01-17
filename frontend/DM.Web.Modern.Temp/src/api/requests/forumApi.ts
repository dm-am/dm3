import type { Envelope, ListEnvelope, PagingQuery } from "@/api/models/common";
import type { User } from "@/api/models/community";
import type {
  Comment,
  CommentId,
  Board,
  BoardId,
  Topic,
  TopicId,
} from "@/api/models/forum";
import Api from "@/api";
import { BbRenderMode } from "@/api/bbRenderMode";
import type { Patch, Post } from "@/api/models";

export default new (class ForumApi {
  public getBoards() {
    return Api.get<ListEnvelope<Board>>("boards");
  }

  public getBoard(id: BoardId) {
    return Api.get<Envelope<Board>>(`boards/${id}`);
  }

  public getNews() {
    return Api.get<ListEnvelope<Topic>>("boards/Новости проекта/topics", {
      size: 3,
    });
  }

  public getModerators(id: BoardId) {
    return Api.get<ListEnvelope<User>>(`boards/${id}/moderators`);
  }

  public getTopics(id: BoardId, q: PagingQuery, isAttached: boolean) {
    return Api.get<ListEnvelope<Topic>>(`boards/${id}/topics`, {
      ...q,
      isAttached,
    });
  }

  public createTopic(id: BoardId, topic: Post<Topic>) {
    return Api.post<Envelope<Topic>>(`boards/${id}/topics`, topic);
  }

  public getTopic(id: TopicId) {
    return Api.get<Envelope<Topic>>(`topics/${id}`);
  }

  public markBoardAsRead(id: BoardId) {
    return Api.delete(`boards/${id}/comments/unread`);
  }

  public markAllForumAsRead() {
    return Api.delete("forum/comments/unread");
  }

  public markTopicAsRead(id: TopicId) {
    return Api.delete(`topics/${id}/comments/unread`);
  }

  public getComments(id: TopicId, q: PagingQuery) {
    return Api.get<ListEnvelope<Comment>>(`topics/${id}/comments`, q);
  }

  public createComment(id: TopicId, comment: Post<Comment>) {
    return Api.post<Envelope<Comment>>(`topics/${id}/comments`, comment);
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
      BbRenderMode.Bb,
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
