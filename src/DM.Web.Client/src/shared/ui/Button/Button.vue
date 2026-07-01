<script setup lang="ts">
withDefaults(
  defineProps<{
    type?: "submit" | "button";
    loading?: boolean;
    disabled?: boolean;
  }>(),
  {
    type: "submit",
  },
);
</script>

<template>
  <button
    :type="type"
    :disabled="loading || disabled"
    :aria-busy="loading || undefined"
  >
    <slot />
  </button>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Inputs"

button
  +button
</style>
