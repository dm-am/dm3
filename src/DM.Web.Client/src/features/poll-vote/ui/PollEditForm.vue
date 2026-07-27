<script setup lang="ts">
/**
 * PollEditForm — inline moderator edit form for a poll (title, description,
 * schedule, anonymity). Loads its draft from the poll on mount and persists
 * via the poll entity store; emits `saved` / `cancel` back to the display.
 */
import { ref, computed } from "vue";
import dayjs from "dayjs";
import type { Poll } from "@/entities/poll";
import { usePollsStore } from "@/entities/poll";
import Button from "@/shared/ui/Button/Button.vue";

const props = defineProps<{
  poll: Poll;
}>();

const emit = defineEmits<{
  (e: "saved"): void;
  (e: "cancel"): void;
}>();

const { editPoll } = usePollsStore();

const editTitle = ref(props.poll.title);
const editDetails = ref(props.poll.details || "");
const editStartsUtc = ref(
  dayjs(props.poll.startsUtc).format("YYYY-MM-DDTHH:mm"),
);
const editEndsUtc = ref(dayjs(props.poll.endsUtc).format("YYYY-MM-DDTHH:mm"));
const editIsAnonymous = ref(props.poll.isAnonymous);
const isSubmitting = ref(false);
const editError = ref("");

// Track if changing from anonymous to public (will reset votes)
const willResetVotes = computed(
  () => props.poll.isAnonymous && !editIsAnonymous.value,
);

async function saveEdit() {
  if (!editTitle.value.trim()) {
    editError.value = "Введите название опроса";
    return;
  }

  isSubmitting.value = true;
  editError.value = "";

  const { error } = await editPoll(props.poll.id!, {
    title: editTitle.value.trim(),
    details: editDetails.value.trim() || undefined,
    startsUtc: new Date(editStartsUtc.value).toISOString(),
    endsUtc: new Date(editEndsUtc.value).toISOString(),
    isAnonymous: editIsAnonymous.value,
  });

  isSubmitting.value = false;

  if (error) {
    if (error.status === 403) {
      editError.value = "Недостаточно прав";
    } else {
      editError.value = "Не удалось сохранить";
    }
  } else {
    emit("saved");
  }
}
</script>

<template>
  <div class="poll-edit-form">
    <div class="edit-field">
      <label class="edit-label"><strong>Название</strong></label>
      <input v-model="editTitle" type="text" class="edit-input" />
    </div>
    <div class="edit-field">
      <label class="edit-label"><strong>Описание</strong></label>
      <textarea
        v-model="editDetails"
        class="edit-input edit-textarea"
        rows="3"
      />
    </div>
    <div class="edit-field">
      <label class="edit-label"><strong>Начало</strong></label>
      <input v-model="editStartsUtc" type="datetime-local" class="edit-input" />
    </div>
    <div class="edit-field">
      <label class="edit-label"><strong>Окончание</strong></label>
      <input v-model="editEndsUtc" type="datetime-local" class="edit-input" />
    </div>
    <div class="edit-field">
      <label class="edit-label"><strong>Тип опроса</strong></label>
      <div class="poll-type-selector">
        <label class="radio-option">
          <input type="radio" v-model="editIsAnonymous" :value="true" />
          <span>Анонимный</span>
        </label>
        <label class="radio-option">
          <input type="radio" v-model="editIsAnonymous" :value="false" />
          <span>Публичный</span>
        </label>
      </div>
      <div v-if="willResetVotes" class="warning-message">
        Все голоса будут сброшены
      </div>
    </div>
    <div class="edit-actions">
      <Button :disabled="isSubmitting" @click="saveEdit">
        {{ isSubmitting ? "Сохранение…" : "Сохранить" }}
      </Button>
      <Button @click="emit('cancel')">Отмена</Button>
      <span v-if="editError" class="edit-error">{{ editError }}</span>
    </div>
  </div>
</template>

<style scoped lang="sass">
.poll-edit-form
  padding: $small 0

.edit-field
  margin-bottom: $small

.edit-label
  display: block
  margin-bottom: $tiny
  color: $text
  font-size: $secondary-font-size

.edit-input
  display: block
  width: 100%
  padding: $small
  border: 1px dashed $border
  background-color: $input-bg-overlay
  color: $text
  font-family: inherit
  font-size: inherit
  box-sizing: border-box

  &:focus
    outline: none
    border-style: solid
    border-color: $border-focus

.edit-textarea
  resize: vertical
  min-height: 60px

.edit-actions
  display: flex
  align-items: center
  gap: $small
  margin-top: $small

.edit-error
  color: $accent-red
  font-size: $secondary-font-size

.poll-type-selector
  display: flex
  gap: $medium

.radio-option
  display: flex
  align-items: center
  gap: $tiny
  cursor: pointer

  input
    margin: 0

.warning-message
  margin-top: $tiny
  color: $accent-red
  font-size: $secondary-font-size
</style>
