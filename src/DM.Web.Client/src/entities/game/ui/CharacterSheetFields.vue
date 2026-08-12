<script setup lang="ts">
/**
 * CharacterSheetFields — schema-driven per-specification controls.
 *
 * The single source of truth for how a character sheet's attribute inputs are
 * rendered: one control per specification, chosen by its Type. Shared by the
 * real create/edit form (CharacterForm) and the schema-editor live preview
 * (CharacterFormPreview) so the two can never drift apart.
 *
 *  - Text            -> text FormField (maxlength = MaxLength, none when unset)
 *  - Number          -> numeric FormField (digits + optional leading minus)
 *  - TextList / NumberList / TextNumberList -> Select (TextNumberList shows the
 *    text with its signed modifier)
 *  - BbCode          -> BBCodeEditor (dual-mode; raw BBCode is the source of truth)
 *
 * Values are read from the `values` record (keyed by spec id) and changes are
 * emitted via `update` — the owning form keeps and mutates the record.
 * Validation is display-only here: the caller supplies `specErrors`; form
 * validity and submission stay in the caller.
 */
import {
  AttributeSpecificationType,
  type AttributeSpecification,
  type AttributeValueSpecification,
} from "../model/types";
import { defineAsyncComponent } from "vue";
import { FormField } from "@/shared/ui/Form";
import { Select, type SelectOption } from "@/shared/ui/Select";
import { SvgIcon } from "@/shared/ui/Icon";

/**
 * Loaded on demand: BBCodeEditor pulls TipTap (360 KB), and this component is
 * exported from the entities/game barrel, which the left sidebar imports on
 * every page. A static import here put the whole editor on the entry chunk —
 * every visitor, including guests on the home page, downloaded it. Only sheets
 * with a BbCode specification ever render it.
 */
const BBCodeEditor = defineAsyncComponent(() =>
  import("@/shared/ui/BBCodeEditor").then((m) => m.BBCodeEditor),
);

withDefaults(
  defineProps<{
    /** Specifications to render, already in the desired order */
    specs: AttributeSpecification[];
    /** Raw value per specification id (read-only here) */
    values: Record<string, string>;
    /** Display-only errors for a spec (empty by default) */
    specErrors?: (spec: AttributeSpecification) => string[];
  }>(),
  {
    specErrors: () => [],
  },
);

const emit = defineEmits<{
  (e: "update", specId: string, value: string): void;
}>();

const Type = AttributeSpecificationType;

function optionLabel(
  spec: AttributeSpecification,
  v: AttributeValueSpecification,
): string {
  if (spec.type === Type.TextNumberList && v.modifier != null) {
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

    <!-- Text -->
    <input
      v-if="spec.type === Type.Text"
      :id="`spec-${spec.id}`"
      :value="values[spec.id]"
      type="text"
      :maxlength="spec.maxLength ?? undefined"
      @input="onTextInput(spec.id, $event)"
    />

    <!-- Number -->
    <input
      v-else-if="spec.type === Type.Number"
      :id="`spec-${spec.id}`"
      :value="values[spec.id]"
      type="text"
      inputmode="numeric"
      :maxlength="spec.maxLength ?? undefined"
      @input="onNumberInput(spec.id, $event)"
    />

    <!-- BbCode -->
    <BBCodeEditor
      v-else-if="spec.type === Type.BbCode"
      :model-value="values[spec.id]"
      context="info"
      :max-length="spec.maxLength ?? 0"
      @update:model-value="emit('update', spec.id, $event)"
    />

    <!-- Lists (TextList / NumberList / TextNumberList) -->
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

.privacy-indicator
  display: inline-flex
  align-items: center
  gap: $tiny
  flex-shrink: 0
  color: $text-muted
  font-size: $secondary-font-size

.privacy-icon
  width: 12px
  height: 12px
</style>
