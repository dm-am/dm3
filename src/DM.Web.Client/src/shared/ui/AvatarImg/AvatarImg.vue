<script setup lang="ts">
import { computed } from "vue";
import { defaultAvatarBody, icons } from "@/shared/lib/utils/icons";
import type { UserPicture } from "@/shared/api/models/community";

/**
 * Universal <img> for avatars. SSOT for:
 *   - picking the right URL out of the 3 variants (small 100 / medium 400 / original ≤1024),
 *   - srcset/sizes for DPR-aware resolution selection by the browser,
 *   - the theme-aware default SVG fallback,
 *   - a11y attrs (loading, decoding, width/height, alt).
 *
 * Usage:
 *   <AvatarImg :picture="user.picture" :alt="user.username" :size="48" />
 *   <AvatarImg :picture="user.picture" :alt="user.username" :size="64" eager />
 *
 * The browser picks the right resolution based on `size` (CSS px)
 * and DPR (1x / 2x / 3x). On retina @2x a 64px DOM slot takes medium (400px)
 * for sharpness; on 1x — small (100px) to save bandwidth.
 */
const props = withDefaults(
  defineProps<{
    /** Picture object (nested DTO with smallUrl/mediumUrl/originalUrl). */
    picture?: UserPicture | null;
    /** Alt text for screen readers. Empty string = decorative. */
    alt: string;
    /**
     * Size in CSS px (square avatar). Used for:
     *   - the width/height attributes (anti-CLS),
     *   - sizes='{N}px' for srcset selection.
     */
    size: number;
    /**
     * eager=false (default) → loading="lazy". For LCP candidates (the large
     * avatar on ProfilePage) set eager=true → fetchpriority="high".
     */
    eager?: boolean;
    /** Extra CSS class on the img. */
    imgClass?: string;
    /**
     * If true, returns the original instead of a thumbnail variant.
     * Needed for large aspect-preserving avatars (ProfilePage 220px).
     * srcset is not used in this mode (the original is the only variant
     * with the aspect preserved).
     */
    preferOriginal?: boolean;
    /**
     * If true and there is no picture, the component renders an empty <img>
     * without the default avatar. Used for Character: "no avatar = no image"
     * (unlike User, where a generic silhouette is shown).
     */
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

// `src` (a fallback for browsers without srcset support — all modern
// browsers support it, but src is still needed as a baseline).
// Empty when the user has no picture: the default silhouette is not a URL any
// more, it is the <svg> below, so "is there a picture" is exactly "is src set".
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

// srcset: candidates with width descriptors. The browser picks by `sizes` * DPR.
// Not used in preferOriginal mode (single source, the browser scales).
const srcset = computed(() => {
  if (props.preferOriginal) return undefined;
  const candidates: string[] = [];
  if (small.value) candidates.push(`${small.value} 100w`);
  if (medium.value) candidates.push(`${medium.value} 400w`);
  return candidates.length > 1 ? candidates.join(", ") : undefined;
});

const sizes = computed(() => `${props.size}px`);
</script>

<template>
  <img
    v-if="src"
    :src="src"
    :srcset="srcset"
    :sizes="srcset ? sizes : undefined"
    :alt="alt"
    :width="size"
    :height="size"
    :class="imgClass"
    :loading="eager ? 'eager' : 'lazy'"
    :fetchpriority="eager ? 'high' : 'auto'"
    decoding="async"
  />
  <!-- No picture: the silhouette, inline, so the cascade paints it. It carries
       the caller's class and the same width/height the <img> would, which is
       what keeps the slot the same size in both branches. `noDefault` callers
       (Character: no avatar means no image, unlike User) get neither. -->
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
// The two tones of the default silhouette, from the theme instead of from four
// hex literals inside a data URI. $text-muted on $bg-element is the pair the
// palette already calibrates to AA (4.50 in the light theme), so the silhouette
// reads on its square in both themes without a number chosen here.
// :deep, because the body arrives through v-html and scoped attributes are
// stamped on compiled markup only.
.default-avatar :deep(.default-avatar-bg)
  fill: $bg-element

.default-avatar :deep(.default-avatar-fg)
  fill: $text-muted
</style>
