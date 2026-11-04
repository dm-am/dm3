<script setup lang="ts">
import { computed } from "vue";
import { defaultAvatarBody, icons } from "@/shared/lib/utils/icons";
import type { UserPicture } from "@/shared/api/models/community";

const props = withDefaults(
  defineProps<{
    picture?: UserPicture | null;
    /** Empty string = decorative. */
    alt: string;
    /** CSS px: the declared box (anti-CLS) and `sizes` for srcset. */
    size: number;
    /** For an LCP candidate: loads eagerly with fetchpriority high. */
    eager?: boolean;
    imgClass?: string;
    /** The original instead of a thumbnail: only it keeps the uploaded aspect ratio. */
    preferOriginal?: boolean;
    /** Without a picture draws nothing instead of the silhouette (a character, unlike a user). */
    noDefault?: boolean;
  }>(),
  {
    picture: null,
    eager: false,
    imgClass: undefined,
    preferOriginal: false,
    noDefault: false,
  },
);

const small = computed(() => props.picture?.smallUrl ?? null);
const medium = computed(() => props.picture?.mediumUrl ?? null);
const original = computed(() => props.picture?.originalUrl ?? null);

// Empty without a picture: the silhouette is the <svg> below, not a URL.
const src = computed(() => {
  if (props.preferOriginal) {
    return original.value || medium.value || small.value || "";
  }
  // Thumbnail-mode pick: small for small displays, medium for large ones.
  if (props.size <= 100) {
    return small.value || medium.value || original.value || "";
  }
  return medium.value || small.value || original.value || "";
});

const defaultAvatarViewBox = icons.defaultAvatar.viewBox;

const srcset = computed(() => {
  if (props.preferOriginal) return undefined;
  const candidates: string[] = [];
  if (small.value) candidates.push(`${small.value} 100w`);
  if (medium.value) candidates.push(`${medium.value} 400w`);
  return candidates.length > 1 ? candidates.join(", ") : undefined;
});

const sizes = computed(() => `${props.size}px`);

// Thumbnails are square crops at the asked size, so only the original needs its own ratio.
const intrinsic = computed(() => {
  if (!props.preferOriginal || !original.value) return null;
  const width = props.picture?.originalWidth;
  const height = props.picture?.originalHeight;
  if (!width || !height || width <= 0 || height <= 0) return null;
  return { width, height };
});

// The declared pair lets the browser reserve the box before the picture arrives (anti-CLS).
const boxHeight = computed(() => {
  const size = intrinsic.value;
  if (!size) return props.size;
  return Math.max(1, Math.round((props.size * size.height) / size.width));
});
</script>

<template>
  <img
    v-if="src"
    :src="src"
    :srcset="srcset"
    :sizes="srcset ? sizes : undefined"
    :alt="alt"
    :width="size"
    :height="boxHeight"
    :class="imgClass"
    :loading="eager ? 'eager' : 'lazy'"
    :fetchpriority="eager ? 'high' : 'auto'"
    decoding="async"
  />
  <!-- Inline, so the theme cascade paints it; the same width/height keeps the slot. -->
  <svg
    v-else-if="!noDefault"
    class="default-avatar"
    :class="imgClass"
    :viewBox="defaultAvatarViewBox"
    :width="size"
    :height="size"
    :role="alt ? 'img' : undefined"
    :aria-label="alt || undefined"
    :aria-hidden="alt ? undefined : 'true'"
    v-html="defaultAvatarBody"
  />
</template>

<style scoped lang="sass">
// :deep: the body arrives through v-html, which scoped attributes never reach.
.default-avatar :deep(.default-avatar-bg)
  fill: $bg-element

.default-avatar :deep(.default-avatar-fg)
  fill: $text-muted
</style>
