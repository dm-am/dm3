<template>
  <section class="section">
    <h2 class="section-title">Настройки</h2>

    <div class="settings-content">
      <div class="form-group">
        <label for="color-schema" class="form-label">Цветовая схема</label>
        <select
          id="color-schema"
          v-model="settingsForm.theme"
          class="form-select"
        >
          <option value="Light">Светлая</option>
          <option value="Dark">Темная</option>
        </select>
      </div>

      <div class="form-group">
        <label class="form-label">Pagination настройки</label>
        <div class="pagination-grid">
          <div class="pagination-item">
            <label for="posts-per-page" class="pagination-label"
              >Постов на странице</label
            >
            <select
              id="posts-per-page"
              v-model.number="settingsForm.paging.postsPerPage"
              class="form-select"
            >
              <option v-for="opt in pagingOptions" :key="opt" :value="opt">
                {{ opt }}
              </option>
            </select>
          </div>

          <div class="pagination-item">
            <label for="comments-per-page" class="pagination-label"
              >Комментариев на странице</label
            >
            <select
              id="comments-per-page"
              v-model.number="settingsForm.paging.commentsPerPage"
              class="form-select"
            >
              <option v-for="opt in pagingOptions" :key="opt" :value="opt">
                {{ opt }}
              </option>
            </select>
          </div>

          <div class="pagination-item">
            <label for="topics-per-page" class="pagination-label"
              >Тем на странице</label
            >
            <select
              id="topics-per-page"
              v-model.number="settingsForm.paging.topicsPerPage"
              class="form-select"
            >
              <option v-for="opt in pagingOptions" :key="opt" :value="opt">
                {{ opt }}
              </option>
            </select>
          </div>

          <div class="pagination-item">
            <label for="messages-per-page" class="pagination-label"
              >Сообщений на странице</label
            >
            <select
              id="messages-per-page"
              v-model.number="settingsForm.paging.messagesPerPage"
              class="form-select"
            >
              <option v-for="opt in pagingOptions" :key="opt" :value="opt">
                {{ opt }}
              </option>
            </select>
          </div>

          <div class="pagination-item">
            <label for="entities-per-page" class="pagination-label"
              >Сущностей на странице</label
            >
            <select
              id="entities-per-page"
              v-model.number="settingsForm.paging.entitiesPerPage"
              class="form-select"
            >
              <option v-for="opt in pagingOptions" :key="opt" :value="opt">
                {{ opt }}
              </option>
            </select>
          </div>
        </div>
      </div>

      <span v-if="saveSettingsAction.error.value" class="error-text">
        {{ saveSettingsAction.error.value }}
      </span>
      <Button
        :loading="saveSettingsAction.loading.value"
        @click="saveSettings"
        class="save-button"
      >
        Сохранить настройки
      </Button>
    </div>
  </section>
</template>

<script setup lang="ts">
import { ref, watch } from "vue";
import { useUserStore } from "@/entities/user";
import { PersonalApi } from "@/shared/api";
import Button from "@/shared/ui/Button/Button.vue";
import { useAsyncAction } from "@/shared/lib/composables/useAsyncAction";
import { useToast } from "@/shared/lib/composables/useToast";
import { Theme, type Preferences } from "@/shared/api/models/personal";
import type { User } from "@/shared/api/models/community/users";
import { useUiStore } from "@/shared/stores/ui";

const props = defineProps<{
  user: User;
}>();

const userStore = useUserStore();
const uiStore = useUiStore();
const toast = useToast();

// Backend-allowed paging values
const pagingOptions = [5, 10, 20, 30, 40, 50, 100, 200];

// Settings form
const settingsForm = ref({
  theme: Theme.Light as Theme,
  paging: {
    postsPerPage: 50,
    commentsPerPage: 50,
    topicsPerPage: 50,
    messagesPerPage: 50,
    entitiesPerPage: 50,
  },
});

// Initialize form from user data
watch(
  () => props.user,
  (currentUser) => {
    if (currentUser) {
      settingsForm.value = {
        theme: currentUser.settings?.theme || Theme.Light,
        paging: currentUser.settings?.paging || {
          postsPerPage: 50,
          commentsPerPage: 50,
          topicsPerPage: 50,
          messagesPerPage: 50,
          entitiesPerPage: 50,
        },
      };
    }
  },
  { immediate: true },
);

// Save settings
const saveSettingsAction = useAsyncAction();

const saveSettings = () => {
  saveSettingsAction.execute(async () => {
    const preferences: Partial<Preferences> = {
      theme: settingsForm.value.theme,
      paging: settingsForm.value.paging,
    };

    const { error } = await PersonalApi.updateMyPreferences(preferences);
    if (error) throw new Error("Не удалось сохранить настройки");

    // Apply theme immediately
    uiStore.updateTheme(settingsForm.value.theme);

    await userStore.fetchUser();
    toast.success("Настройки успешно сохранены");
  });
};
</script>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"
@import "src/assets/styles/Inputs"
@import "../AccountPage.styles"

.settings-content
  padding: $medium
  background-color: $bg-element
  border-radius: $border-radius

.pagination-grid
  display: grid
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr))
  gap: $small

.pagination-item
  display: flex
  flex-direction: column

.pagination-label
  display: block
  margin-bottom: $tiny
  color: $text-muted
  font-size: $secondary-font-size

@media (max-width: 768px)
  .pagination-grid
    grid-template-columns: 1fr
</style>
