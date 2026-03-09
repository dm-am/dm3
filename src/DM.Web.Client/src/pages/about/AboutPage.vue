<script setup lang="ts">
import { useRoute } from "vue-router";
import { useWebsiteReviewStore } from "@/shared/stores/websiteReviews";
import { useUserStore } from "@/entities/user";
import { extractNumberParam } from "@/app/providers/router";
import { useFetchData } from "@/shared/lib/composables/useFetchData";
import { storeToRefs } from "pinia";
import { userIsSeniorModerator } from "@/entities/user";
import { ref, computed } from "vue";
import ThePaging from "@/shared/ui/Paging/ThePaging.vue";
import WebsiteReviewItem from "./WebsiteReviewItem.vue";
import TextArea from "@/shared/ui/TextArea/TextArea.vue";
import TheButton from "@/shared/ui/Button/TheButton.vue";
import { UserAutocomplete } from "@/shared/ui/UserAutocomplete";

const route = useRoute();
const userStore = useUserStore();
const websiteReviewStore = useWebsiteReviewStore();
const { websiteReviews } = storeToRefs(websiteReviewStore);

useFetchData(
  () => websiteReviewStore.fetchWebsiteReviews(extractNumberParam(route.params.n)),
  [
    {
      param: (p) => p.n,
      callback: (n) => websiteReviewStore.fetchWebsiteReviews(extractNumberParam(n)),
    },
  ],
);

const canAddReview = computed(() => userIsSeniorModerator(userStore.user));

// Expandable form state
const formExpanded = ref(false);
const formHovered = ref(false);
const formContent = ref<HTMLElement | null>(null);

function toggleForm() {
  formExpanded.value = !formExpanded.value;
  if (formContent.value) {
    if (formExpanded.value) {
      formContent.value.style.height = "auto";
      const expectedHeight = formContent.value.clientHeight;
      formContent.value.style.height = "0";
      setTimeout(() => {
        if (formContent.value) formContent.value.style.height = `${expectedHeight}px`;
      }, 0);
      setTimeout(() => {
        if (formContent.value) formContent.value.style.height = "auto";
      }, 200);
    } else {
      formContent.value.style.height = `${formContent.value.clientHeight}px`;
      setTimeout(() => {
        if (formContent.value) formContent.value.style.height = "0";
      }, 0);
    }
  }
}
const authorUsername = ref("");
const reviewText = ref("");
const isSubmitting = ref(false);
const errorMessage = ref("");

async function submitReview() {
  if (!authorUsername.value || !reviewText.value.trim()) {
    errorMessage.value = "Заполните все поля";
    return;
  }

  isSubmitting.value = true;
  errorMessage.value = "";

  const { error } = await websiteReviewStore.createWebsiteReview(
    reviewText.value.trim(),
    authorUsername.value,
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
    authorUsername.value = "";
    reviewText.value = "";
  }
}
</script>

<template>
  <page-title>О проекте</page-title>

  <div class="about-intro">
    <p>
      DM.am — площадка для форумных ролевых игр, где сотни игроков
      создают истории в жанрах фэнтези, sci-fi, horror и не только.
    </p>

    <p class="section-title">Почему форумные игры?</p>
    <p>
      В отличие от настольных сессий, здесь не нужно собираться в одно время
      и в одном месте. Пишите когда удобно — утром за кофе, в обеденный перерыв
      или поздно ночью. Игра идет непрерывно, а продуманные посты делают историю
      глубже и интереснее.
    </p>

    <p class="section-title">Что вас ждет</p>
    <ul class="features-list">
      <li>Игры на любой вкус — от классического D&amp;D до авторских сеттингов</li>
      <li>Гибкий темп — играйте в своем ритме, без привязки к расписанию</li>
      <li>Акцент на отыгрыш — здесь ценят хорошие тексты, а не броски кубиков</li>
      <li>Несколько персонажей — участвуйте в нескольких играх одновременно</li>
      <li>Дружелюбное сообщество — помогаем новичкам освоиться</li>
    </ul>

    <p>Не нужно знать правила наизусть — мастера объяснят механику по ходу игры.</p>

    <p>
      Готовы попробовать?
      Загляните в <router-link to="/games"><strong>список игр</strong></router-link>
      или <router-link to="/games/create"><strong>создайте свою</strong></router-link>.
    </p>
  </div>

  <!-- Reviews Section -->
  <h4 class="reviews-title">
    <span
      v-if="canAddReview"
      class="toggle"
      @click="toggleForm"
      @mouseenter="formHovered = true"
      @mouseleave="formHovered = false"
    >
      Наши пользователи о нас<span
        class="toggle-icon"
        :style="{
          transform: `rotate(${formExpanded ? 45 : 0}deg)`,
          opacity: formHovered ? 1 : 0,
        }"
      ></span>
    </span>
    <span v-else>Наши пользователи о нас</span>
  </h4>

  <!-- Add Review (Senior Moderators and Admins only) -->
  <div
    v-if="canAddReview"
    ref="formContent"
    class="review-form-wrapper"
    :class="{ collapsed: !formExpanded }"
  >
    <div class="review-form">
      <div class="form-field">
        <label class="form-label"><strong>Автор</strong></label>
        <user-autocomplete
          v-model="authorUsername"
          placeholder=""
        />
      </div>
      <div class="form-field">
        <label class="form-label"><strong>Текст отзыва</strong></label>
        <text-area v-model="reviewText" placeholder="Текст отзыва..." />
      </div>
      <div class="form-actions">
        <the-button :disabled="isSubmitting" @click="submitReview">
          {{ isSubmitting ? "Сохранение..." : "Добавить отзыв" }}
        </the-button>
        <span v-if="errorMessage" class="error-message">{{ errorMessage }}</span>
      </div>
    </div>
  </div>

  <the-paging
    v-if="websiteReviews"
    :paging="websiteReviews.paging!"
    :to="{ name: 'about', params: route.params }"
  />

  <div v-if="websiteReviews" class="reviews-list">
    <website-review-item
      v-for="websiteReview in websiteReviews.resources"
      :key="websiteReview.id"
      :controls="true"
      :review="websiteReview"
    />
  </div>

  <the-paging
    v-if="websiteReviews && websiteReviews.paging"
    :paging="websiteReviews.paging"
    :to="{ name: 'about', params: route.params }"
  />
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"
@import "src/assets/styles/Themes"
@import "src/assets/styles/Inputs"

.about-intro
  margin-bottom: $medium
  color: $text
  line-height: 1.6

  p
    margin: 0 0 $small

  a
    color: $link
    &:hover
      color: $link-hover
      text-decoration: underline

.section-title
  font-weight: bold
  margin-top: $medium

.features-list
  margin: 0 0 $small
  padding-left: $big

  li
    margin: $tiny 0
    color: $text

// Reviews title with toggle
.reviews-title
  margin: $medium 0 $small
  font-size: $font-size
  font-weight: bold
  text-transform: uppercase
  letter-spacing: 0.5px
  color: $heading

.toggle
  cursor: pointer

.toggle-icon
  position: relative
  display: inline-block
  width: 10px
  height: 10px
  margin-left: 6px
  opacity: 0
  transition: opacity 0.15s ease, transform 0.3s ease
  vertical-align: middle
  margin-top: -2px

  &::before,
  &::after
    content: ""
    position: absolute
    top: 50%
    left: 50%
    background-color: $heading

  &::before
    width: 10px
    height: 2px
    transform: translate(-50%, -50%)

  &::after
    width: 2px
    height: 10px
    transform: translate(-50%, -50%)

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

.reviews-list
  display: flex
  flex-direction: column
  gap: $small
</style>
