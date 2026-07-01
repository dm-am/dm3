<script setup lang="ts">
/**
 * ReceivedReviewsPage — «Полученные оценки» пользователя.
 * Тонкая обертка над `UserRatedPostsList` в режиме `received`: посты,
 * автором которых является этот пользователь и которые получили хотя
 * бы одну оценку.
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

useDocumentTitle(() => `Полученные оценки — ${username.value}`);
</script>

<template>
  <div class="received-reviews-page">
    <ProfileSubpageHeader label="Полученные оценки" :username="username">
      Посты игрока
      <router-link :to="profileLink">{{ username }}</router-link
      >, оцененные хотя бы раз другими участниками сообщества
    </ProfileSubpageHeader>

    <UserRatedPostsList
      :username="username"
      mode="received"
      route-name="received-reviews"
    />
  </div>
</template>

<style scoped lang="sass">
.received-reviews-page
  width: 100%
</style>
