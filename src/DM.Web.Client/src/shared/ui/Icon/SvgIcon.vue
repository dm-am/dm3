<template>
  <svg
    :viewBox="icon.viewBox"
    :fill="icon.fill || 'none'"
    :stroke="icon.stroke"
    :stroke-width="icon.strokeWidth"
    :stroke-linecap="icon.strokeLinecap"
    :stroke-linejoin="icon.strokeLinejoin"
    class="svg-icon"
    aria-hidden="true"
    focusable="false"
    v-html="icon.path"
  />
</template>

<script setup lang="ts">
import { computed } from "vue";
import {
  icons,
  type IconDefinition,
  type IconName,
} from "@/shared/lib/utils/icons";

const props = defineProps<{
  /** Icon name from icons.ts */
  name: IconName;
}>();

// The fallback is the second lock, behind the type. A name the registry does
// not have used to leave `icon` undefined, and `icon.viewBox` in the template
// threw during render — which in Vue takes down the whole subtree, so a
// mistyped icon showed the reader a white page instead of a wrong glyph. A
// question mark is a visible defect; a blank page is an invisible one.
//
// Annotated with the interface rather than left to inference: the registry is
// declared with `satisfies`, which keeps the literal keys that make IconName a
// union of real names but also keeps each entry's literal shape, and an entry
// that draws no outline has no `stroke` member for the template to bind.
const icon = computed<IconDefinition>(
  () => icons[props.name] ?? icons.question,
);
</script>

<style scoped lang="sass">
.svg-icon
  width: 1em
  height: 1em
  flex-shrink: 0
</style>
