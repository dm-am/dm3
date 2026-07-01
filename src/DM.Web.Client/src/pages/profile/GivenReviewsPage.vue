<script setup lang="ts">
/**
 * GivenReviewsPage — «Поставленные оценки» пользователя.
 * Тонкая обертка над `UserRatedPostsList` в режиме `given`: чужие посты,
 * в которых этот пользователь оставил хотя бы один отзыв
 * (см. бэкенд-фильтр `reviewerUsername` в PostsQuery).
 */
import { computed } from "vue";
import { useRoute } from "vue-router";
import { useDocumentTitle } from "@/shared/lib/composables/useDocumentTitle";
import ProfileSubpageHeader from "./ProfileSubpageHeader.vue";
import UserRatedPostsList from "./UserRatedPostsList.vue";

const route = useRoute();
const username = computed(() => route.params.username as string);

const profileLink = computed(() => ({
  name: "profile" as const,
  params: { username: username.value },
}));

useDocumentTitle(() => `Поставленные оценки — ${username.value}`);
</script>

<template>
  <div class="given-reviews-page">
    <ProfileSubpageHeader label="Поставленные оценки" :username="username">
      Чужие посты, оцененные хотя бы раз игроком
      <router-link :to="profileLink">{{ username }}</router-link>
    </ProfileSubpageHeader>

    <UserRatedPostsList
      :username="username"
      mode="given"
      route-name="given-reviews"
    />
  </div>
</template>

<style scoped lang="sass">
.given-reviews-page
  width: 100%
</style>
