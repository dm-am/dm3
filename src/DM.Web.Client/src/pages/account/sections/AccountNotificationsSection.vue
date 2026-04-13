<template>
  <section class="section">
    <h2 class="section-title">Уведомления</h2>

    <div class="notifications-content">
      <div v-if="loading" class="loading-state">Загрузка...</div>

      <template v-else>
        <!-- Telegram Channel -->
        <div class="channel-card">
          <div class="channel-header">
            <span class="channel-name">Telegram</span>
            <span
              v-if="preferences.telegram?.connected"
              class="channel-status channel-status--connected"
            >
              Подключен
            </span>
            <span v-else class="channel-status channel-status--disconnected">
              Не подключен
            </span>
          </div>

          <template v-if="preferences.telegram?.connected">
            <div class="channel-settings">
              <label class="setting-toggle">
                <input
                  type="checkbox"
                  v-model="telegramEnabled"
                  @change="updateTelegramEnabled"
                  :disabled="saving"
                />
                <span>Включить уведомления</span>
              </label>

              <div v-if="telegramEnabled" class="categories-group">
                <div class="categories-title">Категории:</div>
                <div class="categories-list">
                  <label
                    v-for="cat in allCategories"
                    :key="cat.value"
                    class="category-item"
                  >
                    <input
                      type="checkbox"
                      :checked="telegramCategories.includes(cat.value)"
                      @change="toggleTelegramCategory(cat.value)"
                      :disabled="saving"
                    />
                    <span>{{ cat.label }}</span>
                  </label>
                </div>
              </div>
            </div>
          </template>

          <div class="channel-actions">
            <button
              v-if="!preferences.telegram?.connected"
              class="action-btn action-btn--connect"
              @click="$emit('connectTelegram')"
            >
              Подключить
            </button>
            <button
              v-else
              class="action-btn action-btn--disconnect"
              :disabled="disconnecting === 'telegram'"
              @click="disconnectTelegram"
            >
              {{ disconnecting === "telegram" ? "..." : "Отключить" }}
            </button>
          </div>
        </div>

        <!-- Discord Channel -->
        <div class="channel-card">
          <div class="channel-header">
            <span class="channel-name">Discord</span>
            <span
              v-if="preferences.discord?.connected"
              class="channel-status channel-status--connected"
            >
              Подключен
            </span>
            <span v-else class="channel-status channel-status--disconnected">
              Не подключен
            </span>
          </div>

          <template v-if="preferences.discord?.connected">
            <div class="channel-settings">
              <label class="setting-toggle">
                <input
                  type="checkbox"
                  v-model="discordEnabled"
                  @change="updateDiscordEnabled"
                  :disabled="saving"
                />
                <span>Включить уведомления</span>
              </label>

              <div v-if="discordEnabled" class="categories-group">
                <div class="categories-title">Категории:</div>
                <div class="categories-list">
                  <label
                    v-for="cat in allCategories"
                    :key="cat.value"
                    class="category-item"
                  >
                    <input
                      type="checkbox"
                      :checked="discordCategories.includes(cat.value)"
                      @change="toggleDiscordCategory(cat.value)"
                      :disabled="saving"
                    />
                    <span>{{ cat.label }}</span>
                  </label>
                </div>
              </div>
            </div>
          </template>

          <div class="channel-actions">
            <button
              v-if="!preferences.discord?.connected"
              class="action-btn action-btn--connect"
              @click="$emit('connectDiscord')"
            >
              Подключить
            </button>
            <button
              v-else
              class="action-btn action-btn--disconnect"
              :disabled="disconnecting === 'discord'"
              @click="disconnectDiscord"
            >
              {{ disconnecting === "discord" ? "..." : "Отключить" }}
            </button>
          </div>
        </div>
      </template>
    </div>
  </section>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from "vue";
import { AccountApi } from "@/shared/api";
import { useToast } from "@/shared/lib/composables/useToast";
import type {
  NotificationPreferences,
  NotificationCategory,
} from "@/shared/api/models/account";

const emit = defineEmits<{
  (e: "connectTelegram"): void;
  (e: "connectDiscord"): void;
  (e: "refreshPreferences"): void;
}>();

const toast = useToast();

const loading = ref(true);
const saving = ref(false);
const disconnecting = ref<"telegram" | "discord" | null>(null);

const preferences = reactive<NotificationPreferences>({
  telegram: undefined,
  discord: undefined,
});

const allCategories: { value: NotificationCategory; label: string }[] = [
  { value: "Messages", label: "Личные сообщения" },
  { value: "Forum", label: "Форум" },
  { value: "Games", label: "Игры" },
  { value: "Subscriptions", label: "Подписки" },
  { value: "Security", label: "Безопасность" },
  { value: "Moderation", label: "Модерация" },
];

// Local state for toggles
const telegramEnabled = ref(false);
const telegramCategories = ref<NotificationCategory[]>([]);
const discordEnabled = ref(false);
const discordCategories = ref<NotificationCategory[]>([]);

onMounted(async () => {
  await loadPreferences();
});

async function loadPreferences() {
  loading.value = true;
  const { data, error } = await AccountApi.getNotificationPreferences();
  loading.value = false;

  if (!error && data) {
    preferences.telegram = data.telegram;
    preferences.discord = data.discord;

    // Sync local state
    telegramEnabled.value = preferences.telegram?.enabled ?? false;
    telegramCategories.value = preferences.telegram?.enabledCategories ?? [];
    discordEnabled.value = preferences.discord?.enabled ?? false;
    discordCategories.value = preferences.discord?.enabledCategories ?? [];
  }
}

async function updateTelegramEnabled() {
  saving.value = true;
  const { error } = await AccountApi.updateNotificationPreferences({
    telegram: { enabled: telegramEnabled.value },
  });
  saving.value = false;

  if (error) {
    toast.error("Не удалось сохранить настройки");
    telegramEnabled.value = !telegramEnabled.value;
  }
}

async function toggleTelegramCategory(category: NotificationCategory) {
  const idx = telegramCategories.value.indexOf(category);
  if (idx >= 0) {
    telegramCategories.value.splice(idx, 1);
  } else {
    telegramCategories.value.push(category);
  }

  saving.value = true;
  const { error } = await AccountApi.updateNotificationPreferences({
    telegram: { enabledCategories: [...telegramCategories.value] },
  });
  saving.value = false;

  if (error) {
    toast.error("Не удалось сохранить настройки");
    // Revert
    if (idx >= 0) {
      telegramCategories.value.push(category);
    } else {
      telegramCategories.value.splice(
        telegramCategories.value.indexOf(category),
        1,
      );
    }
  }
}

async function updateDiscordEnabled() {
  saving.value = true;
  const { error } = await AccountApi.updateNotificationPreferences({
    discord: { enabled: discordEnabled.value },
  });
  saving.value = false;

  if (error) {
    toast.error("Не удалось сохранить настройки");
    discordEnabled.value = !discordEnabled.value;
  }
}

async function toggleDiscordCategory(category: NotificationCategory) {
  const idx = discordCategories.value.indexOf(category);
  if (idx >= 0) {
    discordCategories.value.splice(idx, 1);
  } else {
    discordCategories.value.push(category);
  }

  saving.value = true;
  const { error } = await AccountApi.updateNotificationPreferences({
    discord: { enabledCategories: [...discordCategories.value] },
  });
  saving.value = false;

  if (error) {
    toast.error("Не удалось сохранить настройки");
    // Revert
    if (idx >= 0) {
      discordCategories.value.push(category);
    } else {
      discordCategories.value.splice(
        discordCategories.value.indexOf(category),
        1,
      );
    }
  }
}

async function disconnectTelegram() {
  disconnecting.value = "telegram";
  const { error } = await AccountApi.disconnectBot("telegram");
  disconnecting.value = null;

  if (error) {
    toast.error("Не удалось отключить Telegram");
  } else {
    preferences.telegram = undefined;
    toast.success("Telegram отключен");
  }
}

async function disconnectDiscord() {
  disconnecting.value = "discord";
  const { error } = await AccountApi.disconnectBot("discord");
  disconnecting.value = null;

  if (error) {
    toast.error("Не удалось отключить Discord");
  } else {
    preferences.discord = undefined;
    toast.success("Discord отключен");
  }
}

// Expose reload for parent component
defineExpose({ loadPreferences });
</script>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"
@import "../AccountPage.styles"

.notifications-content
  padding: $medium
  background-color: $bg-element
  border-radius: $border-radius
  display: flex
  flex-direction: column
  gap: $medium

.loading-state
  color: $text-muted
  text-align: center
  padding: $medium

.channel-card
  padding: $medium
  background-color: $bg
  border-radius: $border-radius
  border: 1px solid $border
  display: flex
  flex-direction: column
  gap: $small

.channel-header
  display: flex
  align-items: center
  gap: $small

.channel-icon
  font-size: 1.3em

.channel-name
  font-weight: 600
  color: $text
  flex: 1

.channel-status
  font-size: $secondary-font-size
  padding: 2px $tiny
  border-radius: 3px

  &--connected
    color: $accent-green
    background-color: rgba($accent-green, 0.1)

  &--disconnected
    color: $text-muted
    background-color: rgba($text-muted, 0.1)

.channel-settings
  display: flex
  flex-direction: column
  gap: $small
  padding-left: $medium

.setting-toggle
  display: flex
  align-items: center
  gap: $small
  cursor: pointer
  color: $text

  input[type="checkbox"]
    width: 16px
    height: 16px
    accent-color: $link

  &:has(input:disabled)
    opacity: 0.6
    cursor: not-allowed

.categories-group
  margin-top: $small
  padding-left: $medium

.categories-title
  font-size: $secondary-font-size
  color: $text-muted
  margin-bottom: $tiny

.categories-list
  display: flex
  flex-direction: column
  gap: $tiny

.category-item
  display: flex
  align-items: center
  gap: $small
  cursor: pointer
  font-size: $secondary-font-size
  color: $text

  input[type="checkbox"]
    width: 14px
    height: 14px
    accent-color: $link

  &:has(input:disabled)
    opacity: 0.6
    cursor: not-allowed

.channel-actions
  margin-top: $small

.action-btn
  background: none
  padding: $tiny $small
  border-radius: $border-radius
  cursor: pointer
  font-size: $secondary-font-size
  transition: opacity 0.15s ease

  &:disabled
    opacity: 0.5
    cursor: not-allowed

  &--connect
    border: 1px solid $link
    color: $link

    &:hover:not(:disabled)
      background-color: rgba($link, 0.1)

  &--disconnect
    border: 1px solid $border
    color: $text-muted

    &:hover:not(:disabled)
      background-color: rgba($text-muted, 0.1)
</style>
