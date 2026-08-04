<script setup lang="ts">
/**
 * ReceivedGameReviewsPage — a user's "Полученные рецензии на игры" page.
 * A thin wrapper over `ProfileGameReviewsList` in `received` mode: reviews
 * other members wrote about the games this user masters.
 */
import { ErrorPage } from "@/shared/ui/ErrorPage";
import ProfileSubpageHeader from "./ProfileSubpageHeader.vue";
import ProfileGameReviewsList from "./ProfileGameReviewsList.vue";
import { useProfileSubpage } from "./useProfileSubpage";

const { username, canonicalUsername, notFound, profileLink } =
  useProfileSubpage("Полученные рецензии на игры");
</script>

<template>
  <ErrorPage v-if="notFound" :code="404" />
  <div v-else class="received-game-reviews-page">
    <ProfileSubpageHeader
      label="Полученные рецензии на игры"
      :username="canonicalUsername"
    >
      Что участники сообщества пишут об играх, которые ведет
      <router-link :to="profileLink">{{ canonicalUsername }}</router-link>
    </ProfileSubpageHeader>

    <ProfileGameReviewsList
      :username="username"
      mode="received"
      route-name="received-game-reviews"
    />
  </div>
</template>

<style scoped lang="sass">
.received-game-reviews-page
  width: 100%
</style>
