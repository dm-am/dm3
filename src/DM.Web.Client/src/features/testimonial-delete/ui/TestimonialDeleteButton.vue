<script setup lang="ts">
/**
 * TestimonialDeleteButton — admin-only delete control for a website
 * testimonial: the trash in the top right corner of the bubble, rendered into
 * the card's #actions slot. Self-gates on admin (renders nothing otherwise)
 * and confirms through ConfirmDialog before removing.
 *
 * Deletion is intentionally admin-only (stricter than the moderator-level
 * create barrier) — testimonials are a curated, public-facing trust signal,
 * so only admins may remove entries.
 */
import { ref, computed } from "vue";
import type { WebsiteTestimonial } from "@/shared/api/models/community";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import { BubbleAction } from "@/shared/ui/SpeechBubble";
import { useTestimonialStore } from "@/entities/testimonial";
import { useAuthStore, userIsAdmin } from "@/entities/user";

const props = defineProps<{
  testimonial: WebsiteTestimonial;
}>();

const userStore = useAuthStore();
const testimonialStore = useTestimonialStore();

const canAdministrate = computed(() => userIsAdmin(userStore.user));
const showConfirm = ref(false);

// The confirmation's action. The store drops the entry on success alone, and a
// refusal stays in the window with the delete ready for a retry.
const removeTestimonial = () =>
  testimonialStore.removeTestimonial(props.testimonial.id, {
    ownsRefusal: true,
    ownsRateLimit: true,
  });
</script>

<template>
  <template v-if="canAdministrate">
    <BubbleAction icon="trash" label="Удалить" @click="showConfirm = true" />
    <ConfirmDialog
      v-model:show="showConfirm"
      title="Удалить отзыв?"
      message="Отзыв будет удален без возможности восстановления"
      confirm-label="Удалить"
      failure="Не удалось удалить отзыв"
      :action="removeTestimonial"
    />
  </template>
</template>
