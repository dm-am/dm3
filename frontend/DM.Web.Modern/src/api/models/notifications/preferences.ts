export enum NotificationCategory {
  Messages = 1,
  Forum = 2,
  Games = 3,
  Subscriptions = 4,
  Security = 5,
  Moderation = 6,
}

export const NOTIFICATION_CATEGORY_LABELS: Record<NotificationCategory, string> = {
  [NotificationCategory.Messages]: "Личные сообщения",
  [NotificationCategory.Forum]: "Форум",
  [NotificationCategory.Games]: "Игры",
  [NotificationCategory.Subscriptions]: "Подписки",
  [NotificationCategory.Security]: "Безопасность",
  [NotificationCategory.Moderation]: "Модерация",
};

export interface ChannelPreferences {
  connected: boolean;
  enabled: boolean;
  enabledCategories: NotificationCategory[];
}

export interface NotificationPreferences {
  discord: ChannelPreferences | null;
  telegram: ChannelPreferences | null;
}

export interface BotLinkResult {
  code: string;
  expiresAt: string;
}

export interface UpdateChannelPreferences {
  enabled?: boolean;
  enabledCategories?: NotificationCategory[];
}

export interface UpdatePreferencesRequest {
  discord?: UpdateChannelPreferences;
  telegram?: UpdateChannelPreferences;
}
