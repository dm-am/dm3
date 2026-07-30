import type { ListEnvelope } from "@/shared/api/models/common";
import { Api } from "@/shared/api";

export type ModerationTagGroup = {
  id: string;
  title: string;
  description?: string;
  sortOrder: number;
  tagsCount: number;
};

export type ModerationTag = {
  id: string;
  shortId: number;
  groupId: string;
  groupTitle: string;
  title: string;
  description?: string;
  sortOrder: number;
  gamesCount: number;
};

export type CreateTagGroupRequest = {
  title: string;
  description?: string;
  sortOrder: number;
};

export type UpdateTagGroupRequest = {
  title: string;
  description?: string;
  sortOrder: number;
};

export type CreateTagRequest = {
  groupId: string;
  title: string;
  description?: string;
  sortOrder: number;
};

export type UpdateTagRequest = {
  groupId: string;
  title: string;
  description?: string;
  sortOrder: number;
};

/**
 * Administration of the tag catalog games are classified by.
 *
 * The endpoints sit under `moderation/tags` because editing the catalog is a
 * staff privilege, but a tag is game vocabulary — `gameApi` reads it and games
 * carry it — so the client belongs to this slice rather than to moderation.
 */
export default new (class GameTagApi {
  /**
   * Get all tag groups
   */
  public getTagGroups() {
    return Api.get<ListEnvelope<ModerationTagGroup>>("moderation/tags/groups");
  }

  /**
   * Get a tag group by ID
   */
  public getTagGroup(groupId: string) {
    return Api.get<ModerationTagGroup>(`moderation/tags/groups/${groupId}`);
  }

  /**
   * Create a new tag group
   */
  public createTagGroup(request: CreateTagGroupRequest) {
    return Api.post<ModerationTagGroup>("moderation/tags/groups", request);
  }

  /**
   * Update a tag group
   */
  public updateTagGroup(groupId: string, request: UpdateTagGroupRequest) {
    return Api.put<ModerationTagGroup>(
      `moderation/tags/groups/${groupId}`,
      request,
    );
  }

  /**
   * Delete a tag group
   */
  public deleteTagGroup(groupId: string) {
    return Api.delete(`moderation/tags/groups/${groupId}`);
  }

  /**
   * Get all tags
   */
  public getTags() {
    return Api.get<ListEnvelope<ModerationTag>>("moderation/tags");
  }

  /**
   * Get a tag by ID
   */
  public getTag(tagId: string) {
    return Api.get<ModerationTag>(`moderation/tags/${tagId}`);
  }

  /**
   * Create a new tag
   */
  public createTag(request: CreateTagRequest) {
    return Api.post<ModerationTag>("moderation/tags", request);
  }

  /**
   * Update a tag
   */
  public updateTag(tagId: string, request: UpdateTagRequest) {
    return Api.put<ModerationTag>(`moderation/tags/${tagId}`, request);
  }

  /**
   * Delete a tag
   */
  public deleteTag(tagId: string) {
    return Api.delete(`moderation/tags/${tagId}`);
  }
})();
