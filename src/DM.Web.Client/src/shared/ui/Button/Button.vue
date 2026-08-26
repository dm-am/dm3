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
@use "@/assets/styles/Inputs" as *

button
  +button

  // Busy — the bar along the bottom edge that says the request is in flight.
  // Drawn by the shared mixin (Inputs.sass) off the aria-busy the template
  // above sets, so the caption stays whatever the caller passed.
  +button-busy

  // Primary action — the site's existing "this one is picked" idiom, the one
  // the active segment of a SegmentedControl wears: a step of fill above the
  // surface plus weight, and no second language of emphasis.
  //
  // The fill is the RELATIVE step and not the solid $bg-element-accent the
  // segment uses, because a form footer IS $bg-element-accent (Form.vue
  // .controls) and that is where most of the site's primary buttons stand: a
  // solid fill there would paint the main action in the exact colour of the
  // strip under it, flatter than the "Отмена" beside it, which takes its own
  // relative step. Composed over $bg-page this overlay is $bg-element-accent
  // itself in both themes — ThemeVariables.css does that arithmetic where the
  // token is declared.
  //
  // The ink is written out although +button already sets it: a fill with no
  // ink of its own is a pair nobody measured, and fillContrast.spec.ts asks
  // every fill on the site for both halves.
  //
  // Hover needs nothing here. +button stacks $hover-overlay over whatever
  // fill the control carries, so the primary steps up by exactly what every
  // neighbouring control steps up by, and the border stays the shared $border.
  &.primary
    background-color: $control-bg-hover-overlay
    color: $text
    font-weight: 700
</style>
