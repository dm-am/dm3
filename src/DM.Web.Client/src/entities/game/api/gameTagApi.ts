import type { ListEnvelope } from "@/shared/api/models/common";
import { Api, RENDER_AUDIENCE } from "@/shared/api";
import type { RequestOptions } from "@/shared/api/interceptor";

export type ModerationTagGroup = {
  id: string;
  title: string;
  description?: string;
  sortOrder: number;
  /** How many tags of the group one game may carry; absent means no limit. */
  maxTagsPerGame?: number | null;
  tagsCount: number;
};

export type ModerationTag = {
  id: string;
  shortId: number;
  groupId: string;
  groupTitle: string;
  title: string;
  /**
   * Rendered, for the card of the tag; as typed on the author_edit audience,
   * which the edit window reads.
   */
  description?: string;
  sortOrder: number;
  gamesCount: number;
};

/** For both the creation and the replacement of a group. */
type TagGroupRequest = {
  title: string;
  description?: string;
  sortOrder: number;
  maxTagsPerGame?: number | null;
};

/** For both the creation and the replacement of a tag. */
type TagRequest = {
  groupId: string;
  title: string;
  description?: string;
  sortOrder: number;
};

// Written under moderation/, but a tag is game vocabulary, so the client lives here.
export default new (class GameTagApi {
  public getTagGroups(options?: RequestOptions) {
    return Api.get<ListEnvelope<ModerationTagGroup>>(
      "moderation/tags/groups",
      undefined,
      undefined,
      options,
    );
  }

  public createTagGroup(request: TagGroupRequest, options?: RequestOptions) {
    return Api.post<ModerationTagGroup>(
      "moderation/tags/groups",
      request,
      options,
    );
  }

  public updateTagGroup(
    groupId: string,
    request: TagGroupRequest,
    options?: RequestOptions,
  ) {
    return Api.put<ModerationTagGroup>(
      `moderation/tags/groups/${groupId}`,
      request,
      options,
    );
  }

  public deleteTagGroup(groupId: string, options?: RequestOptions) {
    return Api.delete(`moderation/tags/groups/${groupId}`, undefined, options);
  }

  public getTags(options?: RequestOptions) {
    return Api.get<ListEnvelope<ModerationTag>>(
      "moderation/tags",
      undefined,
      undefined,
      options,
    );
  }

  /** The tag with its description as typed, for the edit window. */
  public getTagForEdit(tagId: string) {
    return Api.get<ModerationTag>(
      `moderation/tags/${tagId}`,
      undefined,
      RENDER_AUDIENCE.AuthorEdit,
    );
  }

  public createTag(request: TagRequest, options?: RequestOptions) {
    return Api.post<ModerationTag>("moderation/tags", request, options);
  }

  public updateTag(
    tagId: string,
    request: TagRequest,
    options?: RequestOptions,
  ) {
    return Api.put<ModerationTag>(`moderation/tags/${tagId}`, request, options);
  }

  public deleteTag(tagId: string, options?: RequestOptions) {
    return Api.delete(`moderation/tags/${tagId}`, undefined, options);
  }
})();
