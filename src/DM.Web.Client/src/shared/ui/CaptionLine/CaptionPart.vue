<script setup lang="ts">
/**
 * A part of a CaptionLine that has a say in how it joins the next part. Any
 * element is a part already; this one exists for `:bar="false"`, which runs
 * the part into the next one with a plain space instead of " | ", and for
 * `joint`, which runs it in with a mark and a space ("Автор: X" and the date,
 * joined by ","). The mark ends this part, so it stays at the end of a line
 * the part ends and goes to the next line with it. And it exists for `flow`,
 * a part that is words of the sentence the line runs on from (a title after
 * the dash of a list): it wraps at its own spaces where the line ends, instead
 * of going to the next line whole and leaving the dash alone above it.
 * CaptionLine reads the props off the vnode, so the component itself only
 * renders its slot.
 */
withDefaults(
  defineProps<{
    /** A bar follows this part. False joins it to the next with a space. */
    bar?: boolean;
    /** The mark that joins it to the next part, before a space, in place of the bar. */
    joint?: string;
    /** Words of the sentence, wrapping at their spaces, not one box. */
    flow?: boolean;
  }>(),
  { bar: true, joint: undefined, flow: false },
);
</script>

<template>
  <span class="caption-part"><slot /></span>
</template>
