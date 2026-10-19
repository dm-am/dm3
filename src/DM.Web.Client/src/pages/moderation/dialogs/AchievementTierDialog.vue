<script setup lang="ts">
/** Creates or edits a tier; the code is editable on create only. The window runs the request. */
import { ref, computed } from "vue";
import {
  achievementApi,
  CATALOG_CODE_MAX_LENGTH,
  CATALOG_TITLE_MAX_LENGTH,
  MIN_TIER,
  MAX_TIER,
  TIER_SCALE_HINT,
} from "@/entities/achievement";
import type { AchievementType } from "@/shared/api/models/achievements";
import Dialog from "@/shared/ui/Layout/Dialog.vue";
import DialogTitle from "@/shared/ui/Layout/DialogTitle.vue";
import Form from "@/shared/ui/Form/Form.vue";
import FormField from "@/shared/ui/Form/FormField.vue";
import { OWNS_REFUSAL } from "@/shared/api/interceptor";

const props = defineProps<{
  /** Tier to edit; null creates a new tier in the given category. */
  tier: AchievementType | null;
  /** Category the (new) tier belongs to. */
  categoryId: string;
  /** Suggested tier for a new tier; none, or one off the scale, leaves the field empty. */
  defaultTier: number | null;
}>();

const emit = defineEmits<{
  (e: "success"): void;
  (e: "cancel"): void;
}>();

const code = ref(props.tier?.code ?? "");
const title = ref(props.tier?.title ?? "");
const threshold = ref(props.tier?.threshold ?? 0);
const onScale = (n: number | null): n is number =>
  n !== null && n >= MIN_TIER && n <= MAX_TIER;
const tierNumber = ref<number | null>(
  props.tier?.tier ?? (onScale(props.defaultTier) ? props.defaultTier : null),
);
/** A cleared number field reads as "", which goes out as no value. */
const tierSent = () =>
  typeof tierNumber.value === "number" ? tierNumber.value : null;

const canSubmit = computed(
  () =>
    title.value.trim().length > 0 &&
    threshold.value >= 1 &&
    (props.tier !== null || code.value.trim().length > 0),
);

async function send() {
  const flags = OWNS_REFUSAL;
  const result = props.tier
    ? await achievementApi.updateAchievementType(
        props.tier.id,
        {
          title: title.value,
          threshold: threshold.value,
          tier: tierSent(),
        },
        flags,
      )
    : await achievementApi.createAchievementType(
        {
          code: code.value,
          title: title.value,
          threshold: threshold.value,
          tier: tierSent(),
          achievementCategoryId: props.categoryId,
        },
        flags,
      );
  if (!result.error) emit("success");
  return result;
}
</script>

<template>
  <Dialog
    :action="send"
    failure="Не удалось сохранить тир"
    @close="emit('cancel')"
  >
    <DialogTitle>{{ tier ? `Тир: ${tier.code}` : "Новый тир" }}</DialogTitle>

    <Form :valid="canSubmit" action="Сохранить" cancel="Отмена">
      <FormField v-if="!tier" label="Код" name="tier-code" field="code">
        <input
          id="tier-code"
          v-model="code"
          type="text"
          required
          :maxlength="CATALOG_CODE_MAX_LENGTH"
        />
        <template #hint>Не меняется, например POSTS_100</template>
      </FormField>

      <FormField label="Название" name="tier-title" field="title">
        <input
          id="tier-title"
          v-model="title"
          type="text"
          required
          :maxlength="CATALOG_TITLE_MAX_LENGTH"
        />
      </FormField>

      <FormField label="Порог" name="tier-threshold" field="threshold">
        <input
          id="tier-threshold"
          v-model.number="threshold"
          type="number"
          min="1"
          required
        />
      </FormField>

      <FormField label="Тир" name="tier-number" field="tier">
        <input
          id="tier-number"
          v-model.number="tierNumber"
          type="number"
          :min="MIN_TIER"
          :max="MAX_TIER"
        />
        <template #hint>{{ TIER_SCALE_HINT }}</template>
      </FormField>
    </Form>
  </Dialog>
</template>
