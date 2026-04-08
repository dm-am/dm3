<script setup lang="ts">
import { ref, onMounted } from "vue";
import type { AttributeSchema } from "@/entities/game";
import { gameApi } from "@/entities/game";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";

const model = defineModel<string | null>();

const schemas = ref<AttributeSchema[]>([]);
const loading = ref(false);
const error = ref<string | null>(null);

async function loadSchemas() {
  loading.value = true;
  error.value = null;

  const { data, error: apiError } = await gameApi.getSchemas();

  if (apiError) {
    error.value = apiError.title || "Failed to load schemas";
  } else if (data) {
    schemas.value = data.resources;
  }

  loading.value = false;
}

onMounted(loadSchemas);
</script>

<template>
  <div class="schema-selector">
    <div v-if="error" class="selector-error">
      {{ error }}
    </div>

    <div v-else-if="schemas.length === 0" class="selector-empty">
      <secondary-text>Нет доступных схем атрибутов</secondary-text>
    </div>

    <div v-else class="schema-options">
      <label class="schema-option">
        <input type="radio" :value="null" v-model="model" />
        <span class="option-label">Без системы атрибутов</span>
      </label>
      <label
        v-for="(schema, index) in schemas"
        :key="schema.id ?? index"
        class="schema-option"
      >
        <input type="radio" :value="schema.id" v-model="model" />
        <span class="option-label">{{ schema.title }}</span>
      </label>
    </div>
  </div>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"

.schema-selector
  min-height: $grid-step * 10

.selector-error
  color: $accent-red

.schema-options
  display: flex
  flex-direction: column
  gap: $small

.schema-option
  display: flex
  align-items: center
  gap: $small
  cursor: pointer

  input[type="radio"]
    width: auto

.option-label
  flex: 1
</style>
