import type { ListEnvelope } from "./models/common";
import type {
  NotepadEntry,
  NotepadCategory,
  CreateNotepadEntryRequest,
  UpdateNotepadEntryRequest,
  CreateNotepadCategoryRequest,
  UpdateNotepadCategoryRequest,
} from "./models/notepads";
import Api from "./client";

export default new (class NotepadApi {
  // ==================== User Notepad ====================

  private userPath = "users/me/notepad";

  /**
   * Get all user notepad entries
   */
  public getUserEntries() {
    return Api.get<ListEnvelope<NotepadEntry>>(this.userPath);
  }

  /**
   * Get user notepad entry by ID
   */
  public getUserEntry(id: string) {
    return Api.get<NotepadEntry>(`${this.userPath}/${id}`);
  }

  /**
   * Create user notepad entry
   */
  public createUserEntry(request: CreateNotepadEntryRequest) {
    return Api.post<NotepadEntry>(this.userPath, request);
  }

  /**
   * Update user notepad entry
   */
  public updateUserEntry(id: string, request: UpdateNotepadEntryRequest) {
    return Api.patch<NotepadEntry>(`${this.userPath}/${id}`, request);
  }

  /**
   * Delete user notepad entry
   */
  public deleteUserEntry(id: string) {
    return Api.delete(`${this.userPath}/${id}`);
  }

  // ==================== Blog Notepad ====================

  private blogPath = (blogId: string) => `blogs/${blogId}/notepad`;

  /**
   * Get blog notepad entries (owner/assistant only)
   */
  public getBlogEntries(blogId: string) {
    return Api.get<ListEnvelope<NotepadEntry>>(this.blogPath(blogId));
  }

  /**
   * Get blog notepad entry by ID
   */
  public getBlogEntry(blogId: string, entryId: string) {
    return Api.get<NotepadEntry>(`${this.blogPath(blogId)}/${entryId}`);
  }

  /**
   * Create blog notepad entry
   */
  public createBlogEntry(blogId: string, request: CreateNotepadEntryRequest) {
    return Api.post<NotepadEntry>(this.blogPath(blogId), request);
  }

  /**
   * Update blog notepad entry
   */
  public updateBlogEntry(
    blogId: string,
    entryId: string,
    request: UpdateNotepadEntryRequest,
  ) {
    return Api.patch<NotepadEntry>(
      `${this.blogPath(blogId)}/${entryId}`,
      request,
    );
  }

  /**
   * Delete blog notepad entry
   */
  public deleteBlogEntry(blogId: string, entryId: string) {
    return Api.delete(`${this.blogPath(blogId)}/${entryId}`);
  }

  // ==================== Categories ====================

  private categoryPath = (
    notepadType: "user" | "game" | "blog",
    containerId?: string,
  ) => {
    if (notepadType === "user") return `${this.userPath}/categories`;
    if (notepadType === "game")
      return `games/${containerId}/notepad/categories`;
    return `blogs/${containerId}/notepad/categories`;
  };

  /**
   * Get notepad categories
   */
  public getCategories(
    notepadType: "user" | "game" | "blog",
    containerId?: string,
  ) {
    return Api.get<ListEnvelope<NotepadCategory>>(
      this.categoryPath(notepadType, containerId),
    );
  }

  /**
   * Create notepad category
   */
  public createCategory(
    notepadType: "user" | "game" | "blog",
    request: CreateNotepadCategoryRequest,
    containerId?: string,
  ) {
    return Api.post<NotepadCategory>(
      this.categoryPath(notepadType, containerId),
      request,
    );
  }

  /**
   * Update notepad category
   */
  public updateCategory(
    notepadType: "user" | "game" | "blog",
    categoryId: string,
    request: UpdateNotepadCategoryRequest,
    containerId?: string,
  ) {
    return Api.patch<NotepadCategory>(
      `${this.categoryPath(notepadType, containerId)}/${categoryId}`,
      request,
    );
  }

  /**
   * Delete notepad category
   */
  public deleteCategory(
    notepadType: "user" | "game" | "blog",
    categoryId: string,
    containerId?: string,
  ) {
    return Api.delete(
      `${this.categoryPath(notepadType, containerId)}/${categoryId}`,
    );
  }
})();
