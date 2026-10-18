<script setup lang="ts">
/**
 * AchievementTierDialog — create or edit an achievement tier (code /
 * title / threshold / tier). Dialog twin of the former hand-rolled
 * modal in ModerationAchievements.vue. Code is only editable on create.
 *
 * The window runs the request (Dialog `action`): it waits for the answer,
 * prints a refusal in itself and closes on success alone.
 */
import { ref, computed } from "vue";
import { achievementApi } from "@/entities/achievement";
import type { AchievementType } from "@/shared/api/models/achievements";
import Dialog from "@/shared/ui/Layout/Dialog.vue";
import DialogTitle from "@/shared/ui/Layout/DialogTitle.vue";
import Form from "@/shared/ui/Form/Form.vue";
import FormField from "@/shared/ui/Form/FormField.vue";

const props = defineProps<{
  /** Tier to edit; null creates a new tier in the given category. */
  tier: AchievementType | null;
  /** Category the (new) tier belongs to. */
  categoryId: string;
  /** Suggested tier number for a new tier. */
  defaultTier: number;
}>();

const emit = defineEmits<{
  (e: "success"): void;
  (e: "cancel"): void;
}>();

const code = ref(props.tier?.code ?? "");
const title = ref(props.tier?.title ?? "");
const threshold = ref(props.tier?.threshold ?? 0);
const tierNumber = ref(props.tier?.tier ?? props.defaultTier);

const canSubmit = computed(
  () =>
    title.value.trim().length > 0 &&
    threshold.value >= 1 &&
    (props.tier !== null || code.value.trim().length > 0),
);

async function send() {
  const flags = { ownsRefusal: true, ownsRateLimit: true };
  const result = props.tier
    ? await achievementApi.updateAchievementType(
        props.tier.id,
        {
          title: title.value,
          threshold: threshold.value,
          tier: tierNumber.value,
        },
        flags,
      )
    : await achievementApi.createAchievementType(
        {
          code: code.value,
          title: title.value,
          threshold: threshold.value,
          tier: tierNumber.value,
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
      <FormField
        v-if="!tier"
        label="Code (стабильный, например POSTS_100)"
        name="tier-code"
        field="code"
      >
        <input
          id="tier-code"
          v-model="code"
          type="text"
          required
          maxlength="80"
        />
      </FormField>

      <FormField label="Название" name="tier-title" field="title">
        <input
          id="tier-title"
          v-model="title"
          type="text"
          required
          maxlength="200"
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

      <FormField label="Tier (1-4)" name="tier-number" field="tier">
        <input
          id="tier-number"
          v-model.number="tierNumber"
          type="number"
          min="1"
          max="9"
        />
      </FormField>
    </Form>
  </Dialog>
</template>
