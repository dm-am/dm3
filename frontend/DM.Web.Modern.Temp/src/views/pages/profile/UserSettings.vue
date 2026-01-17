<script setup lang="ts">
import { ref, computed, watch } from "vue";
import { storeToRefs } from "pinia";
import { useCommunityStore } from "@/stores/community";
import { useUserStore } from "@/stores";
import { UserRole } from "@/api/models/community";
import { ColorSchema } from "@/api/models/community/user-settings";
import TheLoader from "@/components/TheLoader.vue";
import TextArea from "@/components/inputs/TextArea.vue";
import TheButton from "@/components/inputs/TheButton.vue";

const communityStore = useCommunityStore();
const { selectedUser: user } = storeToRefs(communityStore);
const { user: currentUser } = storeToRefs(useUserStore());

const isSaving = ref(false);

const colorSchemaLabels: Record<ColorSchema, string> = {
  [ColorSchema.Light]: "Светлая",
  [ColorSchema.Dark]: "Тёмная",
};

const colorSchema = ref<ColorSchema>(ColorSchema.Light);
const postsPerPage = ref(10);
const commentsPerPage = ref(10);
const topicsPerPage = ref(10);
const messagesPerPage = ref(10);
const entitiesPerPage = ref(10);
const mentorGreetingsMessage = ref("");

const canSetMentorGreeting = computed(() =>
  currentUser.value?.roles.some((r) =>
    [UserRole.Admin, UserRole.SeniorModerator, UserRole.Mentor].includes(r)
  ),
);

watch(
  () => user.value?.settings,
  (settings) => {
    if (settings) {
      colorSchema.value = settings.colorSchema ?? ColorSchema.Light;
      if (settings.pagingLimits) {
        postsPerPage.value = settings.pagingLimits.postsPerPage ?? 10;
        commentsPerPage.value = settings.pagingLimits.commentsPerPage ?? 10;
        topicsPerPage.value = settings.pagingLimits.topicsPerPage ?? 10;
        messagesPerPage.value = settings.pagingLimits.messagesPerPage ?? 10;
        entitiesPerPage.value = settings.pagingLimits.entitiesPerPage ?? 10;
      }
      mentorGreetingsMessage.value = settings.mentorGreetingsMessage || "";
    }
  },
  { immediate: true },
);

const saveSettings = async () => {
  if (!user.value) return;

  isSaving.value = true;
  await communityStore.updateUser(user.value.login, {
    settings: {
      colorSchema: colorSchema.value,
      pagingLimits: {
        postsPerPage: postsPerPage.value,
        commentsPerPage: commentsPerPage.value,
        topicsPerPage: topicsPerPage.value,
        messagesPerPage: messagesPerPage.value,
        entitiesPerPage: entitiesPerPage.value,
      },
      mentorGreetingsMessage: mentorGreetingsMessage.value,
    },
  });
  isSaving.value = false;
};
</script>

<template>
  <the-loader v-if="!user" :big="true" />

  <div v-else class="settings">
    <section class="settings-section">
      <h3>Цветовая схема</h3>
      <select v-model="colorSchema" :disabled="isSaving" class="select-input">
        <option
          v-for="(label, value) in colorSchemaLabels"
          :key="value"
          :value="value"
        >
          {{ label }}
        </option>
      </select>
    </section>

    <section class="settings-section">
      <h3>Пагинация</h3>
      <div class="paging-grid">
        <label class="paging-item">
          <span>Постов на странице</span>
          <input
            v-model.number="postsPerPage"
            type="number"
            min="5"
            max="100"
            :disabled="isSaving"
            class="number-input"
          />
        </label>
        <label class="paging-item">
          <span>Комментариев на странице</span>
          <input
            v-model.number="commentsPerPage"
            type="number"
            min="5"
            max="100"
            :disabled="isSaving"
            class="number-input"
          />
        </label>
        <label class="paging-item">
          <span>Тем на странице</span>
          <input
            v-model.number="topicsPerPage"
            type="number"
            min="5"
            max="100"
            :disabled="isSaving"
            class="number-input"
          />
        </label>
        <label class="paging-item">
          <span>Сообщений на странице</span>
          <input
            v-model.number="messagesPerPage"
            type="number"
            min="5"
            max="100"
            :disabled="isSaving"
            class="number-input"
          />
        </label>
        <label class="paging-item">
          <span>Сущностей на странице</span>
          <input
            v-model.number="entitiesPerPage"
            type="number"
            min="5"
            max="100"
            :disabled="isSaving"
            class="number-input"
          />
        </label>
      </div>
    </section>

    <section v-if="canSetMentorGreeting" class="settings-section">
      <h3>Приветствие ментора</h3>
      <text-area
        v-model="mentorGreetingsMessage"
        :disabled="isSaving"
        class="mentor-greeting"
      />
    </section>

    <the-button :loading="isSaving" @click="saveSettings">
      Сохранить настройки
    </the-button>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Inputs"

.settings
  max-width: $grid-step * 150

.settings-section
  margin-bottom: $big

  h3
    margin-bottom: $small

.select-input
  +input-base()
  font-size: inherit

.paging-grid
  display: grid
  grid-template-columns: repeat(2, 1fr)
  gap: $small

.paging-item
  display: flex
  flex-direction: column
  gap: $tiny

  span
    font-size: $secondary-font-size

.number-input
  +input-base()
  width: 100px
  font-size: inherit

.mentor-greeting
  :deep(textarea)
    min-height: $grid-step * 30
</style>
