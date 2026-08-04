<template>
  <section class="section">
    <h2 class="section-title">Черный список</h2>

    <div class="blacklist-content">
      <!-- Settings -->
      <div class="settings-group">
        <h3 class="settings-title">Настройки</h3>
        <div v-if="settingsLoading" class="loading-state">Загрузка...</div>
        <div v-else class="settings-list">
          <label class="setting-item">
            <input
              type="checkbox"
              v-model="settings.hideComments"
              @change="saveSettings"
              :disabled="settingsSaving"
            />
            <span>Скрывать комментарии</span>
          </label>
          <label class="setting-item">
            <input
              type="checkbox"
              v-model="settings.hideMessages"
              @change="saveSettings"
              :disabled="settingsSaving"
            />
            <span>Скрывать сообщения в чате</span>
          </label>
          <label class="setting-item">
            <input
              type="checkbox"
              v-model="settings.hideGames"
              @change="saveSettings"
              :disabled="settingsSaving"
            />
            <span>Скрывать игры</span>
          </label>
          <label class="setting-item">
            <input
              type="checkbox"
              v-model="settings.hideBlogs"
              @change="saveSettings"
              :disabled="settingsSaving"
            />
            <span>Скрывать блоги</span>
          </label>
          <label class="setting-item">
            <input
              type="checkbox"
              v-model="settings.blockDirectMessages"
              @change="saveSettings"
              :disabled="settingsSaving"
            />
            <span>Блокировать личные сообщения</span>
          </label>
          <label class="setting-item">
            <input
              type="checkbox"
              v-model="settings.autoPopulateContentBlacklist"
              @change="saveSettings"
              :disabled="settingsSaving"
            />
            <span>Автозаполнять ЧС игр и блогов</span>
          </label>
        </div>
      </div>

      <!-- Blocked users list -->
      <div class="blocked-group">
        <h3 class="settings-title">Заблокированные</h3>
        <div v-if="listLoading" class="loading-state">Загрузка...</div>
        <EmptyState
          v-else-if="blockedUsers.length === 0"
          title="Нет заблокированных пользователей"
        />
        <div v-else class="blocked-list">
          <div
            v-for="entry in blockedUsers"
            :key="entry.id"
            class="blocked-item"
          >
            <div class="blocked-info">
              <RouterLink
                :to="`/users/${entry.username}`"
                class="blocked-username"
              >
                {{ entry.username }}
              </RouterLink>
              <span class="blocked-date">{{
                formatDate(entry.createdUtc)
              }}</span>
            </div>
            <button
              class="unblock-btn"
              :disabled="unblockingUsername === entry.username"
              @click="pendingUnblock = entry.username"
            >
              {{
                unblockingUsername === entry.username ? "..." : "Разблокировать"
              }}
            </button>
          </div>
        </div>

        <button class="add-btn" @click="openBlockModal">+ Заблокировать</button>
      </div>
    </div>

    <ConfirmDialog
      :show="pendingUnblock !== null"
      title="Разблокировка пользователя"
      :message="`Разблокировать ${pendingUnblock ?? ''}?`"
      confirm-label="Разблокировать"
      :loading="unblockingUsername !== null"
      @update:show="(v) => !v && (pendingUnblock = null)"
      @confirm="confirmUnblock"
    />
  </section>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from "vue";
import { RouterLink } from "vue-router";
import { useModal } from "vue-final-modal";
import { blacklistApi } from "@/entities/user";
import { useToast } from "@/shared/lib/composables/useToast";
import { EmptyState } from "@/shared/ui";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import { formatDate } from "@/shared/lib/utils/datetime";
import { BlockUserDialog } from "@/features/block-user";
import type {
  BlacklistEntry,
  BlacklistSettings,
} from "@/shared/api/models/personal";
import { notifyFailure } from "@/shared/lib/errors";

const toast = useToast();

// Settings state
const settingsLoading = ref(true);
const settingsSaving = ref(false);
const settings = reactive<BlacklistSettings>({
  hideComments: false,
  hideMessages: false,
  hideGames: false,
  hideBlogs: false,
  blockDirectMessages: false,
  autoPopulateContentBlacklist: false,
});

// Blocked users state
const listLoading = ref(true);
const blockedUsers = ref<BlacklistEntry[]>([]);
const unblockingUsername = ref<string | null>(null);

// Block modal
const { open: openBlockModal, close: closeBlockModal } = useModal({
  component: BlockUserDialog,
  attrs: {
    onSuccess: (entry: BlacklistEntry) => {
      blockedUsers.value.unshift(entry);
      closeBlockModal();
      toast.success(`${entry.username} заблокирован`);
    },
    onCancel: () => closeBlockModal(),
  },
});

onMounted(async () => {
  await Promise.all([loadSettings(), loadBlockedUsers()]);
});

async function loadSettings() {
  settingsLoading.value = true;
  const { data, error } = await blacklistApi.getSettings();
  settingsLoading.value = false;

  if (!error && data) {
    Object.assign(settings, data);
  }
}

async function saveSettings() {
  settingsSaving.value = true;
  const { error } = await blacklistApi.updateSettings({ ...settings });
  settingsSaving.value = false;

  if (error) {
    notifyFailure(error, "Не удалось сохранить настройки");
    // Reload to restore correct state
    await loadSettings();
  }
}

async function loadBlockedUsers() {
  listLoading.value = true;
  const { data, error } = await blacklistApi.getBlacklist();
  listLoading.value = false;

  if (!error && data) {
    blockedUsers.value = data.resources;
  }
}

// Unblock is ConfirmDialog-gated: `pendingUnblock` holds the username the
// dialog is asking about.
const pendingUnblock = ref<string | null>(null);

async function confirmUnblock() {
  const username = pendingUnblock.value;
  if (!username || unblockingUsername.value) return;

  unblockingUsername.value = username;
  const { error } = await blacklistApi.unblockUser(username);
  unblockingUsername.value = null;

  if (error) {
    notifyFailure(error, "Не удалось разблокировать пользователя");
  } else {
    pendingUnblock.value = null;
    blockedUsers.value = blockedUsers.value.filter(
      (u) => u.username !== username,
    );
    toast.success(`${username} разблокирован`);
  }
}
</script>

<style scoped lang="sass">
@import "@/assets/styles/Inputs"
@import "../AccountPage.styles"

.blacklist-content
  padding: $medium
  background-color: $bg-element
  border-radius: $border-radius
  display: flex
  flex-direction: column
  gap: $large

.settings-group,
.blocked-group
  display: flex
  flex-direction: column
  gap: $small

.settings-title
  margin: 0
  font-size: $font-size
  font-weight: 600
  color: $text

.loading-state
  color: $text-muted
  padding: $small 0

.settings-list
  display: flex
  flex-direction: column
  gap: $small

.setting-item
  display: flex
  align-items: center
  gap: $small
  cursor: pointer
  color: $text

  &:has(input:disabled)
    opacity: 0.6
    cursor: default

.blocked-list
  display: flex
  flex-direction: column
  gap: $small

.blocked-item
  display: flex
  justify-content: space-between
  align-items: center
  padding: $small $medium
  background-color: $bg
  border-radius: $border-radius
  border: 1px solid $border
  gap: $medium

  @media (max-width: $bp-tablet)
    flex-direction: column
    align-items: stretch

.blocked-info
  display: flex
  align-items: center
  gap: $small
  flex: 1
  min-width: 0

.blocked-username
  font-weight: 500
  color: $link
  text-decoration: none

  &:hover
    text-decoration: underline

.blocked-date
  font-size: $secondary-font-size
  color: $text-muted

.unblock-btn
  background: none
  border: 1px solid $border
  color: $text-muted
  padding: $tiny $small
  border-radius: $border-radius
  cursor: pointer
  font-size: $secondary-font-size
  flex-shrink: 0

  &:hover:not(:disabled)
    +tint($text-muted, 15%)

  &:disabled
    opacity: 0.5
    cursor: default

  @media (max-width: $bp-tablet)
    align-self: flex-end
    margin-top: $small

.add-btn
  align-self: flex-start
  +button
</style>
