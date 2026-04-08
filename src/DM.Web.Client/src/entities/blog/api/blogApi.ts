import type { ListEnvelope, PagingQuery } from "@/shared/api/models/common";
import type { Blog, BlogRef, BlogUser } from "../model/types";
import { Api } from "@/shared/api";

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
      status: "Active",
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

  // Blog users
  public getUsers(blogId: string) {
    return Api.get<ListEnvelope<BlogUser>>(`blogs/${blogId}/users`);
  }

  public getAssistants(blogId: string) {
    return Api.get<ListEnvelope<BlogUser>>(`blogs/${blogId}/users/assistants`);
  }

  public getReaders(blogId: string) {
    return Api.get<ListEnvelope<unknown>>(`blogs/${blogId}/readers`);
  }

  public subscribe(blogId: string) {
    return Api.post<unknown>(`blogs/${blogId}/readers`);
  }

  public unsubscribe(blogId: string) {
    return Api.delete(`blogs/${blogId}/readers`);
  }

  // Blog invitations
  public getInvitations(blogId: string) {
    return Api.get<ListEnvelope<unknown>>(`blogs/${blogId}/invitations`);
  }

  public inviteAssistant(blogId: string, username: string) {
    return Api.post<unknown>(`blogs/${blogId}/invitations/assistants`, {
      username,
    });
  }

  public inviteReader(blogId: string, username: string) {
    return Api.post<unknown>(`blogs/${blogId}/invitations/readers`, {
      username,
    });
  }

  public cancelInvitation(blogId: string, tokenId: string) {
    return Api.delete(`blogs/${blogId}/invitations/${tokenId}`);
  }
})();
