import type {
  Envelope,
  ListEnvelope,
  PagingQuery,
} from "@/shared/api/models/common";
import type { GameReview } from "@/shared/api/models/game/reviews";
import { Api } from "@/shared/api";
import { RENDER_AUDIENCE } from "@/shared/api/audience";
import type { RequestOptions } from "@/shared/api/interceptor";

const gameReviewApi = {
  getGameReviews(gameId: string, paging?: PagingQuery) {
    const queryParams: Record<string, number | undefined> = {};
    const pageSize = paging?.take ?? 20;
    queryParams.take = pageSize;
    if (paging?.number && paging.number > 1) {
      queryParams.skip = (paging.number - 1) * pageSize;
    } else if (paging?.skip) {
      queryParams.skip = paging.skip;
    }
    return Api.get<ListEnvelope<GameReview>>(
      `games/${gameId}/reviews`,
      queryParams,
    );
  },

  createGameReview(gameId: string, review: { text: string }) {
    return Api.post<Envelope<GameReview>>(`games/${gameId}/reviews`, review);
  },

  /** The source for the editor: the listing carries the display rendering. */
  getGameReviewForEdit(gameId: string, reviewId: string) {
    return Api.get<Envelope<GameReview>>(
      `games/${gameId}/reviews/${reviewId}`,
      undefined,
      RENDER_AUDIENCE.AuthorEdit,
    );
  },

  /** An omitted text keeps the stored one. */
  updateGameReview(gameId: string, reviewId: string, patch: { text?: string }) {
    return Api.patch<Envelope<GameReview>>(
      `games/${gameId}/reviews/${reviewId}`,
      patch,
    );
  },

  deleteGameReview(gameId: string, reviewId: string, options?: RequestOptions) {
    return Api.delete(
      `games/${gameId}/reviews/${reviewId}`,
      undefined,
      options,
    );
  },
};

export default gameReviewApi;
