<script setup lang="ts">
// The choice between an anonymous and a public poll, the same in creation and in editing.
import { useId } from "vue";
import FormField from "@/shared/ui/Form/FormField.vue";

const isAnonymous = defineModel<boolean>({ required: true });

// One name per field: two editors on a page keep their radios apart.
const name = `poll-type-${useId()}`;
</script>

<template>
  <FormField label="Тип опроса">
    <div class="poll-type-selector">
      <label class="radio-option">
        <input v-model="isAnonymous" type="radio" :name="name" :value="true" />
        <span>Анонимный опрос</span>
        <span class="hint">(рекомендуется)</span>
      </label>
      <label class="radio-option">
        <input v-model="isAnonymous" type="radio" :name="name" :value="false" />
        <span>Публичный опрос</span>
        <span class="hint warning">Все увидят кто как голосовал</span>
      </label>
    </div>
    <slot />
  </FormField>
</template>

<style scoped lang="sass">
.poll-type-selector
  display: flex
  flex-direction: column
  gap: $small

.radio-option
  display: flex
  align-items: center
  gap: $small
  cursor: pointer

  input[type="radio"]
    margin: 0

.hint
  margin-left: $small
  font-size: $secondary-font-size
  color: $text-muted

  &.warning
    color: $accent-red
</style>
