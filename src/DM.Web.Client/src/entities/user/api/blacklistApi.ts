import type { ListEnvelope } from "@/shared/api/models/common";
import type {
  BlacklistEntry,
  BlockUserRequest,
  BlacklistSettings,
} from "@/shared/api/models/personal";
import { Api } from "@/shared/api";
import type { RequestOptions } from "@/shared/api/interceptor";

export default new (class BlacklistApi {
  public getBlacklist(skip = 0, take = 50) {
    return Api.get<ListEnvelope<BlacklistEntry>>(
      `users/me/blacklist?skip=${skip}&take=${take}`,
    );
  }

  public getSettings(options?: RequestOptions) {
    return Api.get<BlacklistSettings>(
      "users/me/blacklist/settings",
      undefined,
      undefined,
      options,
    );
  }

  public updateSettings(settings: BlacklistSettings) {
    return Api.patch<BlacklistSettings>(
      "users/me/blacklist/settings",
      settings,
    );
  }

  public blockUser(request: BlockUserRequest) {
    return Api.post<BlacklistEntry>("users/me/blacklist", request);
  }

  public unblockUser(username: string) {
    return Api.delete(`users/me/blacklist/${encodeURIComponent(username)}`);
  }
})();
