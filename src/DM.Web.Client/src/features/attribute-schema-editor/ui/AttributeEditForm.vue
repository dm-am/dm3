<script setup lang="ts">
// Edits the spec object inside the parent draft, so the table and preview follow live.
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
import { ATTRIBUTE_SCHEMA_TITLE_MAX_LENGTH } from "@/shared/lib/constants/game";

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
        id="attr-title"
        v-model="spec.title"
        type="text"
        placeholder="Например, Сила"
        :maxlength="ATTRIBUTE_SCHEMA_TITLE_MAX_LENGTH"
      />
    </form-field>

    <form-field label="Тип">
      <Select
        id="attr-type"
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
        id="attr-maxlength"
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
      <Button type="button" variant="primary" @click="$emit('done')">
        Готово
      </Button>
    </div>
  </div>
</template>

<style scoped lang="sass">
.attribute-edit
  padding: $medium
  background-color: $bg-element-overlay
  border: 1px solid $border

.edit-header
  font-weight: bold
  margin-bottom: $small

.edit-actions
  margin-top: $medium
</style>
