<script setup lang="ts">
// Drawn here, never by an image service: the value is a second factor's secret. uqr is a lazy chunk.
import { computed, ref, watch } from "vue";

const props = withDefaults(
  defineProps<{
    /** What the square encodes. */
    value: string;
    /** Name of the picture for a screen reader. */
    label: string;
    /** Side of the drawn square, in pixels. */
    size?: number;
  }>(),
  { size: 220 },
);

/** Level M rather than the default L: the margin buys back a bad camera and a glare. */
const ERROR_CORRECTION = "M" as const;

/** Wider than the default of one: cameras stumble on a square that runs into the page. */
const QUIET_ZONE = 3;

/** Modules per side, quiet zone included; 0 until the matrix is drawn. */
const modules = ref(0);
/** The dark modules as one SVG path, in module units. */
const path = ref("");
const failed = ref(false);

const boxStyle = computed(() => ({
  width: `${props.size}px`,
  height: `${props.size}px`,
}));

async function draw(value: string) {
  path.value = "";
  modules.value = 0;
  failed.value = false;

  if (!value) return;

  try {
    const { encode } = await import("uqr");
    const result = encode(value, {
      ecc: ERROR_CORRECTION,
      border: QUIET_ZONE,
    });

    // Raced by a later value: writing this one over it would show the old secret.
    if (value !== props.value) return;

    const segments: string[] = [];
    for (let row = 0; row < result.size; row++) {
      for (let column = 0; column < result.size; column++) {
        if (result.data[row][column])
          segments.push(`M${column},${row}h1v1h-1z`);
      }
    }

    modules.value = result.size;
    path.value = segments.join("");
  } catch {
    // No chunk, or a value too long for any version: the caller keeps the secret in text beside it.
    if (value === props.value) failed.value = true;
  }
}

watch(() => props.value, draw, { immediate: true });
</script>

<template>
  <div class="qr-code" :style="boxStyle">
    <!-- Dark on white in both themes, the owner's named exception: cameras read a dark drawing on light. -->
    <svg
      v-if="path"
      class="qr-code-image"
      xmlns="http://www.w3.org/2000/svg"
      :viewBox="`0 0 ${modules} ${modules}`"
      role="img"
      :aria-label="label"
      shape-rendering="crispEdges"
    >
      <rect width="100%" height="100%" fill="white" />
      <path :d="path" fill="black" />
    </svg>

    <p v-else-if="failed" class="qr-code-note">
      Не удалось нарисовать код. Введите секрет вручную.
    </p>

    <!-- Same square, so nothing moves when the drawing replaces it. -->
    <div v-else class="qr-code-pending" aria-hidden="true" />
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Skeleton" as *

.qr-code
  display: flex
  align-items: center
  justify-content: center
  box-sizing: border-box
  padding: $small
  border: 1px solid $border
  // See the note in the template.
  background-color: white

.qr-code-image
  width: 100%
  height: 100%

.qr-code-note
  margin: 0
  padding: $small
  text-align: center
  font-size: $secondary-font-size
  // The box is white in both themes.
  color: black

.qr-code-pending
  width: 100%
  height: 100%
  +skeleton-shimmer($square: true)
</style>
