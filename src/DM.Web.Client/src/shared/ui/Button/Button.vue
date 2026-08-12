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

  // Busy — the bar along the bottom edge that says the request is in flight.
  // Drawn by the shared mixin (Inputs.sass) off the aria-busy the template
  // above sets, so the caption stays whatever the caller passed.
  +button-busy

  // Primary action — filled with the site's link/action colour so it clearly
  // outranks the secondary (e.g. "Отмена") control. The ink is $text-on-fill and
  // not white: $link is navy in the light theme only, in the dark one it is a
  // light blue on which white measures 2.30.
  // Hover is the +button overlay over the same fill. $link-hover cannot serve
  // as the hover fill: at its luminance neither white (3.45) nor $text (3.63)
  // clears AA in the light theme.
  &.primary
    background-color: $link
    border-color: $link
    color: $text-on-fill
</style>
