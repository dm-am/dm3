<script setup lang="ts">
withDefaults(
  defineProps<{
    type?: "submit" | "button";
    variant?: "primary" | "default";
    loading?: boolean;
    disabled?: boolean;
  }>(),
  {
    type: "submit",
    variant: "default",
  },
);
</script>

<template>
  <button
    :type="type"
    :class="{ primary: variant === 'primary' }"
    :disabled="loading || disabled"
    :aria-busy="loading || undefined"
  >
    <slot />
  </button>
</template>

<style scoped lang="sass">
@import "@/assets/styles/Inputs"

button
  +button

  // Primary action — filled with the site's link/action navy so it clearly
  // outranks the secondary (e.g. Отмена) control. Navy is dark, so white text
  // keeps a high, theme-stable contrast.
  &.primary
    background-color: $link
    border-color: $link
    color: #fff

    &:hover:not(:disabled)
      background-color: $link-hover
      border-color: $link-hover
</style>
