<script setup lang="ts">
import { ref, watch } from "vue";
import type { AttributeValueSpecification } from "@/entities/game";
import { RemoveCross } from "@/shared/ui/RemoveButton";

const props = defineProps<{
  modelValue: AttributeValueSpecification[] | null;
  withModifier: boolean;
}>();

const emit = defineEmits<{
  "update:modelValue": [AttributeValueSpecification[]];
}>();

interface Row {
  key: number;
  value: string;
  modifier: number | null;
}

let keyCounter = 0;
const rows = ref<Row[]>([]);

// Local rows hydrate from the model without echoing our own emits back into a reset.
let suppress = false;
watch(
  () => props.modelValue,
  (incoming) => {
    if (suppress) return;
    rows.value = (incoming ?? []).map((v) => ({
      key: ++keyCounter,
      value: v.value,
      modifier: v.modifier ?? null,
    }));
  },
  { immediate: true },
);

function emitChange() {
  suppress = true;
  emit(
    "update:modelValue",
    rows.value.map((r) => ({
      value: r.value,
      modifier: props.withModifier ? (r.modifier ?? null) : null,
    })),
  );
  // Release on next microtask so the prop watcher above ignores this echo.
  queueMicrotask(() => (suppress = false));
}

function addRow() {
  rows.value.push({ key: ++keyCounter, value: "", modifier: null });
  emitChange();
}

function removeRow(key: number) {
  const index = rows.value.findIndex((r) => r.key === key);
  if (index !== -1) rows.value.splice(index, 1);
  emitChange();
}
</script>

<template>
  <div class="values-editor">
    <div v-if="rows.length === 0" class="values-empty">Значений пока нет</div>

    <div v-for="(row, index) in rows" :key="row.key" class="value-row">
      <input
        v-model="row.value"
        type="text"
        class="value-input"
        :placeholder="`Значение ${index + 1}`"
        :aria-label="`Значение ${index + 1}`"
        @input="emitChange"
      />
      <input
        v-if="withModifier"
        v-model.number="row.modifier"
        type="number"
        class="modifier-input"
        placeholder="Мод."
        aria-label="Модификатор"
        @input="emitChange"
      />
      <RemoveCross label="Удалить значение" @click="removeRow(row.key)" />
    </div>

    <button type="button" class="add-btn" @click="addRow">
      + Добавить значение
    </button>
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Links" as *

.values-editor
  display: flex
  flex-direction: column
  gap: $small

.values-empty
  color: $text-muted
  font-size: $secondary-font-size

.value-row
  display: flex
  gap: $small

  .value-input
    flex: 1

  .modifier-input
    width: $grid-step * 20
    flex-shrink: 0

.add-btn
  align-self: flex-start
  +inline-link-button
</style>
