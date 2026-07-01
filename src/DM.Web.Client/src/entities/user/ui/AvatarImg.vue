<script setup lang="ts">
import { computed } from "vue";
import { defaultAvatarUrl } from "@/shared/lib/utils/icons";
import type { UserPicture } from "@/shared/api/models/community";

/**
 * Универсальный <img> для аватаров. SSOT для:
 *   - выбора правильного URL из 3 вариантов (small 100 / medium 400 / original ≤1024),
 *   - srcset/sizes для DPR-aware resolution selection браузером,
 *   - fallback на theme-aware default SVG,
 *   - a11y attrs (loading, decoding, width/height, alt).
 *
 * Использование:
 *   <AvatarImg :picture="user.picture" :alt="user.username" :size="48" />
 *   <AvatarImg :picture="user.picture" :alt="user.username" :size="64" eager />
 *
 * Браузер сам подберет подходящее разрешение исходя из `size` (CSS px)
 * и DPR (1x / 2x / 3x). На retina @2x для 64px DOM возьмет medium (400px)
 * чтобы было четко; на 1x — small (100px) для экономии трафика.
 */
const props = withDefaults(
  defineProps<{
    /** Picture object (nested DTO с smallUrl/mediumUrl/originalUrl). */
    picture?: UserPicture | null;
    /** Alt text для screen-reader'ов. Пустая строка = decorative. */
    alt: string;
    /**
     * Размер в CSS px (квадратный аватар). Используется для:
     *   - width/height атрибутов (anti-CLS),
     *   - sizes='{N}px' для srcset selection.
     */
    size: number;
    /**
     * eager=false (default) → loading="lazy". Для LCP-кандидатов (большой
     * аватар на ProfilePage) ставим eager=true → fetchpriority="high".
     */
    eager?: boolean;
    /** Дополнительный CSS-class на img. */
    imgClass?: string;
    /**
     * Если true, возвращает original вместо thumbnail-варианта.
     * Нужен для больших aspect-preserving аватаров (ProfilePage 220px).
     * При этом srcset не используется (original — единственный вариант
     * с сохраненной аспектом).
     */
    preferOriginal?: boolean;
    /**
     * Если true, при отсутствии picture компонент рендерит пустой <img>
     * без default-аватара. Используется для Character: «no avatar = no image»
     * (в отличие от User где показываем generic силуэт).
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

const hasAnyUrl = computed(
  () => !!(small.value || medium.value || original.value),
);

const shouldRender = computed(() => (props.noDefault ? hasAnyUrl.value : true));

// `src` (fallback для браузеров без srcset support — все современные
// браузеры поддерживают, но src все равно нужен как baseline).
const src = computed(() => {
  const fallback = props.noDefault ? "" : defaultAvatarUrl;
  if (props.preferOriginal) {
    return original.value || medium.value || small.value || fallback;
  }
  // Для thumbnail-режима пик: small для маленьких display, medium для крупных.
  if (props.size <= 100) {
    return small.value || medium.value || original.value || fallback;
  }
  return medium.value || small.value || original.value || fallback;
});

// srcset: candidates с width descriptors. Браузер выбирает по `sizes` * DPR.
// Не используется в preferOriginal-режиме (single source, browser scales).
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
    v-if="shouldRender"
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
</template>
