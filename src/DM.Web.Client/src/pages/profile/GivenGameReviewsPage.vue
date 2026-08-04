<script setup lang="ts">
/**
 * GivenGameReviewsPage — a user's "Написанные рецензии на игры" page.
 * A thin wrapper over `ProfileGameReviewsList` in `written` mode: reviews this
 * user wrote about games.
 */
import { ErrorPage } from "@/shared/ui/ErrorPage";
import ProfileSubpageHeader from "./ProfileSubpageHeader.vue";
import ProfileGameReviewsList from "./ProfileGameReviewsList.vue";
import { useProfileSubpage } from "./useProfileSubpage";

const { username, canonicalUsername, notFound, profileLink } =
  useProfileSubpage("Написанные рецензии на игры");
</script>

<template>
  <ErrorPage v-if="notFound" :code="404" />
  <div v-else class="given-game-reviews-page">
    <ProfileSubpageHeader
      label="Написанные рецензии на игры"
      :username="canonicalUsername"
    >
      Что игрок
      <router-link :to="profileLink">{{ canonicalUsername }}</router-link>
      пишет об играх сообщества
    </ProfileSubpageHeader>

    <ProfileGameReviewsList
      :username="username"
      mode="written"
      route-name="given-game-reviews"
    />
  </div>
</template>

<style scoped lang="sass">
.given-game-reviews-page
  width: 100%
</style>
