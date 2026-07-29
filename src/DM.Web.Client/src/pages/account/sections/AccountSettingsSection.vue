<template>
  <section class="section">
    <h2 class="section-title">Настройки</h2>

    <div class="settings-content">
      <FormField label="Цветовая схема">
        <Select
          :model-value="settingsForm.theme"
          :options="themeOptions"
          @update:model-value="(v) => (settingsForm.theme = v as Theme)"
        />
      </FormField>

      <div class="pagination-group">
        <div class="pagination-group-label">Pagination настройки</div>
        <div class="pagination-grid">
          <FormField label="Постов на странице">
            <Select
              :model-value="String(settingsForm.paging.postsPerPage)"
              :options="pagingSelectOptions"
              @update:model-value="
                (v) => (settingsForm.paging.postsPerPage = Number(v))
              "
            />
          </FormField>

          <FormField label="Комментариев на странице">
            <Select
              :model-value="String(settingsForm.paging.commentsPerPage)"
              :options="pagingSelectOptions"
              @update:model-value="
                (v) => (settingsForm.paging.commentsPerPage = Number(v))
              "
            />
          </FormField>

          <FormField label="Тем на странице">
            <Select
              :model-value="String(settingsForm.paging.topicsPerPage)"
              :options="pagingSelectOptions"
              @update:model-value="
                (v) => (settingsForm.paging.topicsPerPage = Number(v))
              "
            />
          </FormField>

          <FormField label="Сообщений на странице">
            <Select
              :model-value="String(settingsForm.paging.messagesPerPage)"
              :options="pagingSelectOptions"
              @update:model-value="
                (v) => (settingsForm.paging.messagesPerPage = Number(v))
              "
            />
          </FormField>

          <FormField label="Сущностей на странице">
            <Select
              :model-value="String(settingsForm.paging.entitiesPerPage)"
              :options="pagingSelectOptions"
              @update:model-value="
                (v) => (settingsForm.paging.entitiesPerPage = Number(v))
              "
            />
          </FormField>
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
import { useAuthStore } from "@/entities/user";
import { personalApi } from "@/shared/api";
import Button from "@/shared/ui/Button/Button.vue";
import { FormField } from "@/shared/ui/Form";
import { Select, type SelectOption } from "@/shared/ui/Select";
import { useAsyncAction } from "@/shared/lib/composables/useAsyncAction";
import { useToast } from "@/shared/lib/composables/useToast";
import { Theme, type Preferences } from "@/shared/api/models/personal";
import type { User } from "@/shared/api/models/community/users";
import { useUiStore } from "@/shared/stores/ui";

const props = defineProps<{
  user: User;
}>();

const userStore = useAuthStore();
const uiStore = useUiStore();
const toast = useToast();

const themeOptions: SelectOption[] = [
  { value: Theme.Light, label: "Светлая" },
  { value: Theme.Dark, label: "Темная" },
];

// Backend-allowed paging values
const pagingOptions = [5, 10, 20, 30, 40, 50, 100, 200];
const pagingSelectOptions: SelectOption[] = pagingOptions.map((opt) => ({
  value: String(opt),
  label: String(opt),
}));

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

    const { error } = await personalApi.updateMyPreferences(preferences);
    if (error) throw new Error("Не удалось сохранить настройки");

    // Apply theme immediately
    uiStore.updateTheme(settingsForm.value.theme);

    await userStore.fetchUser();
    toast.success("Настройки успешно сохранены");
  });
};
</script>

<style scoped lang="sass">
@import "@/assets/styles/Inputs"
@import "../AccountPage.styles"

.settings-content
  padding: $medium
  background-color: $bg-element
  border-radius: $border-radius

.pagination-group
  margin: $small 0

.pagination-group-label
  margin-bottom: $tiny
  color: $text-muted
  font-size: $secondary-font-size

.pagination-grid
  display: grid
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr))
  gap: $small

@media (max-width: 768px)
  .pagination-grid
    grid-template-columns: 1fr
</style>
