<script setup lang="ts">
// A warning is never withdrawn: its points are corrected, signed and explained instead.
import { computed, ref } from "vue";
import Dialog from "@/shared/ui/Layout/Dialog.vue";
import DialogTitle from "@/shared/ui/Layout/DialogTitle.vue";
import Form from "@/shared/ui/Form/Form.vue";
import FormField from "@/shared/ui/Form/FormField.vue";
import { Select, type SelectOption } from "@/shared/ui/Select";
import { warningApi, type Warning } from "@/entities/moderation";
import { WARNING_LIMIT } from "../api";
import { OWNS_REFUSAL } from "@/shared/api/interceptor";
import { REASON_MAX_LENGTH } from "../lib/limits";

const props = defineProps<{
  warningId: string;
  currentPoints: number;
}>();

const emit = defineEmits<{
  (e: "success", warning: Warning | null): void;
  (e: "cancel"): void;
}>();

// Zero included: zero is a correction, not an absence of one.
const pointsOptions: SelectOption[] = Array.from(
  { length: WARNING_LIMIT + 1 },
  (_, points) => ({ value: String(points), label: String(points) }),
);

const points = ref(String(props.currentPoints));
const reason = ref("");

const reasonTooLong = computed(() => reason.value.length > REASON_MAX_LENGTH);
const canSubmit = computed(
  () => reason.value.trim().length > 0 && !reasonTooLong.value,
);

async function send() {
  const result = await warningApi.changeWarningPoints(
    props.warningId,
    { points: Number(points.value), reason: reason.value },
    OWNS_REFUSAL,
  );
  if (!result.error) emit("success", result.data?.resource ?? null);
  return result;
}
</script>

<template>
  <Dialog
    :action="send"
    failure="Не удалось изменить баллы"
    :dirty="reason.trim().length > 0"
    discard-text="Набранная причина пропадет"
    @close="emit('cancel')"
  >
    <DialogTitle>Изменение баллов</DialogTitle>

    <Form :valid="canSubmit" action="Сохранить" cancel="Отмена">
      <FormField label="Баллы" name="points-change" field="points">
        <Select
          id="points-change"
          :model-value="points"
          :options="pointsOptions"
          @update:model-value="(v) => (points = v)"
        />
      </FormField>

      <FormField
        label="Причина изменения"
        name="points-change-reason"
        field="reason"
        :errors="reasonTooLong ? ['Слишком длинное значение'] : []"
      >
        <textarea
          id="points-change-reason"
          v-model="reason"
          rows="4"
        ></textarea>
      </FormField>
    </Form>
  </Dialog>
</template>
