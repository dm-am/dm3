import type { Envelope } from "@/api/models/common";
import type { ModerationProfile } from "@/api/models/moderation";
import type { UserLogin } from "@/api/models/community";
import Api from "@/api";

export default new (class ModerationApi {
  public getModerationProfile(login: UserLogin) {
    return Api.get<Envelope<ModerationProfile>>(
      `moderation/users/${login}/profile`,
    );
  }

  public createModNote(login: UserLogin, text: string) {
    return Api.post<Envelope<ProfileModNote>>(
      `moderation/users/${login}/notes`,
      { text },
    );
  }

  public updateModNote(noteId: string, text: string) {
    return Api.put<Envelope<ProfileModNote>>(
      `moderation/notes/${noteId}`,
      { text },
    );
  }

  public deleteModNote(noteId: string) {
    return Api.delete(`moderation/notes/${noteId}`);
  }

  public getWarnings(login: UserLogin) {
    return Api.get<Envelope<UserWarningsInfo>>(`users/${login}/warnings`);
  }

  public createWarning(warning: CreateWarning) {
    return Api.post<Envelope<Warning>>("warnings", warning);
  }

  public removeWarning(warningId: string) {
    return Api.delete(`warnings/${warningId}`);
  }

  public getBans(login: UserLogin) {
    return Api.get<Envelope<UserBanStatus>>(`users/${login}/bans`);
  }

  public createBan(ban: CreateBan) {
    return Api.post<Envelope<Ban>>("bans", ban);
  }

  public liftBan(banId: string) {
    return Api.delete(`bans/${banId}`);
  }
})();

export type ProfileModNote = {
  id: string;
  text: string;
  author?: { id: string; login: string };
  createdAtUtc: string;
  modifiedAtUtc?: string;
};

export type Warning = {
  id: string;
  user?: { login: string };
  moderator?: { login: string };
  entityId?: string;
  entityType?: string;
  points: number;
  reason: string;
  createdUtc: string;
  isActive: boolean;
};

export type CreateWarning = {
  userLogin: string;
  points: number;
  reason: string;
  entityId?: string;
  entityType?: string;
};

export type Ban = {
  id: string;
  user?: { login: string };
  moderator?: { login: string };
  type: BanType;
  startedUtc: string;
  expiresUtc?: string;
  comment: string;
  isActive: boolean;
  liftedUtc?: string;
};

export type CreateBan = {
  userLogin: string;
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
  login: string;
  totalPoints: number;
  activeCount: number;
  warnings: Warning[];
};

export type UserBanStatus = {
  login: string;
  isBanned: boolean;
  activeBan?: Ban;
  history: Ban[];
};
