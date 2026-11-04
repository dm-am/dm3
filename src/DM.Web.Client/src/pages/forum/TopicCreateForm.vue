<script setup lang="ts">
import { computed, ref } from "vue";
import { useRouter } from "vue-router";
import { storeToRefs } from "pinia";
import { LoginPrompt } from "@/features/auth";
import { useBoardsStore } from "@/entities/forum";
import { useAuthStore } from "@/shared/stores";
import { useAnimatedHeightToggle } from "@/shared/lib/composables/useAnimatedHeightToggle";
import { useViewerChange } from "@/shared/lib/composables/useViewerChange";
import { BBCodeEditor } from "@/shared/ui/BBCodeEditor";
import { parseApiErrors } from "@/shared/lib/utils/apiErrors";
import { readFieldError } from "@/shared/lib/errors/validationErrors";
import { refusalHere } from "@/shared/lib/errors";
import { ActionResult } from "@/shared/ui/ActionResult";
import { TOPIC_TITLE_MAX_LENGTH } from "@/shared/lib/constants/forum";

/** "Создать топик" under the listing: the button and its form, or a guest's sign-in. */
const router = useRouter();
const store = useBoardsStore();
const { selectedBoard } = storeToRefs(store);
const { user } = storeToRefs(useAuthStore());

// Offered by the server's answer for this viewer on this board (board.viewer,
// TopicService.MayCreate); a guest is invited only where signing in opens one.
const canCreateTopic = computed(
  () => !!user.value && !!selectedBoard.value?.viewer?.canCreateTopic,
);
const invitesSignIn = computed(
  () => !user.value && !!selectedBoard.value?.viewer?.canCreateTopicAfterSignIn,
);

// The answer is for whoever held the session when the board was read.
useViewerChange(() => {
  const alias = selectedBoard.value?.alias;
  if (alias) void store.trySelectBoardByAlias(alias);
});

const showCreateForm = ref(false);
// Out of the registry: "Развернуть все" reads content, never opens tools.
const createFormZoneRef = ref<HTMLElement | null>(null);
const {
  pinnedHeight: createFormPinnedHeight,
  toggle: animateCreateForm,
  onTransitionEnd: onCreateFormTransitionEnd,
} = useAnimatedHeightToggle(createFormZoneRef, (next) => {
  showCreateForm.value = next;
});
const newTitle = ref("");
const newText = ref("");
const creating = ref(false);
const createErrors = ref<Record<string, string[]>>({});
const createGeneralError = ref<string | null>(null);

function toggleCreateForm() {
  animateCreateForm(!showCreateForm.value);
  // The toggle has already set the ref; the draft is cleared on closing.
  if (!showCreateForm.value) {
    newTitle.value = "";
    newText.value = "";
    createErrors.value = {};
    createGeneralError.value = null;
  }
}

async function handleCreateTopic() {
  if (!selectedBoard.value || creating.value) return;

  creating.value = true;
  createErrors.value = {};
  createGeneralError.value = null;
  try {
    const { data, error } = await store.createTopic(selectedBoard.value.alias, {
      title: newTitle.value,
      text: newText.value,
    });

    if (error) {
      const fieldErrors = parseApiErrors(error);
      createErrors.value = fieldErrors;
      // No field errors (a conflict, a board gone): the general line, the refusal
      // of the interceptor's statuses included; the form stays open with its text.
      if (!Object.keys(fieldErrors).length) {
        createGeneralError.value = refusalHere(
          error,
          "Не удалось создать топик",
        );
      }
      return;
    }

    const created = data?.resource;
    if (created) {
      showCreateForm.value = false;
      newTitle.value = "";
      newText.value = "";
      router.push({
        name: "topic",
        params: {
          alias: selectedBoard.value.alias,
          num: created.topicNumber,
        },
      });
    }
  } finally {
    creating.value = false;
  }
}

const titleError = computed(() => readFieldError(createErrors.value, "title"));
const textError = computed(() => readFieldError(createErrors.value, "text"));
</script>

<template>
  <!-- Below the listing: the toggle and its form, a guest's sign-in, or nothing. -->
  <div v-if="canCreateTopic || invitesSignIn" class="create-topic-section">
    <LoginPrompt v-if="invitesSignIn" action="создать топик" />
    <button
      v-else
      type="button"
      class="create-topic-button"
      @click="toggleCreateForm"
    >
      {{ showCreateForm ? "Отмена" : "Создать топик" }}
    </button>
  </div>

  <!-- v-show: a closed zone of 0 height would still take the column gap. -->
  <div
    v-show="
      canCreateTopic && (showCreateForm || createFormPinnedHeight !== null)
    "
    ref="createFormZoneRef"
    class="expand-zone"
    :class="{ animating: createFormPinnedHeight !== null }"
    :style="
      createFormPinnedHeight !== null
        ? { height: createFormPinnedHeight }
        : undefined
    "
    @transitionend="onCreateFormTransitionEnd"
    @transitioncancel="onCreateFormTransitionEnd"
  >
    <form
      v-if="showCreateForm && canCreateTopic"
      class="create-topic-form"
      @submit.prevent="handleCreateTopic"
    >
      <form-field
        label="Заголовок"
        name="title"
        :errors="titleError ? [titleError] : []"
      >
        <input
          id="create-topic-title"
          v-model="newTitle"
          type="text"
          class="create-topic-title-input"
          placeholder="Заголовок топика"
          :maxlength="TOPIC_TITLE_MAX_LENGTH"
        />
      </form-field>

      <form-field
        label="Текст"
        name="text"
        :errors="textError ? [textError] : []"
      >
        <BBCodeEditor
          v-model="newText"
          context="common"
          placeholder="Текст топика..."
          :min-height="150"
          :max-height="400"
          :resizable="true"
        />
      </form-field>

      <ActionResult :text="createGeneralError" class="create-topic-error" />

      <div class="create-topic-actions">
        <Button
          variant="primary"
          :loading="creating"
          :disabled="!newTitle.trim() || !newText.trim()"
        >
          Создать
        </Button>
      </div>
    </form>
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Inputs" as *
@use "@/assets/styles/Buttons" as *

.create-topic-button
  +button

.create-topic-form
  display: flex
  flex-direction: column
  gap: $small
  padding: $medium
  background-color: $bg-element
  border: 1px dashed $border

.create-topic-title-input
  width: 100%
  +input()

// The refusal line of the site's forms, over the buttons.
.create-topic-error
  --action-result-margin: 0

.create-topic-actions
  display: flex
</style>
