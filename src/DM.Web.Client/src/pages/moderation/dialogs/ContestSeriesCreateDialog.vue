<script setup lang="ts">
/** Creates a contest series, suggesting the next number of the type. The window runs the request. */
import { ref, computed, watch } from "vue";
import {
  achievementApi,
  CONTEST_SERIES_MIN_YEAR,
  CONTEST_SERIES_YEARS_AHEAD,
} from "@/entities/achievement";
import { siteTime } from "@/shared/lib/utils/datetime";
import {
  ContestType,
  type ContestSeries,
} from "@/shared/api/models/achievements";
import Dialog from "@/shared/ui/Layout/Dialog.vue";
import DialogTitle from "@/shared/ui/Layout/DialogTitle.vue";
import Form from "@/shared/ui/Form/Form.vue";
import FormField from "@/shared/ui/Form/FormField.vue";
import { Select, type SelectOption } from "@/shared/ui/Select";
import { YearInput } from "@/shared/ui/DatePicker";
import { OWNS_REFUSAL } from "@/shared/api/interceptor";

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
const year = ref<number | null>(siteTime().year());
/** The latest year the server takes: one past the current. */
const latestYear = siteTime().year() + CONTEST_SERIES_YEARS_AHEAD;
/** What the year field says is wrong with it, shown under it. */
const yearReason = ref<string | null>(null);
const topicUrl = ref("");

function suggestNumber(type: ContestType): number {
  const maxN = props.series
    .filter((s) => s.contestType === type)
    .reduce((m, s) => Math.max(m, s.number), 0);
  return maxN + 1;
}

// The next number for the type chosen first and on every change
number.value = suggestNumber(contestType.value);
watch(contestType, (type) => {
  number.value = suggestNumber(type);
});

// A year of four digits the server takes; the field says why it takes nothing else.
const canSubmit = computed(
  () => number.value >= 1 && year.value !== null && !yearReason.value,
);

/**
 * Enter sends the form as "Создать" does, so it is shut with the button: a
 * year the field did not take would otherwise go out as the last one it did.
 */
function holdEnter(e: KeyboardEvent) {
  if (!canSubmit.value) e.preventDefault();
}

async function send() {
  const result = await achievementApi.createContestSeries(
    {
      contestType: contestType.value,
      number: number.value,
      year: year.value!,
      topicUrl: topicUrl.value.trim() || null,
    },
    OWNS_REFUSAL,
  );
  if (!result.error) emit("success");
  return result;
}
</script>

<template>
  <Dialog
    :action="send"
    failure="Не удалось создать серию"
    @close="emit('cancel')"
  >
    <DialogTitle>Новая серия конкурса</DialogTitle>

    <Form
      :valid="canSubmit"
      action="Создать"
      cancel="Отмена"
      @keydown.enter="holdEnter"
    >
      <FormField label="Тип" name="series-type" field="contestType">
        <Select
          id="series-type"
          :model-value="contestType"
          :options="typeOptions"
          @update:model-value="(v) => (contestType = v as ContestType)"
        />
      </FormField>

      <FormField label="Номер" name="series-number" field="number">
        <input
          id="series-number"
          v-model.number="number"
          type="number"
          min="1"
          required
        />
      </FormField>

      <FormField
        label="Год"
        name="series-year"
        field="year"
        :errors="yearReason ? [yearReason] : []"
      >
        <YearInput
          id="series-year"
          v-model="year"
          :min="CONTEST_SERIES_MIN_YEAR"
          :max="latestYear"
          @reason="yearReason = $event"
        />
      </FormField>

      <FormField
        label="Топик с итогами"
        name="series-topic"
        field="topicUrl"
        optional
      >
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
