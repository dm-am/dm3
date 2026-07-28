<script setup lang="ts">
/**
 * TestimonialDeleteButton — admin-only delete control for a website
 * testimonial, rendered into the testimonial card footer via its #controls
 * slot. Self-gates on admin (renders nothing otherwise) and confirms through
 * ConfirmDialog before removing.
 *
 * Deletion is intentionally admin-only (stricter than the moderator-level
 * create barrier) — testimonials are a curated, public-facing trust signal,
 * so only admins may remove entries.
 */
import { ref, computed } from "vue";
import type { WebsiteTestimonial } from "@/shared/api/models/community";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import { symbols } from "@/shared/lib/utils/icons";
import { useTestimonialStore } from "@/shared/stores/testimonials";
import { useUserStore, userIsAdmin } from "@/entities/user";
import { useToast } from "@/shared/lib/composables/useToast";

const props = defineProps<{
  testimonial: WebsiteTestimonial;
}>();

const userStore = useUserStore();
const testimonialStore = useTestimonialStore();
const toast = useToast();

const canAdministrate = computed(() => userIsAdmin(userStore.user));
const loading = ref(false);
const showConfirm = ref(false);

async function confirmRemove() {
  loading.value = true;
  const { error } = await testimonialStore.removeTestimonial(
    props.testimonial.id,
  );
  loading.value = false;
  if (error) {
    // Keep the entry in place (the store only drops it on success) and
    // surface the failure so the delete stays available for a retry.
    toast.error("Не удалось удалить отзыв");
  } else {
    showConfirm.value = false;
  }
}
</script>

<template>
  <template v-if="canAdministrate"
    ><span class="copy-space">{{ " " }}</span
    ><secondary-text class="testimonial-controls"
      ><button
        type="button"
        class="testimonial-remove"
        @click="showConfirm = true"
      >
        {{ symbols.close }} Удалить
      </button></secondary-text
    >
    <ConfirmDialog
      :show="showConfirm"
      title="Удалить отзыв"
      message="Отзыв будет удален без возможности восстановления."
      confirm-label="Удалить"
      danger
      :loading="loading"
      @confirm="confirmRemove"
      @cancel="showConfirm = false"
      @update:show="showConfirm = $event"
    />
  </template>
</template>

<style scoped lang="sass">
@import "@/assets/styles/Inputs"

// Zero-width preserved space: invisible in layout (font-size: 0) but
// Selection.toString() still emits a real " " before the delete control.
.copy-space
  white-space: pre
  font-size: 0

.testimonial-controls
  display: inline
  margin-left: $small

// Admin "× Удалить" — a real <button> reset to the inline link appearance
// (global $link blue, underline on hover). font-size inherited from the
// surrounding secondary-text.
.testimonial-remove
  +inline-link-button
</style>
