<script setup lang="ts">
import SecondaryText from "@/components/layout/SecondaryText.vue";

defineProps<{
  title: string;
  empty?: string;
  editable?: boolean;
}>();
const value = defineModel<string>();
</script>

<template>
  <dl>
    <secondary-text tag="dt">{{ title }}</secondary-text>
    <dd v-if="editable">
      <input
        type="text"
        :value="value"
        @input="value = ($event.target as HTMLInputElement).value"
        class="stat-input"
      />
    </dd>
    <dd v-else>{{ value || empty }}</dd>
  </dl>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"

dl
  margin: $small 0

dd
  padding: 0

.stat-input
  width: 100%
  padding: $tiny $small
  font-size: inherit
  border-radius: $border-radius
  box-sizing: border-box
</style>
