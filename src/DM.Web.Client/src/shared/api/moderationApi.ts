import type { ModeratedProfile } from "./models/moderation";
import type { Username } from "./models/community";
import type { ListEnvelope, Envelope } from "./models/common";
import Api from "./client";

// ==================== Tag Management Types ====================

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

export default new (class ModerationApi {
  public getModeratedProfile(username: Username) {
    return Api.get<ModeratedProfile>(`moderation/users/${username}/profile`);
  }

  // ==================== Username Change Requests ====================

  /**
   * Get all pending username change requests
   */
  public getPendingUsernameChangeRequests() {
    return Api.get<ListEnvelope<UsernameChangeRequest>>(
      "moderation/username-changes",
    );
  }

  /**
   * Get a specific username change request
   */
  public getUsernameChangeRequest(id: string) {
    return Api.get<Envelope<UsernameChangeRequest>>(
      `moderation/username-changes/${id}`,
    );
  }

  /**
   * Resolve (approve/reject) a username change request
   */
  public resolveUsernameChangeRequest(
    id: string,
    resolve: ResolveUsernameChangeRequest,
  ) {
    return Api.patch<Envelope<UsernameChangeRequest>>(
      `moderation/username-changes/${id}`,
      resolve,
    );
  }

  public createModNote(username: Username, text: string) {
    return Api.post<ModeratedProfileNote>(
      `moderation/users/${username}/notes`,
      { text },
    );
  }

  public updateModNote(noteId: string, text: string) {
    return Api.put<ModeratedProfileNote>(`moderation/notes/${noteId}`, {
      text,
    });
  }

  public deleteModNote(noteId: string) {
    return Api.delete(`moderation/notes/${noteId}`);
  }

  public getWarnings(username: Username) {
    return Api.get<UserWarningsInfo>(`users/${username}/warnings`);
  }

  public createWarning(warning: CreateWarning) {
    return Api.post<Warning>("warnings", warning);
  }

  public removeWarning(warningId: string) {
    return Api.delete(`warnings/${warningId}`);
  }

  public getBans(username: Username) {
    return Api.get<UserBanStatus>(`users/${username}/bans`);
  }

  public createBan(ban: CreateBan) {
    return Api.post<Ban>("bans", ban);
  }

  public liftBan(banId: string) {
    return Api.delete(`bans/${banId}`);
  }

  // ==================== Tag Management ====================

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
   * Get tags by group ID
   */
  public getTagsByGroup(groupId: string) {
    return Api.get<ListEnvelope<ModerationTag>>(
      `moderation/tags/groups/${groupId}/tags`,
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

export type ModeratedProfileNote = {
  id: string;
  text: string;
  author?: { id: string; username: string };
  createdUtc: string;
  modifiedUtc?: string;
};

export type Warning = {
  id: string;
  user?: { username: string };
  moderator?: { username: string };
  entityId?: string;
  entityType?: string;
  points: number;
  reason: string;
  createdUtc: string;
  isActive: boolean;
};

export type CreateWarning = {
  username: string;
  points: number;
  reason: string;
  entityId?: string;
  entityType?: string;
};

export type Ban = {
  id: string;
  user?: { username: string };
  moderator?: { username: string };
  type: BanType;
  startedUtc: string;
  expiresUtc?: string;
  comment: string;
  isActive: boolean;
  liftedUtc?: string;
};

export type CreateBan = {
  username: string;
  type?: BanType;
  expiresUtc?: string;
  durationHours?: number;
  comment: string;
};

export enum BanType {
  Auto = "Auto",
  Temporary = "Temporary",
  Permanent = "Permanent",
  Voluntary = "Voluntary",
}

export type UserWarningsInfo = {
  username: string;
  totalPoints: number;
  activeCount: number;
  warnings: Warning[];
};

export type UserBanStatus = {
  username: string;
  isBanned: boolean;
  activeBan?: Ban;
  history: Ban[];
};

// ==================== Username Change Request Types ====================

export enum UsernameChangeRequestStatus {
  Pending = 0,
  Approved = 1,
  Rejected = 2,
  Completed = 3,
  Expired = 4,
}

export type UsernameChangeRequest = {
  id: string;
  userId: string;
  currentUsername: string;
  requestedUsername?: string | null;
  reason: string;
  status: UsernameChangeRequestStatus;
  createdUtc: string;
  approvalExpiresUtc?: string | null;
  resolvedUtc?: string | null;
  resolvedBy?: string | null;
  comment?: string | null;
};

export type ResolveUsernameChangeRequest = {
  status: UsernameChangeRequestStatus;
  comment?: string | null;
};
