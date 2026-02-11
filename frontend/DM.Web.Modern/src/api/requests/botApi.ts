import Api from "@/api";
import type { Envelope } from "@/api/models/common";
import type {
  BotLinkResult,
  NotificationPreferences,
  UpdatePreferencesRequest,
} from "@/api/models/notifications/preferences";

export default {
  generateTelegramCode() {
    return Api.post<Envelope<BotLinkResult>>("account/bot-link/telegram");
  },

  generateDiscordCode() {
    return Api.post<Envelope<BotLinkResult>>("account/bot-link/discord");
  },

  disconnectTelegram() {
    return Api.delete("account/bot-link/telegram");
  },

  disconnectDiscord() {
    return Api.delete("account/bot-link/discord");
  },

  getPreferences() {
    return Api.get<Envelope<NotificationPreferences>>(
      "account/notification-preferences",
    );
  },

  updatePreferences(request: UpdatePreferencesRequest) {
    return Api.patch<Envelope<NotificationPreferences>>(
      "account/notification-preferences",
      request,
    );
  },
};
