<script setup lang="ts">
import type { Poll } from "@/entities/poll";
import ProgressBar from "@/shared/ui/ProgressBar/ProgressBar.vue";
import { SvgIcon } from "@/shared/ui/Icon";
import { symbols } from "@/shared/lib/utils/icons";
import { highlightMatch } from "@/shared/lib/utils/highlight";
import { Tooltip } from "@/shared/ui/Tooltip";
import { computed, ref } from "vue";
import { DATE_TIME_FORMAT, formatSiteTime } from "@/shared/lib/utils/datetime";
import { storeToRefs } from "pinia";
import { useAuthStore, userIsSeniorModerator } from "@/entities/user";
import { usePollVote, PollEditForm } from "@/features/poll-vote";
import { buildSubscribersTooltip } from "@/shared/lib/utils/tooltipBuilders";

const userStore = useAuthStore();
const { user } = storeToRefs(userStore);
const { voteForOption, cancelVote } = usePollVote();

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

const isActive = computed(() => props.poll.status === "Active");

const startsFormatted = computed(() =>
  formatSiteTime(props.poll.startsUtc, DATE_TIME_FORMAT),
);
const endsFormatted = computed(() =>
  formatSiteTime(props.poll.endsUtc, DATE_TIME_FORMAT),
);
const totalVotes = computed(() =>
  props.poll.options.reduce((sum, option) => sum + option.votesCount, 0),
);
const voted = computed(() => props.poll.options.some((option) => option.voted));
const canEdit = computed(
  () => props.controls && userIsSeniorModerator(userStore.user),
);

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

const statusTooltip = computed(
  () => `Начало: ${startsFormatted.value}\nОкончание: ${endsFormatted.value}`,
);

const isEditing = ref(false);

// One line, "Голосовали: A, B, C", as the readers tooltip; public polls with voters only.
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

function optionHasVoters(option: Poll["options"][number]): boolean {
  return !props.poll.isAnonymous && !!option.voters?.length;
}

// A literal space: whitespace condensing would glue the checkmark to the text in a copy.
function optionText(option: Poll["options"][number]): string {
  const checkmark = option.voted ? `${symbols.checkmark} ` : "";
  return `${checkmark}${option.text}`;
}
</script>
<template>
  <div class="poll">
    <PollEditForm
      v-if="isEditing"
      :poll="poll"
      @saved="isEditing = false"
      @cancel="isEditing = false"
    />

    <template v-else>
      <div class="poll-title">
        <span
          v-if="searchQuery"
          v-html="highlightMatch(poll.title, searchQuery)"
        />
        <template v-else>{{ poll.title }}</template>
        <Tooltip v-if="canEdit" text="Редактировать">
          <button
            type="button"
            class="poll-edit-link"
            aria-label="Редактировать опрос"
            @click="isEditing = true"
          >
            <SvgIcon name="pencil" />
          </button>
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
        <Tooltip :text="statusTooltip" focusable>
          <span class="muted">{{ statusText }}</span>
        </Tooltip>
      </div>
      <template v-for="option in poll.options" :key="option.id">
        <!-- The text left, the counter right; on a public poll the counter shows the voters -->
        <ProgressBar
          :current="option.votesCount"
          :goal="totalVotes || 1"
          :class="{ 'poll-option-voted': option.voted }"
        >
          <span class="poll-option-row"
            ><span class="poll-option-text">{{ optionText(option) }}</span
            ><span class="copy-space">{{ " " }}</span
            ><span class="poll-option-count"
              ><span class="count-bracket">(</span
              ><Tooltip
                v-if="optionHasVoters(option)"
                :text="votersTooltipText(option)"
                focusable
                ><span class="voters-count">{{
                  option.votesCount
                }}</span></Tooltip
              ><template v-else>{{ option.votesCount }}</template
              ><span class="count-bracket">)</span></span
            ></span
          >
          <Tooltip v-if="isActive && user && !voted" text="Проголосовать">
            <button
              type="button"
              class="poll-option-vote"
              :aria-label="`Проголосовать за: ${option.text}`"
              @click="voteForOption(poll.id, option.id)"
            />
          </Tooltip>
          <Tooltip
            v-if="isActive && user && option.voted"
            text="Отменить голос"
          >
            <button
              type="button"
              class="poll-option-vote"
              aria-label="Отменить голос"
              @click="cancelVote(poll.id)"
            />
          </Tooltip>
        </ProgressBar>
      </template>

      <div class="poll-type-indicator">
        {{ poll.isAnonymous ? "Анонимный опрос" : "Публичный опрос" }}
      </div>
    </template>
  </div>
</template>

<style scoped lang="sass">
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

.poll-option-vote
  display: block
  position: absolute
  top: 0
  left: 0
  right: 0
  bottom: 0
  padding: 0
  border: none
  background: transparent
  cursor: pointer
  z-index: 1

// Not flex: flex items copy with a newline between them; the float keeps the counter right.
.poll-option-row
  display: block

.poll-option-count
  float: right
  margin-left: $small

  .count-bracket
    color: $text-muted

  // Above the vote overlay, so its tooltip stays reachable.
  .voters-count
    position: relative
    z-index: 2
    cursor: help

.poll-option-voted
  font-weight: bold

.poll-edit-link
  display: inline-flex
  align-items: center
  margin-left: $small
  padding: 0
  border: none
  background: none
  color: inherit
  cursor: pointer
  opacity: $muted-opacity
  transition: opacity $transition-normal

  &:hover
    opacity: 1

.poll-type-indicator
  margin-top: $tiny
  font-size: $tertiary-font-size
  color: $text-muted
</style>
