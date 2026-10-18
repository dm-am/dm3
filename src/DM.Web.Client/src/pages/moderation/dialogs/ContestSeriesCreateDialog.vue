<script setup lang="ts">
/**
 * ContestSeriesCreateDialog — create a contest series (type / number /
 * year / results topic). Dialog twin of the former hand-rolled modal
 * in ModerationAwards.vue. Suggests number = max + 1 within the chosen
 * contest type.
 *
 * The window runs the request (Dialog `action`): it waits for the answer,
 * prints a refusal in itself - a series with this number and year already
 * there as well as a refusal the interceptor used to say in a plate over it -
 * and closes on success alone.
 */
import { ref, computed, watch } from "vue";
import { achievementApi } from "@/entities/achievement";
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
/** What the year field says is wrong with it, shown under it. */
const yearReason = ref<string | null>(null);
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

// The year field takes only a year of four digits from 2000 to 2100, and
// says why it does not take anything else.
const canSubmit = computed(
  () => number.value >= 1 && year.value !== null && !yearReason.value,
);

/**
 * Enter in a field sends the form as "Создать" does, and the window runs its
 * action whatever the button says. While the button is shut, so is Enter: a
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
    { ownsRefusal: true, ownsRateLimit: true },
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
          :min="2000"
          :max="2100"
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
