<script setup lang="ts">
/**
 * BanDialog — "Создание бана" (product doc 4.2.4.2).
 *
 * Dialog dialog for issuing a ban, triggered from the user moderation
 * block on the profile page. SeniorModerator+ only — the opener gates the
 * trigger, the backend enforces it again ([RequireRole(SeniorModerator)]).
 *
 * Doc contract:
 * - Ban scope ("Охват бана", access policy): "Демократический" / "Полный" —
 *   SEPARATE from the duration. Sent as `accessPolicy`; the backend
 *   (BanService.CreateBan) persists DemocraticBan/FullBan on the ban and the
 *   effective user AccessPolicy aggregates it. Any other value is coerced to
 *   FullBan server-side.
 * - Duration ("Срок"): "1-7 дней" / "2, 4 недели" / "2, 3, 6, 12 месяцев" /
 *   "Бессрочный". "Бессрочный" is encoded as a 100-year durationHours
 *   because the backend validator requires a duration for non-voluntary
 *   bans and the domain itself stores permanent bans as now + 100 years.
 * - Reason ("Причина"): required, max 2000 (mirrors CreateBanValidator).
 *
 * The window runs the request itself (Dialog's `action`): it waits shut, the
 * editor included, prints a refusal over its buttons or under the field the
 * server named (409 is the user already banned), and closes on success alone.
 * A typed reason is asked about before it is dropped.
 */
import { ref, computed } from "vue";
import Dialog from "@/shared/ui/Layout/Dialog.vue";
import DialogTitle from "@/shared/ui/Layout/DialogTitle.vue";
import Form from "@/shared/ui/Form/Form.vue";
import FormField from "@/shared/ui/Form/FormField.vue";
import { Select } from "@/shared/ui/Select";
import { BBCodeEditor } from "@/shared/ui/BBCodeEditor";
import moderationActionsApi, {
  PERMANENT_BAN_HOURS,
  type BanAccessPolicy,
  type BanResult,
} from "../api";

const props = defineProps<{
  /** Target user (violator) username. */
  username: string;
}>();

const emit = defineEmits<{
  (e: "success", ban: BanResult | null): void;
  (e: "cancel"): void;
}>();

// --- Ban scope ("Охват бана", access policy) ---
const policyOptions: { value: BanAccessPolicy; label: string }[] = [
  { value: "DemocraticBan", label: "Демократический" },
  { value: "FullBan", label: "Полный" },
];
const policy = ref<BanAccessPolicy>("DemocraticBan");

// --- Duration ("Срок") — values are durationHours as strings for <select> ---
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

// Mirror of the backend rules: comment required + max 2000, duration > 0.
const commentTooLong = computed(() => comment.value.length > 2000);
const canSubmit = computed(
  () =>
    comment.value.trim().length > 0 &&
    !commentTooLong.value &&
    durationHours.value > 0,
);

/**
 * The window's action. A form that breaks the rules above never gets here:
 * the main button is shut by `canSubmit`, and a shut button submits nothing.
 */
async function send() {
  const result = await moderationActionsApi.createBan(
    {
      username: props.username,
      durationHours: durationHours.value,
      comment: comment.value.trim(),
      accessPolicy: policy.value,
    },
    { ownsRefusal: true, ownsRateLimit: true },
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

    <!-- Violator context (prefilled from the profile page) -->
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
