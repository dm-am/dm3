import type {
  Envelope,
  ListEnvelope,
  Comment,
  User,
} from "@/shared/api/models/common";
import type { FirstUnreadCommentResult } from "../model/postTypes";
import { Api, toCommentsQueryParams, type CommentsQuery } from "@/shared/api";
import { RENDER_AUDIENCE } from "@/shared/api/audience";
import type { RequestOptions } from "@/shared/api/interceptor";

const discussionApi = {
  getGameComments(gameId: string, query?: CommentsQuery) {
    return Api.get<ListEnvelope<Comment>>(
      `games/${gameId}/comments`,
      toCommentsQueryParams(query),
    );
  },

  createGameComment(gameId: string, comment: { text: string }) {
    return Api.post<Envelope<Comment>>(`games/${gameId}/comments`, comment);
  },

  updateGameComment(id: string, comment: { text: string }) {
    return Api.patch<Envelope<Comment>>(`games/comments/${id}`, comment);
  },

  deleteGameComment(id: string, options?: RequestOptions) {
    return Api.delete(`games/comments/${id}`, undefined, options);
  },

  /** The raw BBCode for the editor: AuthorEdit keeps [private] and [mod] for the author. */
  getGameCommentForEdit(id: string) {
    return Api.get<Envelope<Comment>>(
      `games/comments/${id}`,
      undefined,
      RENDER_AUDIENCE.AuthorEdit,
    );
  },

  likeGameComment(id: string) {
    return Api.post<Envelope<User>>(`games/comments/${id}/likes`);
  },

  unlikeGameComment(id: string) {
    return Api.delete(`games/comments/${id}/likes`);
  },

  getFirstUnreadComment(gameId: string) {
    return Api.get<Envelope<FirstUnreadCommentResult>>(
      `games/${gameId}/comments/first-unread`,
    );
  },

  markCommentsAsRead(gameId: string) {
    return Api.delete(`games/${gameId}/comments/unread`);
  },
};

export default discussionApi;
