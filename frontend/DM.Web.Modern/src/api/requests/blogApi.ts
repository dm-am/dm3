import type { ListEnvelope, PagingQuery } from "@/api/models/common";
import type { Blog } from "@/api/models/blog";
import Api from "@/api";

export default new (class {
  public getPublicBlogs(query?: PagingQuery) {
    return Api.get<ListEnvelope<Blog>>("blogs", query);
  }

  public getPopularBlogs() {
    return Api.get<ListEnvelope<Blog>>("blogs/popular");
  }

  public getUserBlogs(login: string) {
    return Api.get<ListEnvelope<Blog>>(`blogs/user/${login}`);
  }
})();
