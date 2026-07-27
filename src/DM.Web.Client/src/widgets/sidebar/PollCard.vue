<script setup lang="ts">
import type { Poll } from "@/entities/poll";
import ProgressBar from "@/shared/ui/ProgressBar/ProgressBar.vue";
import { SvgIcon } from "@/shared/ui/Icon";
import { symbols } from "@/shared/lib/utils/icons";
import { highlightMatch } from "@/shared/lib/utils/highlight";
import { Tooltip } from "@/shared/ui/Tooltip";
import { computed, ref } from "vue";
import dayjs from "dayjs";
import { storeToRefs } from "pinia";
import { useUserStore, userIsSeniorModerator } from "@/entities/user";
import { usePollVote, PollEditForm } from "@/features/poll-vote";
import { buildSubscribersTooltip } from "@/shared/lib/utils/tooltipBuilders";

const userStore = useUserStore();
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

// Edit mode is delegated to features/poll-vote PollEditForm; this only tracks
// whether the inline editor is open.
const isEditing = ref(false);

// Voters tooltip mirrors the readers tooltip: one uniform-colored line
// "Голосовали: A, B, C" (with "... и еще N" when truncated), not a faded
// title over a column of names. Only meaningful for public polls with at
// least one voter - anonymous polls or empty options disable the tooltip.
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

// Explicit literal separator between the checkmark and the option text -
// whitespace-condense would otherwise glue them together when copied.
function optionText(option: Poll["options"][number]): string {
  const checkmark = option.voted ? `${symbols.checkmark} ` : "";
  return `${checkmark}${option.text}`;
}
</script>
<template>
  <div class="poll">
    <!-- Edit mode -->
    <PollEditForm
      v-if="isEditing"
      :poll="poll"
      @saved="isEditing = false"
      @cancel="isEditing = false"
    />

    <!-- View mode -->
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
        <!-- Option text on the left, vote counter in brackets pinned to the
             right edge — the site-wide bracketed-counter idiom. For public
             polls with voters the counter itself is the voters-list trigger
             (cursor: help, tooltip on hover or focus; no underline by
             product decision 2026-07-11), so the rest of the bar stays
             clean for the vote action. Guests simply see the numbers — no
             login prompt. -->
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
              @click="voteForOption(poll.id!, option.id)"
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
              @click="cancelVote(poll.id!)"
            />
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
  padding: 0
  border: none
  background: transparent
  cursor: pointer
  z-index: 1

// Block line, NOT flex: element flex items copy with a newline between
// them (Chrome selection serializer), so the old flex row copied as
// "text\n(count)". Inline text + float:right counter keeps the identical
// "text left, counter pinned right" single-line geometry while the row
// copies as one line: "text (count)".
.poll-option-row
  display: block

// Zero-width preserved space between text and counter: invisible in
// layout (font-size: 0), but Selection.toString() still emits a real " ".
.copy-space
  white-space: pre
  font-size: 0

.poll-option-count
  float: right
  margin-left: $small

  .count-bracket
    color: $text-muted

  // The counter is the voters-list trigger on public polls: it sits above
  // the full-bar vote overlay so its tooltip stays reachable; the only
  // affordance is the help cursor (no underline by product decision)
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
  opacity: 0.5
  transition: opacity 0.15s

  &:hover
    opacity: 1

.poll-type-indicator
  margin-top: $tiny
  font-size: $tertiary-font-size
  color: $text-muted
</style>
