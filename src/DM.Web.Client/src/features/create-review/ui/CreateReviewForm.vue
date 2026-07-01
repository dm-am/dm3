<script setup lang="ts">
import { computed } from "vue";
import { useUserStore } from "@/entities/user";
import { useCreateReview } from "../model";
import TextArea from "@/shared/ui/TextArea/TextArea.vue";
import Button from "@/shared/ui/Button/Button.vue";

const userStore = useUserStore();
const canAddReview = computed(() => userStore.isAuthenticated);

const {
  formExpanded,
  formContent,
  reviewText,
  isSubmitting,
  errorMessage,
  toggleForm,
  submitReview,
} = useCreateReview();
</script>

<template>
  <!-- Add Review (authenticated users only) -->
  <template v-if="canAddReview">
    <a v-if="!formExpanded" class="toggle-link" @click="toggleForm"
      >+ Добавить отзыв</a
    >
    <div
      ref="formContent"
      class="review-form-wrapper"
      :class="{ collapsed: !formExpanded }"
    >
      <div class="review-form">
        <div class="form-field">
          <label class="form-label"
            ><strong
              >Текст отзыва (обычный текст, без форматирования)</strong
            ></label
          >
          <text-area
            v-model="reviewText"
            placeholder="Расскажите, что вам нравится в DM.AM..."
            :max-length="1000"
          />
        </div>
        <div class="form-actions">
          <Button :disabled="isSubmitting" @click="submitReview">
            {{ isSubmitting ? "Сохранение..." : "Добавить отзыв" }}
          </Button>
          <span v-if="errorMessage" class="error-message">{{
            errorMessage
          }}</span>
        </div>
      </div>
    </div>
  </template>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"

.toggle-link
  display: inline-block
  margin-bottom: $medium
  color: $link
  cursor: pointer
  &:hover
    color: $link-hover

// Collapsible form wrapper
.review-form-wrapper
  overflow: hidden
  transition: height 0.2s ease

  &.collapsed
    height: 0

// Review form styling
.review-form
  margin-bottom: $medium
  padding: $medium
  background-color: $bg-element-overlay
  border: 1px dashed $border

.form-field
  margin-bottom: $medium

.form-label
  display: block
  margin-bottom: $tiny
  color: $text
  font-size: $font-size

.form-actions
  display: flex
  align-items: center
  gap: $medium

.error-message
  color: $accent-red
  font-size: $secondary-font-size
</style>
