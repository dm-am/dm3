<script setup lang="ts">
/**
 * "Изменение баллов" — the only thing that can still be done to a warning.
 *
 * A warning is a record of a decision and is never withdrawn, so what used to
 * be a delete is a correction of the points instead: the record keeps its place
 * in the log and in the profile, and the number it weighs on the violation
 * scale is what moves. The correction is signed and carries an explanation,
 * both of which the card then shows to every reader — which is why the
 * explanation is required here and not merely asked for.
 *
 * The rules mirror ChangeWarningPointsValidator: points 0-6, explanation
 * present and no longer than 2000 characters. Who may open this at all is the
 * server's answer, not this dialog's: the row carries `canChangePoints`.
 *
 * The window runs the request itself (Dialog's `action`): it waits shut,
 * prints a refusal over its buttons or under the field the server named, and
 * closes on success alone. A typed explanation is asked about before it is
 * dropped.
 */
import { computed, ref } from "vue";
import Dialog from "@/shared/ui/Layout/Dialog.vue";
import DialogTitle from "@/shared/ui/Layout/DialogTitle.vue";
import Form from "@/shared/ui/Form/Form.vue";
import FormField from "@/shared/ui/Form/FormField.vue";
import { Select, type SelectOption } from "@/shared/ui/Select";
import { moderationApi, type Warning } from "@/entities/moderation";

const props = defineProps<{
  /** The warning being corrected. */
  warningId: string;
  /** Points it currently carries — where the field starts. */
  currentPoints: number;
}>();

const emit = defineEmits<{
  (e: "success", warning: Warning | null): void;
  (e: "cancel"): void;
}>();

/** The whole scale, zero included: zero is a correction, not an absence of one. */
const pointsOptions: SelectOption[] = ["0", "1", "2", "3", "4", "5", "6"].map(
  (value) => ({ value, label: value }),
);

const points = ref(String(props.currentPoints));
const reason = ref("");

const reasonTooLong = computed(() => reason.value.length > 2000);
const canSubmit = computed(
  () => reason.value.trim().length > 0 && !reasonTooLong.value,
);

/**
 * The window's action. An empty or overlong explanation never gets here: the
 * main button is shut by `canSubmit`, and a shut button submits nothing.
 */
async function send() {
  const result = await moderationApi.changeWarningPoints(
    props.warningId,
    { points: Number(points.value), reason: reason.value.trim() },
    { ownsRefusal: true, ownsRateLimit: true },
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
