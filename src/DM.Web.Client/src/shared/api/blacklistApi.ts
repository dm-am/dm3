import type { ListEnvelope } from "./models/common";
import type { BlacklistEntry, BlockUserRequest, BlacklistSettings } from "./models/personal";
import Api from "./client";

export default new (class BlacklistApi {
  /**
   * Get current user's blacklist
   * @param skip Number of items to skip (default 0)
   * @param take Number of items to take (default 20, max 100)
   */
  public getBlacklist(skip = 0, take = 50) {
    return Api.get<ListEnvelope<BlacklistEntry>>(`users/me/blacklist?skip=${skip}&take=${take}`);
  }

  /**
   * Get blacklist behavior settings
   */
  public getSettings() {
    return Api.get<BlacklistSettings>("users/me/blacklist/settings");
  }

  /**
   * Update blacklist behavior settings
   */
  public updateSettings(settings: BlacklistSettings) {
    return Api.patch<BlacklistSettings>("users/me/blacklist/settings", settings);
  }

  /**
   * Block a user
   */
  public blockUser(request: BlockUserRequest) {
    return Api.post<BlacklistEntry>("users/me/blacklist", request);
  }

  /**
   * Unblock a user
   */
  public unblockUser(username: string) {
    return Api.delete(`users/me/blacklist/${encodeURIComponent(username)}`);
  }
})();
