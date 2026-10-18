import type { ListEnvelope } from "@/shared/api/models/common";
import { Api } from "@/shared/api";

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
  /** The description as typed, the markup included: what the edit window holds. */
  description?: string;
  /** The same description as the server renders it, for the card of the tag. */
  descriptionHtml?: string;
  sortOrder: number;
  gamesCount: number;
};

export type CreateTagGroupRequest = {
  title: string;
  description?: string;
  sortOrder: number;
  maxTagsPerGame?: number | null;
};

export type UpdateTagGroupRequest = {
  title: string;
  description?: string;
  sortOrder: number;
  maxTagsPerGame?: number | null;
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
 * The refusal a caller takes over because its own screen carries it.
 *
 * A screen that answers a failure where the reader is looking — the strip in
 * place of a list, the card in place of a sheet — is the one carrier of that
 * failure, and the response interceptor has to stay quiet or the same sentence
 * stands in two places at once. The methods below pass this through untouched;
 * every call that leaves it out is unchanged, because the client then sees
 * nothing and the interceptor speaks as it always did.
 *
 * Declared here rather than imported: `shared/api` keeps its own copy of the
 * type to itself, and the client reads the two fields off the request by name,
 * so a structural twin carries them just as well.
 */
type RefusalFlags = {
  /** The screen shows a 403 and a 5xx itself. */
  ownsRefusal?: boolean;
  /** The screen shows a 429 itself, the wait included. */
  ownsRateLimit?: boolean;
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
   *
   * @param options Set by a caller whose screen shows the failure itself: the
   * tag section draws a failed load in place of both its panels.
   */
  public getTagGroups(options?: RefusalFlags) {
    return Api.get<ListEnvelope<ModerationTagGroup>>(
      "moderation/tags/groups",
      undefined,
      undefined,
      options,
    );
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
  public createTagGroup(
    request: CreateTagGroupRequest,
    options?: RefusalFlags,
  ) {
    return Api.post<ModerationTagGroup>(
      "moderation/tags/groups",
      request,
      options,
    );
  }

  /**
   * Update a tag group
   */
  public updateTagGroup(
    groupId: string,
    request: UpdateTagGroupRequest,
    options?: RefusalFlags,
  ) {
    return Api.put<ModerationTagGroup>(
      `moderation/tags/groups/${groupId}`,
      request,
      options,
    );
  }

  /**
   * Delete a tag group
   */
  public deleteTagGroup(groupId: string, options?: RefusalFlags) {
    return Api.delete(`moderation/tags/groups/${groupId}`, undefined, options);
  }

  /**
   * Get all tags
   *
   * @param options As in getTagGroups — the tag section reads both at once and
   * shows whichever failure comes back in one place.
   */
  public getTags(options?: RefusalFlags) {
    return Api.get<ListEnvelope<ModerationTag>>(
      "moderation/tags",
      undefined,
      undefined,
      options,
    );
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
  public createTag(request: CreateTagRequest, options?: RefusalFlags) {
    return Api.post<ModerationTag>("moderation/tags", request, options);
  }

  /**
   * Update a tag
   */
  public updateTag(
    tagId: string,
    request: UpdateTagRequest,
    options?: RefusalFlags,
  ) {
    return Api.put<ModerationTag>(`moderation/tags/${tagId}`, request, options);
  }

  /**
   * Delete a tag
   */
  public deleteTag(tagId: string, options?: RefusalFlags) {
    return Api.delete(`moderation/tags/${tagId}`, undefined, options);
  }
})();
