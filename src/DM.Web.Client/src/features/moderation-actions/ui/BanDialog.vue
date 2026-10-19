<script setup lang="ts">
// SeniorModerator+ only: the opener gates the trigger and the server enforces it again.
import { ref, computed } from "vue";
import Dialog from "@/shared/ui/Layout/Dialog.vue";
import DialogTitle from "@/shared/ui/Layout/DialogTitle.vue";
import Form from "@/shared/ui/Form/Form.vue";
import FormField from "@/shared/ui/Form/FormField.vue";
import { Select } from "@/shared/ui/Select";
import { BBCodeEditor } from "@/shared/ui/BBCodeEditor";
import type { Ban } from "@/entities/moderation";
import moderationActionsApi, {
  PERMANENT_BAN_HOURS,
  type BanAccessPolicy,
} from "../api";
import { OWNS_REFUSAL } from "@/shared/api/interceptor";
import { REASON_MAX_LENGTH } from "../lib/limits";

const props = defineProps<{
  username: string;
}>();

const emit = defineEmits<{
  (e: "success", ban: Ban | null): void;
  (e: "cancel"): void;
}>();

const policyOptions: { value: BanAccessPolicy; label: string }[] = [
  { value: "DemocraticBan", label: "Демократический" },
  { value: "FullBan", label: "Полный" },
];
const policy = ref<BanAccessPolicy>("DemocraticBan");

// Values are durationHours as strings for the select.
const PERMANENT = "permanent";
const durationOptions = [
  { value: String(24), label: "1 день" },
  { value: String(2 * 24), label: "2 дня" },
  { value: String(3 * 24), label: "3 дня" },
  { value: String(4 * 24), label: "4 дня" },
  { value: String(5 * 24), label: "5 дней" },
  { value: String(6 * 24), label: "6 дней" },
  { value: String(7 * 24), label: "7 дней" },
  { value: String(14 * 24), label: "2 недели" },
  { value: String(28 * 24), label: "4 недели" },
  { value: String(60 * 24), label: "2 месяца" },
  { value: String(90 * 24), label: "3 месяца" },
  { value: String(180 * 24), label: "6 месяцев" },
  { value: String(365 * 24), label: "12 месяцев" },
  { value: PERMANENT, label: "Бессрочный" },
];
const duration = ref(String(24));

const comment = ref("");

const isPermanent = computed(() => duration.value === PERMANENT);
const durationHours = computed(() =>
  isPermanent.value ? PERMANENT_BAN_HOURS : parseInt(duration.value, 10),
);

const commentTooLong = computed(() => comment.value.length > REASON_MAX_LENGTH);
const canSubmit = computed(
  () =>
    comment.value.trim().length > 0 &&
    !commentTooLong.value &&
    durationHours.value > 0,
);

async function send() {
  const result = await moderationActionsApi.createBan(
    {
      username: props.username,
      durationHours: durationHours.value,
      comment: comment.value,
      accessPolicy: policy.value,
    },
    OWNS_REFUSAL,
  );
  if (!result.error) emit("success", result.data?.resource ?? null);
  return result;
}
</script>

<template>
  <Dialog
    v-slot="{ busy }"
    editor
    :action="send"
    failure="Не удалось выдать бан"
    :dirty="comment.trim().length > 0"
    discard-text="Набранная причина пропадет"
    @close="emit('cancel')"
  >
    <DialogTitle>Создание бана</DialogTitle>

    <div class="ban-context">
      Нарушитель:
      <router-link :to="{ name: 'profile', params: { username } }">{{
        username
      }}</router-link>
    </div>

    <Form :valid="canSubmit" action="Выдать бан" cancel="Отмена">
      <FormField label="Охват бана" name="ban-policy" field="accessPolicy">
        <Select
          id="ban-policy"
          :model-value="policy"
          :options="policyOptions"
          @update:model-value="(v) => (policy = v as BanAccessPolicy)"
        />
      </FormField>

      <FormField label="Срок" name="ban-duration" field="durationHours">
        <Select
          id="ban-duration"
          :model-value="duration"
          :options="durationOptions"
          @update:model-value="(v) => (duration = v)"
        />
      </FormField>

      <FormField
        label="Причина бана"
        name="ban-comment"
        field="comment"
        :errors="commentTooLong ? ['Слишком длинное значение'] : []"
      >
        <BBCodeEditor
          v-model="comment"
          context="common"
          placeholder="Опишите причину бана..."
          :disabled="busy"
          :min-height="100"
          :max-height="300"
          :is-moderator="true"
        />
      </FormField>
    </Form>
  </Dialog>
</template>

<style scoped lang="sass">
// One field step down to the first field, as between two fields.
.ban-context
  margin-bottom: $medium
</style>
