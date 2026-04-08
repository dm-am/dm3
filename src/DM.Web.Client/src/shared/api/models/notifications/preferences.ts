export enum NotificationCategory {
  Messages = 1,
  Forum = 2,
  Games = 3,
  Subscriptions = 4,
  Security = 5,
  Moderation = 6,
}

export const NOTIFICATION_CATEGORY_LABELS: Record<
  NotificationCategory,
  string
> = {
  [NotificationCategory.Messages]: "Личные сообщения",
  [NotificationCategory.Forum]: "Форум",
  [NotificationCategory.Games]: "Игры",
  [NotificationCategory.Subscriptions]: "Подписки",
  [NotificationCategory.Security]: "Безопасность",
  [NotificationCategory.Moderation]: "Модерация",
};

export interface BotConnection {
  connected: boolean;
  enabled: boolean;
  enabledCategories: NotificationCategory[];
}

export interface NotificationSettings {
  discord: BotConnection | null;
  telegram: BotConnection | null;
}

export interface BotLinkResult {
  code: string;
  expiresUtc: string;
}

export interface UpdateBotConnection {
  enabled?: boolean;
  enabledCategories?: NotificationCategory[];
}

export interface UpdateNotificationSettingsRequest {
  discord?: UpdateBotConnection;
  telegram?: UpdateBotConnection;
}

// Backwards compatibility aliases
export type ChannelPreferences = BotConnection;
export type NotificationPreferences = NotificationSettings;
export type UpdateChannelPreferences = UpdateBotConnection;
export type UpdatePreferencesRequest = UpdateNotificationSettingsRequest;
