<script setup lang="ts">
/**
 * WarningDialog — "Создание предупреждения" (product doc 4.2.4.1).
 *
 * Dialog dialog for issuing a moderator warning to a user. Opened from
 * warn buttons on topics/comments/messages/profiles with the target user
 * and a link to the violating content prefilled.
 *
 * The doc calls for a non-modal window; the shared Dialog (vue-final-modal)
 * is modal (locks scroll) — the site-wide dialog idiom is used deliberately,
 * see the integration notes.
 *
 * Validation mirrors the backend CreateWarningValidator: points 0-6
 * (0 = verbal warning), reason required and max 2000 chars.
 *
 * The violation object itself is referenced by entityId/entityType (the
 * server resolves the block copy); the human-readable link is shown for
 * the moderator's context only and is not part of the payload.
 */
import { ref, computed, onMounted } from "vue";
import Dialog from "@/shared/ui/Layout/Dialog.vue";
import DialogTitle from "@/shared/ui/Layout/DialogTitle.vue";
import Form from "@/shared/ui/Form/Form.vue";
import FormField from "@/shared/ui/Form/FormField.vue";
import { Select } from "@/shared/ui/Select";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { BBCodeEditor } from "@/shared/ui/BBCodeEditor";
import { useToast } from "@/shared/lib/composables/useToast";
import { parseApiErrors, getFieldError } from "@/shared/lib/utils/apiErrors";
import type { BadRequestError } from "@/shared/api/models/common";
import moderationActionsApi, { type WarningResult } from "../api";
import { notifyFailure } from "@/shared/lib/errors";

const props = defineProps<{
  /** Target user (violator) username. */
  username: string;
  /** Id of the violating entity (comment, topic, message...). */
  entityId?: string;
  /** Entity type name for the backend ("Comment", "Topic", "Message"...). */
  entityType?: string;
  /** Human-readable link to the violating content (context display only). */
  entityLink?: string;
}>();

const emit = defineEmits<{
  (e: "success", warning: WarningResult | null): void;
  (e: "cancel"): void;
}>();

const toast = useToast();

// --- Warning type + points (doc: "Устное предупреждение (0 баллов)" /
// "Предупреждение (1-6 баллов)") ---
const WARNING_KIND_VERBAL = "verbal";
const WARNING_KIND_SCORED = "scored";

const kindOptions = [
  { value: WARNING_KIND_VERBAL, label: "Устное предупреждение (0 баллов)" },
  { value: WARNING_KIND_SCORED, label: "Предупреждение (1-6 баллов)" },
];

const pointsOptions = ["1", "2", "3", "4", "5", "6"].map((v) => ({
  value: v,
  label: v,
}));

const kind = ref(WARNING_KIND_SCORED);
const points = ref("1");
const reason = ref("");
const sending = ref(false);
const fieldErrors = ref<Record<string, string[]>>({});

// Effective points sent to the backend: verbal warning is always 0.
const effectivePoints = computed(() =>
  kind.value === WARNING_KIND_VERBAL ? 0 : parseInt(points.value, 10),
);

// Mirror of the backend rules (points 0-6, reason required + max 2000).
const reasonTooLong = computed(() => reason.value.length > 2000);
const canSubmit = computed(
  () =>
    reason.value.trim().length > 0 &&
    !reasonTooLong.value &&
    effectivePoints.value >= 0 &&
    effectivePoints.value <= 6,
);

// --- "Баллы: N/6" context (current active points of the target user) ---
const currentPoints = ref<number | null>(null);

onMounted(async () => {
  const { data } = await moderationActionsApi.getUserWarnings(props.username);
  if (data) currentPoints.value = data.totalPoints;
});

async function submit() {
  if (!canSubmit.value || sending.value) return;

  sending.value = true;
  fieldErrors.value = {};

  const { data, error } = await moderationActionsApi.createWarning({
    username: props.username,
    points: effectivePoints.value,
    reason: reason.value.trim(),
    entityId: props.entityId,
    entityType: props.entityType,
  });

  sending.value = false;

  if (error) {
    fieldErrors.value = parseApiErrors(error as BadRequestError);
    if (!Object.keys(fieldErrors.value).length) {
      notifyFailure(error, "Не удалось отправить предупреждение");
    }
    return;
  }

  toast.success("Предупреждение отправлено");
  emit("success", data?.resource ?? null);
}

function reasonErrors(): string[] {
  const server = getFieldError(fieldErrors.value, "reason");
  if (server) return [server];
  if (reasonTooLong.value) return ["Слишком длинное значение"];
  return [];
}

function pointsErrors(): string[] {
  const server = getFieldError(fieldErrors.value, "points");
  return server ? [server] : [];
}
</script>

<template>
  <Dialog>
    <DialogTitle>Создание предупреждения</DialogTitle>

    <!-- Violation context: target user, current points, violation link -->
    <div class="warn-context">
      <div>
        Пользователь:
        <router-link :to="{ name: 'profile', params: { username } }">{{
          username
        }}</router-link>
      </div>
      <SecondaryText v-if="currentPoints !== null">
        Баллы: {{ currentPoints }}/6
      </SecondaryText>
      <div v-if="entityLink" class="warn-violation">
        Нарушение:
        <a :href="entityLink" target="_blank" rel="noopener">{{
          entityLink
        }}</a>
      </div>
    </div>

    <Form
      :valid="canSubmit"
      :loading="sending"
      action="Отправить предупреждение"
      cancel="Отменить"
      @submit="submit"
      @cancel="emit('cancel')"
    >
      <FormField label="Тип предупреждения" name="warning-kind">
        <Select
          :model-value="kind"
          :options="kindOptions"
          @update:model-value="(v) => (kind = v)"
        />
      </FormField>

      <FormField
        v-if="kind === WARNING_KIND_SCORED"
        label="Баллы"
        name="warning-points"
        :errors="pointsErrors()"
      >
        <Select
          :model-value="points"
          :options="pointsOptions"
          @update:model-value="(v) => (points = v)"
        />
      </FormField>

      <FormField
        label="Причина предупреждения"
        name="warning-reason"
        :errors="reasonErrors()"
      >
        <BBCodeEditor
          v-model="reason"
          context="common"
          placeholder="Опишите нарушение..."
          :disabled="sending"
          :min-height="100"
          :max-height="300"
          :is-moderator="true"
        />
      </FormField>
    </Form>
  </Dialog>
</template>

<style scoped lang="sass">
.warn-context
  display: flex
  flex-direction: column
  gap: $minor
  margin-bottom: $small

.warn-violation
  // Long permalinks must wrap inside the fixed-width dialog.
  overflow-wrap: anywhere
</style>
