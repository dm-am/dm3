import type {
  Envelope,
  ListEnvelope,
  PagingQuery,
} from "@/shared/api/models/common";
import type {
  Post,
  PostReview,
  FirstUnreadPostResult,
  FirstUnreadRoomPostResult,
  RoomPostsFilter,
  RatedPostsQuery,
  CreatePostInput,
  UpdatePostInput,
} from "../model/postTypes";
import { Api } from "@/shared/api";
import { RENDER_AUDIENCE } from "@/shared/api/audience";
import type { RequestOptions } from "@/shared/api/interceptor";

const postApi = {
  getRatedPosts(params?: RatedPostsQuery) {
    return Api.get<ListEnvelope<Post>>("posts", params);
  },

  getPosts(roomId: string, paging?: PagingQuery, filter?: RoomPostsFilter) {
    const queryParams: Record<
      string,
      string | number | boolean | string[] | undefined
    > = { ...filter };
    const pageSize = paging?.take ?? 20;
    queryParams.take = pageSize;

    if (paging?.number && paging.number > 1) {
      queryParams.skip = (paging.number - 1) * pageSize;
    } else if (paging?.skip) {
      queryParams.skip = paging.skip;
    }

    return Api.get<ListEnvelope<Post>>(`rooms/${roomId}/posts`, queryParams);
  },

  getPost(postId: string, options?: RequestOptions) {
    return Api.get<Envelope<Post>>(
      `posts/${postId}`,
      undefined,
      undefined,
      options,
    );
  },

  // The display rendering flattens [private], and a save from it publishes the private text.
  getPostForEdit(postId: string) {
    return Api.get<Envelope<Post>>(
      `posts/${postId}`,
      undefined,
      RENDER_AUDIENCE.AuthorEdit,
    );
  },

  createPost(roomId: string, post: CreatePostInput) {
    return Api.post<Envelope<Post>>(`rooms/${roomId}/posts`, post);
  },

  updatePost(postId: string, patch: UpdatePostInput) {
    return Api.patch<Envelope<Post>>(`posts/${postId}`, patch);
  },

  deletePost(postId: string, options?: RequestOptions) {
    return Api.delete(`posts/${postId}`, undefined, options);
  },

  createPostReview(postId: string, request: { sign: number; text: string }) {
    return Api.post<Envelope<PostReview>>(`posts/${postId}/reviews`, request);
  },

  getPostReviews(postId: string, paging?: PagingQuery) {
    const queryParams: Record<string, number | undefined> = {};
    const pageSize = paging?.take ?? 20;
    queryParams.take = pageSize;

    if (paging?.number && paging.number > 1) {
      queryParams.skip = (paging.number - 1) * pageSize;
    } else if (paging?.skip) {
      queryParams.skip = paging.skip;
    }

    return Api.get<ListEnvelope<PostReview>>(
      `posts/${postId}/reviews`,
      queryParams,
    );
  },

  /** The source for the editor: the listing carries rendered HTML. */
  getPostReviewForEdit(postId: string, reviewId: string) {
    return Api.get<Envelope<PostReview>>(
      `posts/${postId}/reviews/${reviewId}`,
      undefined,
      RENDER_AUDIENCE.AuthorEdit,
    );
  },

  /** Fields left out keep their value. */
  updatePostReview(
    postId: string,
    reviewId: string,
    patch: { sign?: number; text?: string },
  ) {
    return Api.patch<Envelope<PostReview>>(
      `posts/${postId}/reviews/${reviewId}`,
      patch,
    );
  },

  deletePostReview(postId: string, reviewId: string, options?: RequestOptions) {
    return Api.delete(
      `posts/${postId}/reviews/${reviewId}`,
      undefined,
      options,
    );
  },

  // The GUID of the game, not its public id.
  getFirstUnreadPost(gameId: string) {
    return Api.get<Envelope<FirstUnreadPostResult>>(
      `games/${gameId}/posts/first-unread`,
    );
  },

  // The caller falls back to the first page on any failure, so no plate is raised.
  getFirstUnreadRoomPost(roomId: string, take: number) {
    return Api.get<Envelope<FirstUnreadRoomPostResult>>(
      `rooms/${roomId}/posts/first-unread`,
      { take },
      undefined,
      { ownsRefusal: true },
    );
  },

  markRoomAsRead(roomId: string) {
    return Api.delete(`rooms/${roomId}/posts/unread`);
  },
};

export default postApi;
