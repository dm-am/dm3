<script setup lang="ts">
/**
 * AchievementTierDialog — create or edit an achievement tier (code /
 * title / threshold / tier). Dialog twin of the former hand-rolled
 * modal in ModerationAchievements.vue. Code is only editable on create.
 */
import { ref, computed } from "vue";
import { achievementApi } from "@/shared/api";
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

const saving = ref(false);
const error = ref<string | null>(null);

const canSubmit = computed(
  () =>
    title.value.trim().length > 0 &&
    threshold.value >= 1 &&
    (props.tier !== null || code.value.trim().length > 0),
);

async function submit() {
  if (!canSubmit.value || saving.value) return;
  saving.value = true;
  error.value = null;
  try {
    if (props.tier) {
      await achievementApi.updateAchievementType(props.tier.id, {
        title: title.value,
        threshold: threshold.value,
        tier: tierNumber.value,
      });
    } else {
      await achievementApi.createAchievementType({
        code: code.value,
        title: title.value,
        threshold: threshold.value,
        tier: tierNumber.value,
        achievementCategoryId: props.categoryId,
      });
    }
    emit("success");
  } catch {
    error.value = "Не удалось сохранить тир";
  } finally {
    saving.value = false;
  }
}
</script>

<template>
  <Dialog narrow>
    <DialogTitle>{{ tier ? `Тир: ${tier.code}` : "Новый тир" }}</DialogTitle>

    <Form
      :valid="canSubmit"
      :loading="saving"
      action="Сохранить"
      cancel="Отмена"
      @submit="submit"
      @cancel="emit('cancel')"
    >
      <FormField
        v-if="!tier"
        label="Code (стабильный, например POSTS_100)"
        name="tier-code"
        :errors="error ? [error] : []"
      >
        <input
          id="tier-code"
          v-model="code"
          type="text"
          required
          maxlength="80"
        />
      </FormField>

      <FormField
        label="Название"
        name="tier-title"
        :errors="tier && error ? [error] : []"
      >
        <input
          id="tier-title"
          v-model="title"
          type="text"
          required
          maxlength="200"
        />
      </FormField>

      <FormField label="Порог" name="tier-threshold">
        <input
          id="tier-threshold"
          v-model.number="threshold"
          type="number"
          min="1"
          required
        />
      </FormField>

      <FormField label="Tier (1-4)" name="tier-number">
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
