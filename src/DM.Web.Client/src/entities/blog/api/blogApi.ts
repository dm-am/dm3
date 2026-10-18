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

/**
 * Who says the refusal out loud, passed through to the HTTP client untouched.
 *
 * A screen that answers a failure where the reader is looking — a card standing
 * in place of the text it could not load — is the one carrier of that failure,
 * and the response interceptor has to stay quiet or the same sentence stands in
 * two places at once. Every call that leaves these out is unchanged: the client
 * then sees nothing and the interceptor speaks as it always did.
 *
 * Declared here rather than imported: `shared/api` keeps its own copy of the
 * type to itself, and the client reads the two fields off the request by name,
 * so a structural twin carries them just as well.
 */
type RefusalOptions = {
  /** The screen shows a 403 and a 5xx itself. */
  ownsRefusal?: boolean;
  /** The screen shows a 429 itself, the wait included. */
  ownsRateLimit?: boolean;
};

/**
 * Convert a 1-based page number to skip/take (backend paginates with
 * skip/take; the client works with page numbers). Mirrors gameApi.
 */
function toSkipTake(paging?: PagingQuery): Record<string, number | undefined> {
  const queryParams: Record<string, number | undefined> = {};
  const pageSize = paging?.take ?? 20;
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
  public getPublicBlogs(query?: PagingQuery) {
    if (!query) {
      return Api.get<ListEnvelope<Blog>>("blogs");
    }

    // Convert page number to skip/take for backend
    const pageSize = query.take ?? 20;
    const queryParams: Record<string, number | undefined> = {
      take: pageSize,
    };

    if (query.number && query.number > 1) {
      queryParams.skip = (query.number - 1) * pageSize;
    } else if (query.skip) {
      queryParams.skip = query.skip;
    }

    return Api.get<ListEnvelope<Blog>>("blogs", queryParams);
  }

  /**
   * Get active blogs for sidebar (lightweight refs)
   */
  public getActiveBlogs() {
    return Api.get<ListEnvelope<BlogRef>>("blogs", {
      statuses: ["Active"],
      take: 5,
      projection: "ref",
    });
  }

  /**
   * Get popular blogs sorted by subscriber count (lightweight refs for sidebar)
   */
  public getPopularBlogs() {
    return Api.get<ListEnvelope<BlogRef>>("blogs", {
      sortBy: "popularity",
      take: 10,
      projection: "ref",
    });
  }

  /**
   * Get blogs where current user participates (author, assistant, or subscriber)
   * Uses lightweight BlogRef projection for sidebar efficiency
   */
  public getParticipatingBlogs() {
    return Api.get<ListEnvelope<BlogRef>>("blogs", {
      participating: true,
      projection: "ref",
    });
  }

  /**
   * Get blogs where user is author or assistant
   */
  public getUserBlogs(username: string) {
    return Api.get<ListEnvelope<Blog>>("blogs", { authorUsername: username });
  }

  /**
   * Get blogs filtered by host usernames (owner OR assistant, OR logic).
   * Use this for the profile "В роли ведущего" section.
   */
  public getBlogsByHost(username: string, take = 100) {
    return Api.get<ListEnvelope<Blog>>("blogs", {
      hostUsernames: [username],
      take,
    });
  }

  /**
   * Get all blogs the current authenticated user participates in
   * (owner, assistant, mentor, or reader). Used for the profile
   * "В роли читателя" section — only visible on own profile.
   */
  public getMyParticipatingBlogs(take = 100) {
    return Api.get<ListEnvelope<Blog>>("blogs", {
      participating: true,
      take,
    });
  }

  /**
   * Get the user's most-liked (published) publication across every blog
   * they author. Drives the profile "Blogs" tab spotlight. The envelope's
   * `resource` is `null` when the user has no published publications —
   * callers branch on that instead of failing the request.
   */
  public getUserBestPublication(username: string) {
    return Api.get<Envelope<Publication | null>>(
      `users/${encodeURIComponent(username)}/best-publication`,
    );
  }

  // === Single blog (BlogController) ===
  // Every `{id}` below is publicId-tolerant on the backend (5-letter public
  // id or GUID), mirroring the game endpoints.

  public getBlog(id: string) {
    return Api.get<Envelope<Blog>>(`blogs/${id}`);
  }

  /** Create a blog (POST v1/blogs, 201 + Envelope<Blog>). */
  public createBlog(input: CreateBlogInput) {
    return Api.post<Envelope<Blog>>("blogs", input);
  }

  public updateBlog(id: string, patch: UpdateBlogInput) {
    return Api.patch<Envelope<Blog>>(`blogs/${id}`, patch);
  }

  public deleteBlog(id: string, options?: RefusalOptions) {
    return Api.delete(`blogs/${id}`, undefined, options);
  }

  /**
   * Blog status transition (Start / Freeze / Close / Reopen), mirroring
   * POST games/{id}/status. Both endpoints run the one status machine
   * (ModuleStatusPolicy); the blog half is gated by BlogIntention
   * .SetStatusActive / .SetStatusClosed — owner or assistant.
   */
  public transitionStatus(
    id: string,
    transition: BlogStatusTransition,
    options?: RefusalOptions,
  ) {
    return Api.post<Envelope<Blog>>(
      `blogs/${id}/status`,
      { transition },
      options,
    );
  }

  /**
   * Premoderation transition (POST v1/blogs/{id}/premoderation). The endpoint
   * is authentication-gated and checks the rank per move: SetApproved and
   * SetAwaitingEdits are Mentor+, SubmitForApproval belongs to the owner alone.
   */
  public changePremoderation(
    id: string,
    transition: BlogPremoderationTransition,
    options?: RefusalOptions,
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
    options?: RefusalOptions,
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

  /**
   * One publication.
   *
   * `options` is empty unless a caller says otherwise, so the redirect and the
   * editor keep the behaviour they had; the publication page, which puts the
   * failure in a card instead of the text, takes the refusal over. The audience
   * is the default one, spelled out because the options sit behind it.
   */
  public getPublication(id: string, options?: RefusalOptions) {
    return Api.get<Envelope<Publication>>(
      `publications/${id}`,
      undefined,
      RENDER_AUDIENCE.Display,
      options,
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

  public deletePublication(id: string, options?: RefusalOptions) {
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

  public deleteBlogComment(id: string, options?: RefusalOptions) {
    return Api.delete(`blogs/comments/${id}`, undefined, options);
  }

  /**
   * Fetch a comment's raw BBCode source for the editor (AuthorEdit audience
   * round-trips [private]/[mod] for the author).
   */
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
  // The publication discussion runs the same section as the blog one, so it
  // reads the same query (search, authors, period, sort) and owns the same
  // single-comment mutations. It used to send paging alone, which is why the
  // publication page had no filter bar to send anything else from.

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

  public deletePublicationComment(id: string, options?: RefusalOptions) {
    return Api.delete(`publications/comments/${id}`, undefined, options);
  }

  /**
   * Raw BBCode source of a publication comment for the editor (the AuthorEdit
   * audience round-trips [private]/[mod] for its author), twin of
   * getBlogCommentForEdit.
   */
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

  public getUsers(
    id: string,
    role?: "owner" | "assistant" | "mentor" | "reader",
  ) {
    return Api.get<ListEnvelope<BlogUser>>(
      `blogs/${id}/users`,
      role ? { role } : undefined,
    );
  }

  public getAssistants(id: string) {
    return Api.get<ListEnvelope<BlogUser>>(`blogs/${id}/users/assistants`);
  }

  public removeAssistant(
    id: string,
    username: string,
    options?: RefusalOptions,
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

  public deleteNote(blogId: string, entryId: string, options?: RefusalOptions) {
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
