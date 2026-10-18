<script setup lang="ts">
// A request in flight looks like the disabled state, because it is one: the
// control has stopped answering, and the site draws that one way — the hatch
// of +disabled-hatch, over the fill the button already had. aria-busy sits on
// top of that and is read by nobody's stylesheet: it is what tells a screen
// reader the control is coming back rather than switched off.
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

  // The main action of a form: the site's one filled control, whose colours,
  // states and ring live in +button-primary (Inputs.sass) for this button and
  // for the guest card's alike. One per form, on a page and in a window.
  &.primary
    +button-primary
</style>
