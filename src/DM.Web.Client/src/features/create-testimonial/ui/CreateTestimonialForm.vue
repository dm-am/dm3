<script setup lang="ts">
import { computed } from "vue";
import {
  useAuthStore,
  userIsSeniorModerator,
  UserAutocomplete,
} from "@/entities/user";
import { useCreateTestimonial } from "../model";
import TextArea from "@/shared/ui/TextArea/TextArea.vue";
import Button from "@/shared/ui/Button/Button.vue";
import { TESTIMONIAL_TEXT_MAX_LENGTH } from "@/shared/lib/constants/community";

const userStore = useAuthStore();

// The rank is the server's: WebsiteTestimonialIntentionResolver admits Create for SeniorModerator up.
const canAddTestimonial = computed(() => userIsSeniorModerator(userStore.user));

const {
  formExpanded,
  authorUsername,
  testimonialText,
  isSubmitting,
  authorError,
  errorMessage,
  toggleForm,
  submitTestimonial,
} = useCreateTestimonial();
</script>

<template>
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
            label="Автор отзыва"
            name="testimonialAuthor"
            :errors="authorError ? [authorError] : []"
          >
            <template #hint
              >Отзыв появится от имени этого пользователя</template
            >
            <UserAutocomplete
              id="testimonialAuthor"
              v-model="authorUsername"
              placeholder="Имя пользователя"
            />
          </form-field>
          <form-field
            label="Текст отзыва (обычный текст, без форматирования)"
            name="testimonialText"
            :errors="errorMessage ? [errorMessage] : []"
          >
            <template #hint
              >Опишите впечатления от игры: что понравилось, что можно
              улучшить</template
            >
            <text-area
              v-model="testimonialText"
              placeholder="Расскажите, что вам нравится в Dungeon Master..."
              :max-length="TESTIMONIAL_TEXT_MAX_LENGTH"
            />
          </form-field>
          <div class="form-actions">
            <Button
              variant="primary"
              :loading="isSubmitting"
              @click="submitTestimonial"
            >
              Добавить отзыв
            </Button>
          </div>
        </div>
      </div>
    </div>
  </template>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Links" as *

.toggle-link
  display: inline-block
  margin-bottom: $medium
  +inline-link-button


.review-form
  margin-bottom: $medium
  padding: $medium
  background-color: $bg-element-overlay
  border: 1px solid $border

.form-actions
  display: flex
  align-items: center
  gap: $medium
</style>
