import type {
  Comment,
  Envelope,
  ListEnvelope,
  PagingQuery,
  User,
} from "@/shared/api/models/common";
import type {
  CreateNotepadEntryRequest,
  UpdateNotepadEntryRequest,
} from "@/shared/api/models/notepads";
import type {
  Blog,
  BlogInvitation,
  BlogPremoderationTransition,
  BlogRef,
  BlogStatusTransition,
  BlogUser,
  CreateBlogInput,
  CreatePublicationInput,
  CreateRubricInput,
  Publication,
  Rubric,
  UpdateBlogInput,
  UpdatePublicationInput,
} from "../model/types";
import {
  Api,
  notepadEndpoints,
  toCommentsQueryParams,
  type CommentsQuery,
} from "@/shared/api";
import { RENDER_AUDIENCE } from "@/shared/api/audience";
import type { RequestOptions } from "@/shared/api/interceptor";
import { DEFAULT_PAGE_SIZE } from "@/shared/lib/composables/usePaging";
import { SIDEBAR_BLOG_ROWS } from "../model/sidebarRows";

/** The server pages by skip and take, the client by 1-based page numbers. */
function toSkipTake(paging?: PagingQuery): Record<string, number | undefined> {
  const queryParams: Record<string, number | undefined> = {};
  const pageSize = paging?.take ?? DEFAULT_PAGE_SIZE;
  queryParams.take = pageSize;

  if (paging?.number && paging.number > 1) {
    queryParams.skip = (paging.number - 1) * pageSize;
  } else if (paging?.skip) {
    queryParams.skip = paging.skip;
  }

  return queryParams;
}

/** The calls of one blog's notepad. */
function notepadOf(blogId: string) {
  return notepadEndpoints(`blogs/${blogId}/notepad`);
}

export default new (class {
  public getActiveBlogs(take: number = SIDEBAR_BLOG_ROWS) {
    return Api.get<ListEnvelope<BlogRef>>("blogs", {
      statuses: ["Active"],
      take,
      projection: "ref",
    });
  }

  public getPopularBlogs() {
    return Api.get<ListEnvelope<BlogRef>>("blogs", {
      sortBy: "popularity",
      take: 10,
      projection: "ref",
    });
  }

  public getParticipatingBlogs() {
    return Api.get<ListEnvelope<BlogRef>>("blogs", {
      participating: true,
      projection: "ref",
    });
  }

  /** The blogs the given users host, as the full rows of /blogs. */
  public getHostedBlogs(query: {
    hostUsernames: string[];
    search?: string;
    sortBy: string;
    sortOrder: string;
    take: number;
    skip?: number;
  }) {
    return Api.get<ListEnvelope<Blog>>("blogs", query);
  }

  /** The user's most-liked publication; `resource` is null when there is none. */
  public getUserBestPublication(username: string) {
    return Api.get<Envelope<Publication | null>>(
      `users/${encodeURIComponent(username)}/best-publication`,
    );
  }

  // === Single blog (BlogController) ===
  // Every `{id}` takes the public id or the GUID.

  public getBlog(id: string) {
    return Api.get<Envelope<Blog>>(`blogs/${id}`);
  }

  /** The description as the owner typed it; anybody else is given it rendered. */
  public getBlogForEdit(id: string) {
    return Api.get<Envelope<Blog>>(
      `blogs/${id}`,
      undefined,
      RENDER_AUDIENCE.AuthorEdit,
    );
  }

  public createBlog(input: CreateBlogInput) {
    return Api.post<Envelope<Blog>>("blogs", input);
  }

  public updateBlog(id: string, patch: UpdateBlogInput) {
    return Api.patch<Envelope<Blog>>(`blogs/${id}`, patch);
  }

  public deleteBlog(id: string, options?: RequestOptions) {
    return Api.delete(`blogs/${id}`, undefined, options);
  }

  public transitionStatus(
    id: string,
    transition: BlogStatusTransition,
    options?: RequestOptions,
  ) {
    return Api.post<Envelope<Blog>>(
      `blogs/${id}/status`,
      { transition },
      options,
    );
  }

  public changePremoderation(
    id: string,
    transition: BlogPremoderationTransition,
    options?: RequestOptions,
  ) {
    return Api.post<Envelope<Blog>>(
      `blogs/${id}/premoderation`,
      { transition },
      options,
    );
  }

  // === Rubrics (BlogController) ===

  public createRubric(blogId: string, input: CreateRubricInput) {
    return Api.post<Envelope<Rubric>>(`blogs/${blogId}/rubrics`, input);
  }

  public deleteRubric(
    blogId: string,
    rubricId: string,
    options?: RequestOptions,
  ) {
    return Api.delete(
      `blogs/${blogId}/rubrics/${rubricId}`,
      undefined,
      options,
    );
  }

  // === Publications (PublicationController) ===

  public getPublications(
    blogId: string,
    options?: { rubricId?: string; paging?: PagingQuery },
  ) {
    const queryParams: Record<string, number | string | undefined> = {
      ...toSkipTake(options?.paging),
    };
    if (options?.rubricId) queryParams.rubricId = options.rubricId;
    return Api.get<ListEnvelope<Publication>>(
      `blogs/${blogId}/publications`,
      queryParams,
    );
  }

  // The audience is the default one, spelled out because the options follow it.
  public getPublication(id: string, options?: RequestOptions) {
    return Api.get<Envelope<Publication>>(
      `publications/${id}`,
      undefined,
      RENDER_AUDIENCE.Display,
      options,
    );
  }

  /** The content as its author typed it; anybody else is given it rendered. */
  public getPublicationForEdit(id: string) {
    return Api.get<Envelope<Publication>>(
      `publications/${id}`,
      undefined,
      RENDER_AUDIENCE.AuthorEdit,
    );
  }

  public createPublication(blogId: string, input: CreatePublicationInput) {
    return Api.post<Envelope<Publication>>(
      `blogs/${blogId}/publications`,
      input,
    );
  }

  public updatePublication(id: string, input: UpdatePublicationInput) {
    return Api.patch<Envelope<Publication>>(`publications/${id}`, input);
  }

  public deletePublication(id: string, options?: RequestOptions) {
    return Api.delete(`publications/${id}`, undefined, options);
  }

  public likePublication(id: string) {
    return Api.post<Envelope<User>>(`publications/${id}/likes`);
  }

  public unlikePublication(id: string) {
    return Api.delete(`publications/${id}/likes`);
  }

  // === Blog discussion comments (BlogCommentController) ===

  public getBlogComments(blogId: string, query?: CommentsQuery) {
    return Api.get<ListEnvelope<Comment>>(
      `blogs/${blogId}/comments`,
      toCommentsQueryParams(query),
    );
  }

  public createBlogComment(blogId: string, comment: { text: string }) {
    return Api.post<Envelope<Comment>>(`blogs/${blogId}/comments`, comment);
  }

  public markBlogCommentsAsRead(blogId: string) {
    return Api.delete(`blogs/${blogId}/comments/unread`);
  }

  public updateBlogComment(id: string, comment: { text: string }) {
    return Api.patch<Envelope<Comment>>(`blogs/comments/${id}`, comment);
  }

  public deleteBlogComment(id: string, options?: RequestOptions) {
    return Api.delete(`blogs/comments/${id}`, undefined, options);
  }

  /** The raw BBCode for the editor: AuthorEdit keeps [private] and [mod] for the author. */
  public getBlogCommentForEdit(id: string) {
    return Api.get<Envelope<Comment>>(
      `blogs/comments/${id}`,
      undefined,
      RENDER_AUDIENCE.AuthorEdit,
    );
  }

  public likeBlogComment(id: string) {
    return Api.post<Envelope<User>>(`blogs/comments/${id}/likes`);
  }

  public unlikeBlogComment(id: string) {
    return Api.delete(`blogs/comments/${id}/likes`);
  }

  // === Publication comments (PublicationCommentController) ===

  public getPublicationComments(publicationId: string, query?: CommentsQuery) {
    return Api.get<ListEnvelope<Comment>>(
      `publications/${publicationId}/comments`,
      toCommentsQueryParams(query),
    );
  }

  public createPublicationComment(
    publicationId: string,
    comment: { text: string },
  ) {
    return Api.post<Envelope<Comment>>(
      `publications/${publicationId}/comments`,
      comment,
    );
  }

  public updatePublicationComment(id: string, comment: { text: string }) {
    return Api.patch<Envelope<Comment>>(`publications/comments/${id}`, comment);
  }

  public deletePublicationComment(id: string, options?: RequestOptions) {
    return Api.delete(`publications/comments/${id}`, undefined, options);
  }

  public getPublicationCommentForEdit(id: string) {
    return Api.get<Envelope<Comment>>(
      `publications/comments/${id}`,
      undefined,
      RENDER_AUDIENCE.AuthorEdit,
    );
  }

  public likePublicationComment(id: string) {
    return Api.post<Envelope<User>>(`publications/comments/${id}/likes`);
  }

  public unlikePublicationComment(id: string) {
    return Api.delete(`publications/comments/${id}/likes`);
  }

  // === Readers / subscription (BlogReaderController) ===

  public getReaders(id: string) {
    return Api.get<ListEnvelope<BlogUser>>(`blogs/${id}/readers`);
  }

  public subscribe(id: string) {
    return Api.post<BlogUser>(`blogs/${id}/readers`);
  }

  public unsubscribe(id: string) {
    return Api.delete(`blogs/${id}/readers`);
  }

  // === Blog users & assistants (BlogUserController) ===

  public getUsers(id: string, role?: BlogUser["role"]) {
    return Api.get<ListEnvelope<BlogUser>>(
      `blogs/${id}/users`,
      role ? { role } : undefined,
    );
  }

  public removeAssistant(
    id: string,
    username: string,
    options?: RequestOptions,
  ) {
    return Api.delete(
      `blogs/${id}/users/assistants/${encodeURIComponent(username)}`,
      undefined,
      options,
    );
  }

  // === Blog notepad (BlogNotepadController) ===

  public getNotepad(blogId: string) {
    return notepadOf(blogId).list();
  }

  public createNote(blogId: string, input: CreateNotepadEntryRequest) {
    return notepadOf(blogId).create(input);
  }

  public updateNote(
    blogId: string,
    entryId: string,
    input: UpdateNotepadEntryRequest,
  ) {
    return notepadOf(blogId).update(entryId, input);
  }

  public deleteNote(blogId: string, entryId: string, options?: RequestOptions) {
    return notepadOf(blogId).remove(entryId, options);
  }

  // === Blog blacklist (BlogBlacklistController) ===

  public getBlacklist(id: string) {
    return Api.get<ListEnvelope<User>>(`blogs/${id}/blacklist`);
  }

  public addToBlacklist(id: string, username: string) {
    return Api.post<Envelope<User>>(`blogs/${id}/blacklist`, { username });
  }

  public removeFromBlacklist(id: string, username: string) {
    return Api.delete(`blogs/${id}/blacklist/${encodeURIComponent(username)}`);
  }

  // === Invitations (BlogInvitationController) ===

  public getInvitations(id: string) {
    return Api.get<ListEnvelope<BlogInvitation>>(`blogs/${id}/invitations`);
  }

  public inviteAssistant(id: string, username: string) {
    return Api.post<BlogInvitation>(`blogs/${id}/invitations/assistants`, {
      username,
    });
  }

  public inviteReader(id: string, username: string) {
    return Api.post<BlogInvitation>(`blogs/${id}/invitations/readers`, {
      username,
    });
  }

  public cancelInvitation(id: string, invitationId: string) {
    return Api.delete(`blogs/${id}/invitations/${invitationId}`);
  }
})();
