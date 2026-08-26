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

const userStore = useAuthStore();

// Single policy owner: testimonials are a curated review barrier — regular
// users post feedback in the forum topic instead. The rank is the server's,
// not a looser one: WebsiteTestimonialIntentionResolver admits Create for
// SeniorModerator and above (an admin posts on a user's behalf), so an
// ordinary moderator opening this form would have been answered 403.
// See pages/about/TestimonialsPage.vue, which renders this form unconditionally.
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
          <!-- The entry is signed by the participant named here, not by the
               moderator filling the form in. The same picker the other
               moderation forms use (award grant, invitations, roles). -->
          <form-field
            label="Автор отзыва"
            name="testimonialAuthor"
            :errors="authorError ? [authorError] : []"
          >
            <template #hint
              >Отзыв будет опубликован от имени этого участника</template
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
              :max-length="1000"
            />
          </form-field>
          <div class="form-actions">
            <Button :loading="isSubmitting" @click="submitTestimonial">
              Добавить отзыв
            </Button>
          </div>
        </div>
      </div>
    </div>
  </template>
</template>

<style scoped lang="sass">
@use "@/assets/styles/Inputs" as *

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
