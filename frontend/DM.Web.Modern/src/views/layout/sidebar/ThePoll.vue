<script setup lang="ts">
import type { Poll, PollOptionId } from "@/api/models/community";
import ProgressBar from "@/components/ProgressBar.vue";
import { IconType } from "@/components/icons/iconType";
import { computed, ref } from "vue";
import dayjs from "dayjs";
import { storeToRefs } from "pinia";
import { useUserStore } from "@/stores";
import { usePollsStore } from "@/stores/polls";
import { userIsHighAuthority } from "@/api/models/community/helpers";
import TheButton from "@/components/inputs/TheButton.vue";

const userStore = useUserStore();
const { user } = storeToRefs(userStore);
const { vote, unvote, editPoll } = usePollsStore();

const props = withDefaults(
  defineProps<{
    poll: Poll;
    detailed?: boolean;
  }>(),
  { detailed: false },
);

const closed = computed(() => dayjs(props.poll.ends).isBefore(dayjs()));
const endsFormatted = computed(() =>
  dayjs(props.poll.ends).format("DD.MM.YYYY HH:mm"),
);
const totalVotes = computed(() =>
  props.poll.options.reduce((sum, option) => sum + option.votesCount, 0),
);
const voted = computed(() => props.poll.options.some((option) => option.voted));
const canEdit = computed(() => props.detailed && userIsHighAuthority(userStore.user));

// Edit mode
const isEditing = ref(false);
const editTitle = ref("");
const editEnds = ref("");
const isSubmitting = ref(false);
const editError = ref("");

function startEditing() {
  editTitle.value = props.poll.title;
  editEnds.value = dayjs(props.poll.ends).format("YYYY-MM-DDTHH:mm");
  editError.value = "";
  isEditing.value = true;
}

function cancelEditing() {
  isEditing.value = false;
  editError.value = "";
}

async function saveEdit() {
  if (!editTitle.value.trim()) {
    editError.value = "Введите название опроса";
    return;
  }

  isSubmitting.value = true;
  editError.value = "";

  const { error } = await editPoll(props.poll.id!, {
    title: editTitle.value.trim(),
    ends: new Date(editEnds.value).toISOString(),
  });

  isSubmitting.value = false;

  if (error) {
    if (error.status === 403) {
      editError.value = "Недостаточно прав";
    } else {
      editError.value = "Не удалось сохранить";
    }
  } else {
    isEditing.value = false;
  }
}

async function voteForOption(optionId: PollOptionId) {
  await vote(props.poll.id!, optionId);
}

async function cancelVote() {
  await unvote(props.poll.id!);
}
</script>
<template>
  <div class="poll" :class="{ 'poll--detailed': detailed }">
    <!-- Edit mode -->
    <template v-if="isEditing">
      <div class="poll-edit-form">
        <div class="edit-field">
          <label class="edit-label"><strong>Название</strong></label>
          <input v-model="editTitle" type="text" class="edit-input" />
        </div>
        <div class="edit-field">
          <label class="edit-label"><strong>Окончание</strong></label>
          <input v-model="editEnds" type="datetime-local" class="edit-input" />
        </div>
        <div class="edit-actions">
          <the-button :disabled="isSubmitting" @click="saveEdit">
            {{ isSubmitting ? "Сохранение..." : "Сохранить" }}
          </the-button>
          <the-button secondary @click="cancelEditing">Отмена</the-button>
          <span v-if="editError" class="edit-error">{{ editError }}</span>
        </div>
      </div>
    </template>

    <!-- View mode -->
    <template v-else>
      <div class="poll-title">
        {{ poll.title }}
        <a v-if="canEdit" class="poll-edit-link" @click="startEditing" title="Редактировать">
          <the-icon :font="IconType.Edit" />
        </a>
      </div>
      <div v-if="detailed" class="poll-meta">
        <secondary-text v-if="closed" class="poll-status poll-status--closed">
          Завершен
        </secondary-text>
        <secondary-text v-else class="poll-status poll-status--active">
          Активен до {{ endsFormatted }}
        </secondary-text>
        <secondary-text class="poll-votes">
          Всего голосов: {{ totalVotes }}
        </secondary-text>
      </div>
    <div v-else class="poll-status-inline">
      <secondary-text v-if="closed">Завершен</secondary-text>
      <secondary-text v-else>Активен до {{ endsFormatted }}</secondary-text>
    </div>
    <progress-bar
      v-for="option in poll.options"
      :key="option.id"
      :current="option.votesCount"
      :goal="totalVotes || 1"
      :class="{ 'poll-option-voted': option.voted }"
    >
      <the-icon v-if="option.voted" :font="IconType.Tick" />
      {{ option.text }}&nbsp;&ndash;&nbsp;{{ option.votesCount }}
      <a
        v-if="!closed && user && !voted"
        @click="voteForOption(option.id)"
        class="poll-option-vote"
        title="Проголосовать"
      />
      <a
        v-if="!closed && user && option.voted"
        @click="cancelVote"
        class="poll-option-vote"
        title="Отменить голос"
      />
    </progress-bar>
    <div v-if="detailed && !user && !closed" class="poll-login-hint">
      <secondary-text>Войдите чтобы голосовать</secondary-text>
    </div>
    </template>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Themes"
@import "src/assets/styles/Variables"

.poll
  margin: $small 0

  &--detailed
    margin-bottom: $large
    padding: $medium
    border: 1px dashed $border
    background: $bg-element
    max-width: none

.poll-title
  font-weight: bold

.poll--detailed .poll-title
  font-size: 1.1em
  color: $heading
  margin-bottom: $small

.poll-meta
  display: flex
  gap: $medium
  flex-wrap: wrap
  margin-bottom: $medium

.poll-status
  &--closed
    color: $text-muted
  &--active
    color: $accent-green

.poll-votes
  color: $text-muted

.poll-option-vote
  display: block
  position: absolute
  top: 0
  left: 0
  right: 0
  bottom: 0
  cursor: pointer
  z-index: 1

.poll-option-voted
  font-weight: bold

.poll-login-hint
  margin-top: $small
  text-align: center

.poll-edit-link
  margin-left: $small
  cursor: pointer
  opacity: 0.5
  transition: opacity 0.15s

  &:hover
    opacity: 1

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
    border-color: $button-border-hover

.edit-actions
  display: flex
  align-items: center
  gap: $small
  margin-top: $small

.edit-error
  color: $accent-red
  font-size: $secondary-font-size
</style>
