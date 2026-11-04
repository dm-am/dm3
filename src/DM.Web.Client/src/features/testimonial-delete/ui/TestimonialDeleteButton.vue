<script setup lang="ts">
// Admin-only, stricter than the create barrier: testimonials are a curated public signal.
import { ref, computed } from "vue";
import type { WebsiteTestimonial } from "@/shared/api/models/community";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import { BubbleAction } from "@/shared/ui/SpeechBubble";
import { useTestimonialStore } from "@/entities/testimonial";
import { useAuthStore, userIsAdmin } from "@/entities/user";
import { OWNS_REFUSAL } from "@/shared/api/interceptor";

const props = defineProps<{
  testimonial: WebsiteTestimonial;
}>();

const userStore = useAuthStore();
const testimonialStore = useTestimonialStore();

const canAdministrate = computed(() => userIsAdmin(userStore.user));
const showConfirm = ref(false);

const removeTestimonial = () =>
  testimonialStore.removeTestimonial(props.testimonial.id, OWNS_REFUSAL);
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
