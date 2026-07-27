<script setup lang="ts">
/**
 * ContestSeriesCreateDialog — create a contest series (type / number /
 * year / results topic). Dialog twin of the former hand-rolled modal
 * in ModerationAwards.vue. Suggests number = max + 1 within the chosen
 * contest type.
 */
import { ref, computed, watch } from "vue";
import { achievementApi } from "@/shared/api";
import {
  ContestType,
  type ContestSeries,
} from "@/shared/api/models/achievements";
import Dialog from "@/shared/ui/Layout/Dialog.vue";
import DialogTitle from "@/shared/ui/Layout/DialogTitle.vue";
import Form from "@/shared/ui/Form/Form.vue";
import FormField from "@/shared/ui/Form/FormField.vue";
import { Select, type SelectOption } from "@/shared/ui/Select";

const props = defineProps<{
  /** Existing series — used to suggest the next number per type. */
  series: ContestSeries[];
}>();

const emit = defineEmits<{
  (e: "success"): void;
  (e: "cancel"): void;
}>();

const typeOptions: SelectOption[] = [
  { value: ContestType.Literary, label: "Литературный" },
  { value: ContestType.Art, label: "Художественный" },
];

const contestType = ref<ContestType>(ContestType.Literary);
const number = ref(1);
const year = ref(new Date().getFullYear());
const topicUrl = ref("");

function suggestNumber(type: ContestType): number {
  const maxN = props.series
    .filter((s) => s.contestType === type)
    .reduce((m, s) => Math.max(m, s.number), 0);
  return maxN + 1;
}

// Suggest the next number for the initially selected and re-selected type
number.value = suggestNumber(contestType.value);
watch(contestType, (type) => {
  number.value = suggestNumber(type);
});

const saving = ref(false);
const error = ref<string | null>(null);

const canSubmit = computed(
  () => number.value >= 1 && year.value >= 2000 && year.value <= 2100,
);

async function submit() {
  if (!canSubmit.value || saving.value) return;
  saving.value = true;
  error.value = null;
  const { error: e } = await achievementApi.createContestSeries({
    contestType: contestType.value,
    number: number.value,
    year: year.value,
    topicUrl: topicUrl.value.trim() || null,
  });
  saving.value = false;
  if (e) {
    error.value = e.title ?? "Не удалось создать серию";
    return;
  }
  emit("success");
}
</script>

<template>
  <Dialog narrow>
    <DialogTitle>Новая серия конкурса</DialogTitle>

    <Form
      :valid="canSubmit"
      :loading="saving"
      action="Создать"
      cancel="Отмена"
      @submit="submit"
      @cancel="emit('cancel')"
    >
      <FormField label="Тип" name="series-type" :errors="error ? [error] : []">
        <Select
          :model-value="contestType"
          :options="typeOptions"
          @update:model-value="(v) => (contestType = v as ContestType)"
        />
      </FormField>

      <FormField label="Номер" name="series-number">
        <input
          id="series-number"
          v-model.number="number"
          type="number"
          min="1"
          required
        />
      </FormField>

      <FormField label="Год" name="series-year">
        <input
          id="series-year"
          v-model.number="year"
          type="number"
          min="2000"
          max="2100"
          required
        />
      </FormField>

      <FormField label="Топик с итогами" name="series-topic" optional>
        <input
          id="series-topic"
          v-model="topicUrl"
          type="url"
          placeholder="https://..."
        />
      </FormField>
    </Form>
  </Dialog>
</template>
