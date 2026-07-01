<script setup lang="ts">
import type { Poll, PollOptionId } from "@/entities/poll";
import ProgressBar from "@/shared/ui/ProgressBar/ProgressBar.vue";
import { SvgIcon } from "@/shared/ui/Icon";
import { symbols } from "@/shared/lib/utils/icons";
import { highlightMatch } from "@/shared/lib/utils/highlight";
import { Tooltip } from "@/shared/ui/Tooltip";
import { computed, ref } from "vue";
import dayjs from "dayjs";
import { storeToRefs } from "pinia";
import { useUserStore, userIsSeniorModerator } from "@/entities/user";
import { usePollsStore } from "@/entities/poll";
import Button from "@/shared/ui/Button/Button.vue";
import { buildSubscribersTooltip } from "@/shared/lib/utils/tooltipBuilders";

const userStore = useUserStore();
const { user } = storeToRefs(userStore);
const { vote, unvote, editPoll } = usePollsStore();

const props = withDefaults(
  defineProps<{
    poll: Poll;
    /** Show edit controls (for moderators on polls page) */
    controls?: boolean;
    /** Search query for highlighting matches in title */
    searchQuery?: string;
  }>(),
  { controls: false },
);

// Use server-computed status
const isActive = computed(() => props.poll.status === "Active");

const startsFormatted = computed(() =>
  dayjs(props.poll.startsUtc).format("DD.MM.YYYY [в] HH:mm"),
);
const endsFormatted = computed(() =>
  dayjs(props.poll.endsUtc).format("DD.MM.YYYY [в] HH:mm"),
);
const totalVotes = computed(() =>
  props.poll.options.reduce((sum, option) => sum + option.votesCount, 0),
);
const voted = computed(() => props.poll.options.some((option) => option.voted));
const canEdit = computed(
  () => props.controls && userIsSeniorModerator(userStore.user),
);

// Status text
const statusText = computed(() => {
  switch (props.poll.status) {
    case "Pending":
      return `Начнется ${startsFormatted.value}`;
    case "Active":
      return `Активен до ${endsFormatted.value}`;
    case "Closed":
      return "Завершен";
    default:
      return "";
  }
});

// Tooltip with full dates
const statusTooltip = computed(() => {
  return `Начало: ${startsFormatted.value}\nОкончание: ${endsFormatted.value}`;
});

// Edit mode
const isEditing = ref(false);
const editTitle = ref("");
const editDetails = ref("");
const editStartsUtc = ref("");
const editEndsUtc = ref("");
const editIsAnonymous = ref(true);
const isSubmitting = ref(false);
const editError = ref("");

// Track if changing from anonymous to public (will reset votes)
const willResetVotes = computed(
  () => props.poll.isAnonymous && !editIsAnonymous.value,
);

function startEditing() {
  editTitle.value = props.poll.title;
  editDetails.value = props.poll.details || "";
  editStartsUtc.value = dayjs(props.poll.startsUtc).format("YYYY-MM-DDTHH:mm");
  editEndsUtc.value = dayjs(props.poll.endsUtc).format("YYYY-MM-DDTHH:mm");
  editIsAnonymous.value = props.poll.isAnonymous;
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
    isEditing.value = false;
  }
}

async function voteForOption(optionId: PollOptionId) {
  await vote(props.poll.id!, optionId);
}

async function cancelVote() {
  await unvote(props.poll.id!);
}

// Voters tooltip mirrors the readers tooltip: one uniform-colored line
// "Голосовали: A, B, C" (with "... и еще N" when truncated), not a faded
// title over a column of names.
function votersTooltipText(option: Poll["options"][number]): string {
  return buildSubscribersTooltip(
    {
      subscribersCount: option.totalVoters ?? option.voters?.length ?? 0,
      subscriberUsernames: option.voters?.map((v) => v.username) ?? [],
    },
    "",
    "Голосовали",
  );
}
</script>
<template>
  <div class="poll">
    <!-- Edit mode -->
    <template v-if="isEditing">
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
          <input
            v-model="editStartsUtc"
            type="datetime-local"
            class="edit-input"
          />
        </div>
        <div class="edit-field">
          <label class="edit-label"><strong>Окончание</strong></label>
          <input
            v-model="editEndsUtc"
            type="datetime-local"
            class="edit-input"
          />
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
            {{ isSubmitting ? "Сохранение..." : "Сохранить" }}
          </Button>
          <Button @click="cancelEditing">Отмена</Button>
          <span v-if="editError" class="edit-error">{{ editError }}</span>
        </div>
      </div>
    </template>

    <!-- View mode -->
    <template v-else>
      <div class="poll-title">
        <span
          v-if="searchQuery"
          v-html="highlightMatch(poll.title, searchQuery)"
        />
        <template v-else>{{ poll.title }}</template>
        <Tooltip v-if="canEdit" text="Редактировать">
          <a class="poll-edit-link" @click="startEditing">
            <SvgIcon name="pencil" />
          </a>
        </Tooltip>
      </div>
      <div v-if="poll.details" class="poll-details">
        <span
          v-if="searchQuery"
          v-html="highlightMatch(poll.details, searchQuery)"
        />
        <template v-else>{{ poll.details }}</template>
      </div>
      <div class="poll-status-inline">
        <Tooltip :text="statusTooltip">
          <span class="muted">{{ statusText }}</span>
        </Tooltip>
      </div>
      <template v-for="option in poll.options" :key="option.id">
        <!-- Public poll with voters: hovering shows who voted. Read-only for
             guests — they simply cannot vote, with no login prompt. -->
        <Tooltip
          v-if="!poll.isAnonymous && option.voters?.length"
          :text="votersTooltipText(option)"
        >
          <ProgressBar
            :current="option.votesCount"
            :goal="totalVotes || 1"
            :class="{ 'poll-option-voted': option.voted }"
          >
            <span v-if="option.voted">{{ symbols.checkmark }}</span>
            {{ option.text }}&nbsp;&ndash;&nbsp;{{ option.votesCount }}
            <Tooltip v-if="isActive && user && !voted" text="Проголосовать">
              <a @click="voteForOption(option.id)" class="poll-option-vote" />
            </Tooltip>
            <Tooltip
              v-if="isActive && user && option.voted"
              text="Отменить голос"
            >
              <a @click="cancelVote" class="poll-option-vote" />
            </Tooltip>
          </ProgressBar>
        </Tooltip>

        <!-- Anonymous poll or no voters - no tooltip -->
        <ProgressBar
          v-else
          :current="option.votesCount"
          :goal="totalVotes || 1"
          :class="{ 'poll-option-voted': option.voted }"
        >
          <span v-if="option.voted">{{ symbols.checkmark }}</span>
          {{ option.text }}&nbsp;&ndash;&nbsp;{{ option.votesCount }}
          <Tooltip v-if="isActive && user && !voted" text="Проголосовать">
            <a @click="voteForOption(option.id)" class="poll-option-vote" />
          </Tooltip>
          <Tooltip
            v-if="isActive && user && option.voted"
            text="Отменить голос"
          >
            <a @click="cancelVote" class="poll-option-vote" />
          </Tooltip>
        </ProgressBar>
      </template>

      <!-- Poll type indicator -->
      <div class="poll-type-indicator">
        {{ poll.isAnonymous ? "Анонимный опрос" : "Публичный опрос" }}
      </div>
    </template>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Themes"
@import "src/assets/styles/Variables"

.poll
  margin: $small 0

  &:first-child
    margin-top: 0

.poll-title
  font-weight: bold

.poll-details
  margin: $tiny 0
  font-size: $secondary-font-size

.poll-status-inline
  margin-top: $tiny
  font-size: $secondary-font-size
  cursor: help

.muted
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

.poll-type-indicator
  margin-top: $tiny
  font-size: $tertiary-font-size
  color: $text-muted

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
