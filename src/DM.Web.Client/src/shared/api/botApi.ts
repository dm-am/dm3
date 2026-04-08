import Api from "./client";
import type {
  BotLinkResult,
  NotificationSettings,
  UpdateNotificationSettingsRequest,
} from "./models/notifications/preferences";

export type BotType = "telegram" | "discord";

export default {
  /**
   * Generate a linking code for a notification bot
   * @param type Bot type: telegram or discord
   */
  generateLinkCode(type: BotType) {
    return Api.post<BotLinkResult>(`users/me/notifications/bots/${type}`);
  },

  /**
   * Disconnect a notification bot
   * @param type Bot type: telegram or discord
   */
  disconnect(type: BotType) {
    return Api.delete(`users/me/notifications/bots/${type}`);
  },

  getSettings() {
    return Api.get<NotificationSettings>("users/me/notifications/settings");
  },

  updateSettings(request: UpdateNotificationSettingsRequest) {
    return Api.patch<NotificationSettings>(
      "users/me/notifications/settings",
      request,
    );
  },
};
