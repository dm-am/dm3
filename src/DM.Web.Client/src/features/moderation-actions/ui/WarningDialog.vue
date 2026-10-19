<script setup lang="ts">
// The server keeps its own copy of the violating text; the window only quotes it.
import { ref, computed, onMounted } from "vue";
import Dialog from "@/shared/ui/Layout/Dialog.vue";
import DialogTitle from "@/shared/ui/Layout/DialogTitle.vue";
import Form from "@/shared/ui/Form/Form.vue";
import FormField from "@/shared/ui/Form/FormField.vue";
import { Select, type SelectOption } from "@/shared/ui/Select";
import { StatLine } from "@/shared/ui/StatLine";
import { BBCodeEditor } from "@/shared/ui/BBCodeEditor";
import { useToast } from "@/shared/lib/composables/useToast";
import { trimHtmlWhitespace } from "@/shared/lib/utils/trimHtmlWhitespace";
import { CommentText } from "@/features/comment/@x/moderation-actions";
import { warningApi, type Warning } from "@/entities/moderation";
import type { Username } from "@/shared/api/models/common";
import moderationActionsApi, { WARNING_LIMIT } from "../api";
import { OWNS_REFUSAL } from "@/shared/api/interceptor";
import { REASON_MAX_LENGTH } from "../lib/limits";

const props = defineProps<{
  username: string;
  entityId?: string;
  entityType?: string;
  entityLink?: string;
  /** Without it the window asks for a topic's and a chat message's text itself. */
  evidence?: string;
  number?: number;
  /** For an opener whose own screen shows the issued warning (the profile). */
  hideToast?: boolean;
}>();

const emit = defineEmits<{
  (e: "success", warning: Warning | null): void;
  (e: "cancel"): void;
}>();

const toast = useToast();

const pointsOptions: SelectOption[] = Array.from(
  { length: WARNING_LIMIT + 1 },
  (_, value) => ({
    value: String(value),
    label: value === 0 ? "0 (устное предупреждение)" : String(value),
  }),
);

const points = ref("1");
const reason = ref("");

const reasonTooLong = computed(() => reason.value.length > REASON_MAX_LENGTH);
const canSubmit = computed(
  () => reason.value.trim().length > 0 && !reasonTooLong.value,
);

const currentPoints = ref<number | null>(null);
const pointsFailed = ref(false);

const quoted = ref(trimHtmlWhitespace(props.evidence));

/** The text set in a quote frame: the one thing the window adds to a comment's text. */
const quoteHtml = computed(() => `<div class="quote">${quoted.value}</div>`);

// The fields wait for the quote above them, so they do not move under the hand.
const quoteLoading = ref(!quoted.value && !!props.entityId);

onMounted(async () => {
  if (quoteLoading.value) {
    void moderationActionsApi
      .getQuotedText(props.entityType, props.entityId)
      .then((text) => (quoted.value = trimHtmlWhitespace(text)))
      .finally(() => (quoteLoading.value = false));
  }

  const { data } = await warningApi.getWarnings(props.username as Username);
  if (data) currentPoints.value = data.totalPoints;
  else pointsFailed.value = true;
});

async function send() {
  const result = await moderationActionsApi.createWarning(
    {
      username: props.username,
      points: Number(points.value),
      reason: reason.value,
      entityId: props.entityId,
      entityType: props.entityType,
    },
    OWNS_REFUSAL,
  );
  if (!result.error) {
    if (!props.hideToast) toast.success("Предупреждение выдано");
    emit("success", result.data?.resource ?? null);
  }
  return result;
}
</script>

<template>
  <Dialog
    v-slot="{ busy }"
    editor
    :action="send"
    failure="Не удалось выдать предупреждение"
    :dirty="reason.trim().length > 0"
    discard-text="Набранная причина пропадет"
    @close="emit('cancel')"
  >
    <DialogTitle>Создание предупреждения</DialogTitle>

    <div class="warn-context">
      <!-- The second line keeps its height while the points load. -->
      <div class="warn-person">
        <StatLine
          label="Нарушитель"
          :value="username"
          :to="{ name: 'profile', params: { username } }"
        />
        <div class="warn-points">
          <StatLine
            v-if="currentPoints !== null"
            label="Нарушения"
            :value="`${currentPoints}/${WARNING_LIMIT}`"
            :variant="currentPoints > 0 ? 'negative' : 'default'"
          />
          <StatLine
            v-else-if="pointsFailed"
            label="Нарушения"
            value="не удалось загрузить"
            variant="muted"
          />
        </div>
      </div>

      <!-- The text of the comment as the comment draws it, in a quote frame. -->
      <div v-if="quoted" class="warn-evidence">
        <CommentText :html="quoteHtml" />
        <div v-if="number && entityLink" class="warn-evidence-footer">
          <a
            :href="entityLink"
            target="_blank"
            rel="noopener"
            class="warn-number"
            :aria-label="`Комментарий ${number}`"
            >{{ number }}</a
          >
        </div>
      </div>
    </div>

    <Form
      v-if="!quoteLoading"
      :valid="canSubmit"
      action="Выдать"
      cancel="Отмена"
    >
      <FormField label="Баллы" name="warning-points" field="points">
        <Select
          id="warning-points"
          :model-value="points"
          :options="pointsOptions"
          @update:model-value="(v) => (points = v)"
        />
      </FormField>

      <FormField
        label="Причина предупреждения"
        name="warning-reason"
        field="reason"
        :errors="reasonTooLong ? ['Слишком длинное значение'] : []"
      >
        <BBCodeEditor
          v-model="reason"
          context="common"
          placeholder="Опишите нарушение..."
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
@use "@/assets/styles/CommentParts" as *

.warn-context
  display: flex
  flex-direction: column
  gap: $medium
  margin-bottom: $medium

.warn-person
  display: flex
  flex-direction: column
  gap: $minor

.warn-points
  min-height: calc($font-size * 1.25)

// The quote frame stands flush with the window's fields, without the margin of a quote in a text.
.warn-evidence :deep(.bbcode-content > .quote)
  margin: 0

.warn-evidence-footer
  display: flex
  margin-top: $small

.warn-number
  +comment-number
</style>
