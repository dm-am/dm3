<script setup lang="ts">
// Shared by the form and the schema editor's preview, so the two cannot drift apart.
import {
  AttributeSpecificationType,
  type AttributeSpecification,
  type AttributeValueSpecification,
} from "../model/gameTypes";
import { defineAsyncComponent } from "vue";
import { FormField } from "@/shared/ui/Form";
import { Select, type SelectOption } from "@/shared/ui/Select";
import { SvgIcon } from "@/shared/ui/Icon";

// On demand: the sidebar imports the game barrel on every page, and the editor pulls TipTap.
const BBCodeEditor = defineAsyncComponent(() =>
  import("@/shared/ui/BBCodeEditor").then((m) => m.BBCodeEditor),
);

withDefaults(
  defineProps<{
    specs: AttributeSpecification[];
    /** Per specification id; the form keeps and changes it. */
    values: Record<string, string>;
    specErrors?: (spec: AttributeSpecification) => string[];
  }>(),
  {
    specErrors: () => [],
  },
);

const emit = defineEmits<{
  (e: "update", specId: string, value: string): void;
}>();

function optionLabel(
  spec: AttributeSpecification,
  v: AttributeValueSpecification,
): string {
  if (
    spec.type === AttributeSpecificationType.TextNumberList &&
    v.modifier != null
  ) {
    const sign = v.modifier > 0 ? "+" : "";
    return `${v.value} (${sign}${v.modifier})`;
  }
  return v.value;
}

function selectOptions(spec: AttributeSpecification): SelectOption[] {
  return (spec.values ?? []).map((v) => ({
    value: v.value,
    label: optionLabel(spec, v),
  }));
}

function onTextInput(specId: string, event: Event) {
  emit("update", specId, (event.target as HTMLInputElement).value);
}

function onNumberInput(specId: string, event: Event) {
  const el = event.target as HTMLInputElement;
  // Number specs accept digits with an optional leading minus.
  const cleaned = el.value.replace(/(?!^)-|[^\d-]/g, "");
  el.value = cleaned;
  emit("update", specId, cleaned);
}
</script>

<template>
  <FormField
    v-for="spec in specs"
    :key="spec.id"
    :name="`spec-${spec.id}`"
    :errors="specErrors(spec)"
  >
    <template #label>
      <label :for="`spec-${spec.id}`">
        {{ spec.title }}
        <span v-if="spec.required" class="required-mark" aria-hidden="true"
          >*</span
        >
      </label>
      <span v-if="spec.isHidden" class="privacy-indicator" title="Приватность">
        <SvgIcon name="locked" class="privacy-icon" />
        Приватность
      </span>
    </template>

    <input
      v-if="spec.type === AttributeSpecificationType.Text"
      :id="`spec-${spec.id}`"
      :value="values[spec.id]"
      type="text"
      :maxlength="spec.maxLength ?? undefined"
      @input="onTextInput(spec.id, $event)"
    />

    <input
      v-else-if="spec.type === AttributeSpecificationType.Number"
      :id="`spec-${spec.id}`"
      :value="values[spec.id]"
      type="text"
      inputmode="numeric"
      :maxlength="spec.maxLength ?? undefined"
      @input="onNumberInput(spec.id, $event)"
    />

    <BBCodeEditor
      v-else-if="spec.type === AttributeSpecificationType.BbCode"
      :model-value="values[spec.id]"
      context="info"
      :max-length="spec.maxLength ?? 0"
      @update:model-value="emit('update', spec.id, $event)"
    />

    <Select
      v-else
      :id="`spec-${spec.id}`"
      :model-value="values[spec.id]"
      :options="selectOptions(spec)"
      placeholder="Не выбрано"
      @update:model-value="emit('update', spec.id, $event)"
    />
  </FormField>
</template>

<style scoped lang="sass">
.required-mark
  color: $accent-red

// A note beside the bold caption keeps the regular weight.
.privacy-indicator
  display: inline-flex
  align-items: center
  gap: $tiny
  flex-shrink: 0
  color: $text-muted
  font-size: $secondary-font-size
  font-weight: 400

.privacy-icon
  width: 12px
  height: 12px
</style>
