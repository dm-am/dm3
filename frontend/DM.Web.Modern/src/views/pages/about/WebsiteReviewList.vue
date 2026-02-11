<script setup lang="ts">
import ThePaging from "@/components/ThePaging.vue";
import WebsiteReviewItem from "@/views/pages/about/WebsiteReviewItem.vue";
import TextArea from "@/components/inputs/TextArea.vue";
import TheButton from "@/components/inputs/TheButton.vue";
import UserAutocomplete from "@/components/inputs/UserAutocomplete.vue";
import { useRoute } from "vue-router";
import { useWebsiteReviewStore, useUserStore } from "@/stores";
import { storeToRefs } from "pinia";
import { userIsAdmin } from "@/api/models/community/helpers";
import { ref, computed } from "vue";

const route = useRoute();
const userStore = useUserStore();
const websiteReviewStore = useWebsiteReviewStore();
const { websiteReviews } = storeToRefs(websiteReviewStore);

const isAdmin = computed(() => userIsAdmin(userStore.user));
const authorLogin = ref("");
const reviewText = ref("");
const isSubmitting = ref(false);
const errorMessage = ref("");

async function submitReview() {
  if (!authorLogin.value || !reviewText.value.trim()) {
    errorMessage.value = "Заполните все поля";
    return;
  }

  isSubmitting.value = true;
  errorMessage.value = "";

  const { error } = await websiteReviewStore.createWebsiteReview(
    reviewText.value.trim(),
    authorLogin.value,
  );

  isSubmitting.value = false;

  if (error) {
    if (error.status === 409) {
      errorMessage.value = "У этого пользователя уже есть отзыв";
    } else if (error.status === 400) {
      errorMessage.value = "Некорректные данные";
    } else if (error.status === 404) {
      errorMessage.value = "Пользователь не найден";
    } else if (error.status === 403) {
      errorMessage.value = "Недостаточно прав";
    } else if (error.status === 500) {
      errorMessage.value = "Внутренняя ошибка сервера. Попробуйте позже";
    } else {
      errorMessage.value = "Не удалось создать отзыв. Попробуйте позже";
    }
  } else {
    authorLogin.value = "";
    reviewText.value = "";
  }
}
</script>

<template>
  <template v-if="isAdmin">
    <block-title>Добавить отзыв</block-title>
    <div class="admin-form">
      <div class="form-row">
        <label>Автор:</label>
        <user-autocomplete
          v-model="authorLogin"
          placeholder=""
        />
      </div>
      <div class="form-row">
        <label>Текст отзыва:</label>
        <text-area v-model="reviewText" />
      </div>
      <div class="form-row">
        <the-button :disabled="isSubmitting" @click="submitReview">
          {{ isSubmitting ? "Сохранение..." : "Добавить" }}
        </the-button>
        <span v-if="errorMessage" class="error">{{ errorMessage }}</span>
      </div>
    </div>
  </template>

  <the-paging
    v-if="websiteReviews"
    :paging="websiteReviews.paging!"
    :to="{ name: 'about', params: route.params }"
  />

  <secondary-text v-if="websiteReviews && !websiteReviews.resources.length">Нет отзывов о проекте</secondary-text>
  <template v-else-if="websiteReviews">
    <website-review-item
      v-for="websiteReview in websiteReviews.resources"
      :key="websiteReview.id"
      :controls="true"
      :review="websiteReview"
    />
  </template>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Themes"

.admin-form
  margin-bottom: $large
  padding: $medium
  border-radius: $border-radius
  background: $bg-highlight-blue

.form-row
  margin-bottom: $small

  label
    display: block
    margin-bottom: $minor

.error
  margin-left: $small
  color: $heading
</style>
