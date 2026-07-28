<script setup lang="ts">
import { computed } from "vue";
import { useUserStore, userIsModerator } from "@/entities/user";
import { useCreateTestimonial } from "../model";
import TextArea from "@/shared/ui/TextArea/TextArea.vue";
import Button from "@/shared/ui/Button/Button.vue";

const userStore = useUserStore();

// Single policy owner: testimonials are a moderator-curated review barrier —
// regular users post feedback in the forum topic instead. See
// pages/about/TestimonialsPage.vue, which renders this form unconditionally.
const canAddTestimonial = computed(() => userIsModerator(userStore.user));

const {
  formExpanded,
  testimonialText,
  isSubmitting,
  errorMessage,
  toggleForm,
  submitTestimonial,
} = useCreateTestimonial();
</script>

<template>
  <!-- Add Testimonial (authenticated users only) -->
  <template v-if="canAddTestimonial">
    <button
      v-if="!formExpanded"
      type="button"
      class="toggle-link"
      @click="toggleForm"
    >
      + Добавить отзыв
    </button>
    <div class="expand-fold" :class="{ open: formExpanded }">
      <div class="expand-fold-clip" :inert="!formExpanded">
        <div class="review-form">
          <form-field
            label="Текст отзыва (обычный текст, без форматирования)"
            name="testimonialText"
            :errors="errorMessage ? [errorMessage] : []"
          >
            <template #hint
              >Опишите впечатления от игры — что понравилось, что можно
              улучшить</template
            >
            <text-area
              v-model="testimonialText"
              placeholder="Расскажите, что вам нравится в Dungeon Master…"
              :max-length="1000"
            />
          </form-field>
          <div class="form-actions">
            <Button :disabled="isSubmitting" @click="submitTestimonial">
              {{ isSubmitting ? "Сохранение…" : "Добавить отзыв" }}
            </Button>
          </div>
        </div>
      </div>
    </div>
  </template>
</template>

<style scoped lang="sass">
@import "@/assets/styles/Inputs"

.toggle-link
  display: inline-block
  margin-bottom: $medium
  +inline-link-button

// The collapse itself is the global CSS-only .expand-fold (Reset.sass).

// Review form styling
.review-form
  margin-bottom: $medium
  padding: $medium
  background-color: $bg-element-overlay
  border: 1px solid $border
  border-radius: $border-radius

.form-actions
  display: flex
  align-items: center
  gap: $medium
</style>
