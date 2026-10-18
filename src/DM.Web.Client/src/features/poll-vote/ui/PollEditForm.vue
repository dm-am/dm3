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
import { refusalHere } from "@/shared/lib/errors";
import { fromSiteInput, toSiteInput } from "@/shared/lib/utils/datetime";
import Button from "@/shared/ui/Button/Button.vue";
import { DateTimeInput } from "@/shared/ui/DatePicker";

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
// On the site's clock, the one the card prints the dates by: the editor of a
// poll the card calls "20:00" opens on 20:00 (toSiteInput, fromSiteInput).
const editStartsUtc = ref(toSiteInput(props.poll.startsUtc));
const editEndsUtc = ref(toSiteInput(props.poll.endsUtc));
const editIsAnonymous = ref(props.poll.isAnonymous);

// The bounds of an edit. A poll that has begun keeps the start it had, which
// is in the past: the start goes no earlier than the earlier of that and now,
// or the poll could not be edited at all (the server sets no bound on it
// either, UpdatePollValidator). The end comes after the start, as the server
// asks.
const startsMin = [toSiteInput(props.poll.startsUtc), toSiteInput()].sort()[0];
const endsMin = computed(() => {
  const starts = fromSiteInput(editStartsUtc.value);
  return starts ? toSiteInput(dayjs(starts).add(1, "minute")) : undefined;
});

// What the two fields say is wrong with them, shown under each.
const startsReason = ref<string | null>(null);
const endsReason = ref<string | null>(null);
const isSubmitting = ref(false);
const editError = ref("");

// The ids the captions point at. Random suffix, because the polls page renders
// a card per poll and every card owns an editor of its own: nothing closes the
// neighbour when a second one opens, and a constant id would then sit on two
// fields at once. Both `<label for>` and `aria-labelledby` resolve to the first
// match in the document, so the caption of the second editor would reach into
// the first. Same shape as FormField generates.
const uid = `poll-edit-${Math.random().toString(36).slice(2, 9)}`;
const titleId = `${uid}-title`;
const detailsId = `${uid}-details`;
const startsId = `${uid}-starts`;
const endsId = `${uid}-ends`;
const startsReasonId = `${uid}-starts-reason`;
const endsReasonId = `${uid}-ends-reason`;
const kindId = `${uid}-kind`;

// Track if changing from anonymous to public (will reset votes)
const willResetVotes = computed(
  () => props.poll.isAnonymous && !editIsAnonymous.value,
);

async function saveEdit() {
  if (!editTitle.value.trim()) {
    editError.value = "Введите название опроса";
    return;
  }

  // A cleared field has no moment; read by `new Date` it threw past a raised
  // isSubmitting and the editor hung.
  const startsUtc = fromSiteInput(editStartsUtc.value);
  const endsUtc = fromSiteInput(editEndsUtc.value);
  if (!startsUtc || !endsUtc) {
    editError.value = "Укажите начало и окончание опроса";
    return;
  }
  // The lines under the fields say what is wrong; this one says why the
  // press did nothing.
  if (startsReason.value || endsReason.value) {
    editError.value = "Проверьте начало и окончание опроса";
    return;
  }

  isSubmitting.value = true;
  editError.value = "";

  const { error } = await editPoll(props.poll.id!, {
    title: editTitle.value.trim(),
    details: editDetails.value.trim() || undefined,
    startsUtc,
    endsUtc,
    isAnonymous: editIsAnonymous.value,
  });

  isSubmitting.value = false;

  if (error) {
    // The editor stays open with every field as it was typed, and the line
    // beside the button says every refusal - a rejected date pair, a refused
    // edit, a wait, a dead server - with the plate the interceptor held for it
    // taken down. An expired session and a dead connection keep the plate.
    editError.value = refusalHere(error, "Не удалось сохранить") ?? "";
  } else {
    emit("saved");
  }
}
</script>

<template>
  <div class="poll-edit-form">
    <div class="edit-field">
      <label class="edit-label" :for="titleId"><strong>Название</strong></label>
      <input :id="titleId" v-model="editTitle" type="text" class="edit-input" />
    </div>
    <div class="edit-field">
      <label class="edit-label" :for="detailsId"
        ><strong>Описание</strong></label
      >
      <textarea
        :id="detailsId"
        v-model="editDetails"
        class="edit-input edit-textarea"
        rows="3"
      />
    </div>
    <!-- A date and a time under one caption: the caption names the row, and
         the reason under it describes the row. -->
    <div class="edit-field">
      <span :id="startsId" class="edit-label"><strong>Начало</strong></span>
      <DateTimeInput
        v-model="editStartsUtc"
        role="group"
        :aria-labelledby="startsId"
        :aria-describedby="startsReason ? startsReasonId : undefined"
        :min="startsMin"
        @reason="startsReason = $event"
      />
      <span
        v-if="startsReason"
        :id="startsReasonId"
        class="edit-field-error"
        role="alert"
        >{{ startsReason }}</span
      >
    </div>
    <div class="edit-field">
      <span :id="endsId" class="edit-label"><strong>Окончание</strong></span>
      <DateTimeInput
        v-model="editEndsUtc"
        role="group"
        :aria-labelledby="endsId"
        :aria-describedby="endsReason ? endsReasonId : undefined"
        :min="endsMin"
        @reason="endsReason = $event"
      />
      <span
        v-if="endsReason"
        :id="endsReasonId"
        class="edit-field-error"
        role="alert"
        >{{ endsReason }}</span
      >
    </div>
    <div class="edit-field">
      <span :id="kindId" class="edit-label"><strong>Тип опроса</strong></span>
      <div
        class="poll-type-selector"
        role="radiogroup"
        :aria-labelledby="kindId"
      >
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
      <Button variant="primary" :loading="isSubmitting" @click="saveEdit">
        Сохранить
      </Button>
      <Button @click="emit('cancel')">Отмена</Button>
      <span v-if="editError" class="edit-error">{{ editError }}</span>
    </div>
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Inputs" as *

.poll-edit-form
  padding: $small 0

.edit-field
  margin-bottom: $small

// A caption of a field of the page: +field-caption, as FormField draws it.
.edit-label
  display: block
  margin-bottom: $minor
  +field-caption

// The fields of the site (+input): the frame, the ground, the ring and the
// type of every field of a page, a line at $control-height and the text area
// growing with its rows. A dashed frame of its own made these the one set of
// fields on the site drawn another way.
.edit-input
  display: block
  width: 100%
  box-sizing: border-box

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

// What is wrong with a date, in the red line of 14 FormField draws under a
// field: the frame of the field stays as it is.
.edit-field-error
  display: block
  margin-top: $minor
  color: $accent-red
  font-size: $secondary-font-size

.poll-type-selector
  display: flex
  gap: $medium

.radio-option
  display: flex
  align-items: center
  gap: $small
  cursor: pointer

  input
    margin: 0

.warning-message
  margin-top: $tiny
  color: $accent-red
  font-size: $secondary-font-size
</style>
