import type {
  Comment,
  Envelope,
  ListEnvelope,
  PagingQuery,
  User,
} from "@/shared/api/models/common";
import type {
  NotepadEntry,
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
import { Api, toCommentsQueryParams, type CommentsQuery } from "@/shared/api";
import { RENDER_AUDIENCE } from "@/shared/api/audience";

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

  public deleteBlog(id: string) {
    return Api.delete(`blogs/${id}`);
  }

  /**
   * Blog status transition (Start / Freeze / Close / Reopen), mirroring
   * POST games/{id}/status. Both endpoints run the one status machine
   * (ModuleStatusPolicy); the blog half is gated by BlogIntention
   * .SetStatusActive / .SetStatusClosed — owner or assistant.
   */
  public transitionStatus(id: string, transition: BlogStatusTransition) {
    return Api.post<Envelope<Blog>>(`blogs/${id}/status`, { transition });
  }

  /**
   * Premoderation transition (POST v1/blogs/{id}/premoderation). The endpoint
   * is authentication-gated and checks the rank per move: SetApproved and
   * SetAwaitingEdits are Mentor+, SubmitForApproval belongs to the owner alone.
   */
  public changePremoderation(
    id: string,
    transition: BlogPremoderationTransition,
  ) {
    return Api.post<Envelope<Blog>>(`blogs/${id}/premoderation`, {
      transition,
    });
  }

  // === Rubrics (BlogController) ===

  public createRubric(blogId: string, input: CreateRubricInput) {
    return Api.post<Envelope<Rubric>>(`blogs/${blogId}/rubrics`, input);
  }

  public deleteRubric(blogId: string, rubricId: string) {
    return Api.delete(`blogs/${blogId}/rubrics/${rubricId}`);
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

  public getPublication(id: string) {
    return Api.get<Envelope<Publication>>(`publications/${id}`);
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

  public deletePublication(id: string) {
    return Api.delete(`publications/${id}`);
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
    return Api.post<Comment>(`blogs/${blogId}/comments`, comment);
  }

  public markBlogCommentsAsRead(blogId: string) {
    return Api.delete(`blogs/${blogId}/comments/unread`);
  }

  public updateBlogComment(id: string, comment: { text: string }) {
    return Api.patch<Envelope<Comment>>(`blogs/comments/${id}`, comment);
  }

  public deleteBlogComment(id: string) {
    return Api.delete(`blogs/comments/${id}`);
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
    return Api.post<Comment>(`publications/${publicationId}/comments`, comment);
  }

  public updatePublicationComment(id: string, comment: { text: string }) {
    return Api.patch<Envelope<Comment>>(`publications/comments/${id}`, comment);
  }

  public deletePublicationComment(id: string) {
    return Api.delete(`publications/comments/${id}`);
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

  public removeAssistant(id: string, username: string) {
    return Api.delete(
      `blogs/${id}/users/assistants/${encodeURIComponent(username)}`,
    );
  }

  // === Blog notepad (BlogNotepadController) ===

  public getNotepad(blogId: string) {
    return Api.get<ListEnvelope<NotepadEntry>>(`blogs/${blogId}/notepad`);
  }

  public createNote(blogId: string, input: CreateNotepadEntryRequest) {
    return Api.post<Envelope<NotepadEntry>>(`blogs/${blogId}/notepad`, input);
  }

  public updateNote(
    blogId: string,
    entryId: string,
    input: UpdateNotepadEntryRequest,
  ) {
    return Api.patch<Envelope<NotepadEntry>>(
      `blogs/${blogId}/notepad/${entryId}`,
      input,
    );
  }

  public deleteNote(blogId: string, entryId: string) {
    return Api.delete(`blogs/${blogId}/notepad/${entryId}`);
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
