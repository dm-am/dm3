<script setup lang="ts">
/**
 * A string drawn as a QR square.
 *
 * The drawing is done here and not asked of anybody: the one value this
 * component is used for is the otpauth URI of a second factor, and handing that
 * to an outside image service would hand a stranger the secret (INV-15 of the
 * second-factor decision).
 *
 * `uqr` arrives through a dynamic import, so the package is a chunk of its own
 * and downloads for the reader who opens the second-factor setup and for nobody
 * else. `encode` alone is imported: it returns the module matrix, out of which
 * the markup below is one <path>, and that markup can carry a name for a screen
 * reader - which the package's own renderSVG has nowhere to put.
 */
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

/**
 * Error correction level M rather than the package default L: the otpauth URI
 * is short, so the square barely grows, and the margin buys back a bad camera
 * and a glare.
 */
const ERROR_CORRECTION = "M" as const;

/**
 * Quiet zone around the square, in modules. Wider than the package default of
 * one: cameras read a square with a white margin around it and stumble on one
 * that runs into the page.
 */
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

    // Raced by a later value while the package was loading: that draw owns the
    // element now, and writing this one over it would show the old secret.
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
    // The chunk did not arrive, or the value is longer than any version can
    // hold. Either way there is no square, and the caller keeps the secret in
    // text next to it for exactly this case.
    if (value === props.value) failed.value = true;
  }
}

watch(() => props.value, draw, { immediate: true });
</script>

<template>
  <div class="qr-code" :style="boxStyle">
    <!-- Dark on white in both themes, and this is the one place in the project
         where a colour is not taken from the theme. A camera reads a dark
         drawing on a light field; the dark theme would give it a light drawing
         on a dark one, which part of the cameras will not take. Decided by the
         owner as a named exception to the palette rule. -->
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
  border-radius: $border-radius
  // The white field the camera needs, and the only surface on the site that
  // does not follow the theme. See the note in the template.
  background-color: white

.qr-code-image
  width: 100%
  height: 100%

.qr-code-note
  margin: 0
  padding: $small
  text-align: center
  font-size: $secondary-font-size
  // The box is white in both themes, so the sentence on it cannot take the
  // theme's text colour either.
  color: black

.qr-code-pending
  width: 100%
  height: 100%
  +skeleton-shimmer
</style>
