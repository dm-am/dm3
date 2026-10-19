<script setup lang="ts">
import { ref, computed, useId } from "vue";
import dayjs from "dayjs";
import type { Poll } from "@/entities/poll";
import { PollTypeField, usePollsStore } from "@/entities/poll";
import { refusalHere } from "@/shared/lib/errors";
import { fromSiteInput, toSiteInput } from "@/shared/lib/utils/datetime";
import Button from "@/shared/ui/Button/Button.vue";
import { DateTimeInput } from "@/shared/ui/DatePicker";
import FormField from "@/shared/ui/Form/FormField.vue";
import TextArea from "@/shared/ui/TextArea/TextArea.vue";

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
// On the site's clock, the one the card prints the dates by.
const editStartsUtc = ref(toSiteInput(props.poll.startsUtc));
const editEndsUtc = ref(toSiteInput(props.poll.endsUtc));
const editIsAnonymous = ref(props.poll.isAnonymous);

// A begun poll keeps its past start, or it could not be edited (UpdatePollValidator sets no bound).
const startsMin = [toSiteInput(props.poll.startsUtc), toSiteInput()].sort()[0];
const endsMin = computed(() => {
  const starts = fromSiteInput(editStartsUtc.value);
  return starts ? toSiteInput(dayjs(starts).add(1, "minute")) : undefined;
});

// One per editor: every poll card of the page owns one.
const titleId = `poll-edit-title-${useId()}`;

const startsReason = ref<string | null>(null);
const endsReason = ref<string | null>(null);
const isSubmitting = ref(false);
const editError = ref("");

// Public after anonymous resets the votes.
const willResetVotes = computed(
  () => props.poll.isAnonymous && !editIsAnonymous.value,
);

async function saveEdit() {
  if (!editTitle.value.trim()) {
    editError.value = "Введите название опроса";
    return;
  }

  // A cleared field has no moment; `new Date` would throw past a raised isSubmitting.
  const startsUtc = fromSiteInput(editStartsUtc.value);
  const endsUtc = fromSiteInput(editEndsUtc.value);
  if (!startsUtc || !endsUtc) {
    editError.value = "Укажите начало и окончание опроса";
    return;
  }
  if (startsReason.value || endsReason.value) {
    editError.value = "Проверьте начало и окончание опроса";
    return;
  }

  isSubmitting.value = true;
  editError.value = "";

  const { error } = await editPoll(props.poll.id!, {
    title: editTitle.value,
    details: editDetails.value.trim() ? editDetails.value : undefined,
    startsUtc,
    endsUtc,
    isAnonymous: editIsAnonymous.value,
  });

  isSubmitting.value = false;

  if (error) {
    // The editor stays open with every field as typed; an expired session and no connection keep the plate.
    editError.value = refusalHere(error, "Не удалось сохранить") ?? "";
  } else {
    emit("saved");
  }
}
</script>

<template>
  <div class="poll-edit-form">
    <FormField label="Название" name="pollTitle">
      <input :id="titleId" v-model="editTitle" type="text" />
    </FormField>
    <FormField label="Описание" name="pollDetails" optional>
      <TextArea v-model="editDetails" />
    </FormField>
    <FormField
      label="Начало"
      name="pollStartsUtc"
      :errors="startsReason ? [startsReason] : []"
    >
      <DateTimeInput
        v-model="editStartsUtc"
        :min="startsMin"
        @reason="startsReason = $event"
      />
    </FormField>
    <FormField
      label="Окончание"
      name="pollEndsUtc"
      :errors="endsReason ? [endsReason] : []"
    >
      <DateTimeInput
        v-model="editEndsUtc"
        :min="endsMin"
        @reason="endsReason = $event"
      />
    </FormField>
    <PollTypeField v-model="editIsAnonymous">
      <div v-if="willResetVotes" class="warning-message">
        Все голоса будут сброшены
      </div>
    </PollTypeField>
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
.poll-edit-form
  padding: $small 0

.edit-actions
  display: flex
  align-items: center
  gap: $small
  margin-top: $small

.edit-error
  color: $accent-red
  font-size: $secondary-font-size

.warning-message
  margin-top: $tiny
  color: $accent-red
  font-size: $secondary-font-size
</style>
