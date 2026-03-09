import type { ListEnvelope, PagingQuery } from "@/shared/api/models/common";
import type { Blog, BlogUser } from "../model/types";
import { Api } from "@/shared/api";

export default new (class {
  public getPublicBlogs(query?: PagingQuery) {
    return Api.get<ListEnvelope<Blog>>("blogs", query);
  }

  public getPopularBlogs() {
    return Api.get<ListEnvelope<Blog>>("blogs/popular");
  }

  public getSubscribedBlogs() {
    return Api.get<ListEnvelope<Blog>>("blogs/subscribed");
  }

  public getUserBlogs(username: string) {
    return Api.get<ListEnvelope<Blog>>(`blogs/user/${username}`);
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
    return Api.post<unknown>(`blogs/${blogId}/invitations/assistants`, { username });
  }

  public inviteReader(blogId: string, username: string) {
    return Api.post<unknown>(`blogs/${blogId}/invitations/readers`, { username });
  }

  public cancelInvitation(blogId: string, tokenId: string) {
    return Api.delete(`blogs/${blogId}/invitations/${tokenId}`);
  }
})();
