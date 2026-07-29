<script setup lang="ts">
/**
 * AttributeEditForm — inline editor for a single attribute specification.
 * Two-way bound via defineModel: it edits the spec object that lives inside
 * the parent draft, so changes reflect live in the table and preview. Rendered
 * in the region that otherwise shows the character-creation preview.
 */
import type { AttributeSpecification } from "@/entities/game";
import { AttributeSpecificationType } from "@/entities/game";
import FormField from "@/shared/ui/Form/FormField.vue";
import { Select } from "@/shared/ui/Select";
import Button from "@/shared/ui/Button/Button.vue";
import ValuesEditor from "./ValuesEditor.vue";
import {
  SPEC_TYPE_OPTIONS,
  usesMaxLength,
  usesValues,
  usesModifier,
  normalizeSpecForType,
} from "@/entities/game";

const spec = defineModel<AttributeSpecification>({ required: true });

defineEmits<{ done: [] }>();

function onTypeChange(value: string) {
  spec.value.type = value as AttributeSpecificationType;
  normalizeSpecForType(spec.value);
}
</script>

<template>
  <div class="attribute-edit">
    <div class="edit-header">Настройка атрибута</div>

    <form-field label="Название" name="attr-title">
      <input
        v-model="spec.title"
        type="text"
        placeholder="Например, Сила"
        maxlength="100"
      />
    </form-field>

    <form-field label="Тип">
      <Select
        :model-value="spec.type"
        :options="SPEC_TYPE_OPTIONS"
        @update:model-value="onTypeChange"
      />
    </form-field>

    <form-field
      v-if="usesMaxLength(spec.type)"
      label="Максимальная длина"
      name="attr-maxlength"
      optional
    >
      <input
        v-model.number="spec.maxLength"
        type="number"
        min="1"
        placeholder="Без ограничения"
      />
    </form-field>

    <form-field v-if="usesValues(spec.type)" label="Список значений">
      <ValuesEditor
        :model-value="spec.values ?? []"
        :with-modifier="usesModifier(spec.type)"
        @update:model-value="spec.values = $event"
      />
    </form-field>

    <div class="edit-actions">
      <Button type="button" @click="$emit('done')">Готово</Button>
    </div>
  </div>
</template>

<style scoped lang="sass">
.attribute-edit
  padding: $medium
  background-color: $bg-element-overlay
  border: 1px solid $border
  border-radius: $border-radius

.edit-header
  font-weight: bold
  margin-bottom: $small

.edit-actions
  margin-top: $medium
</style>
