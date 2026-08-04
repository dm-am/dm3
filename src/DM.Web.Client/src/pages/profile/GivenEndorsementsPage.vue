<script setup lang="ts">
/**
 * GivenEndorsementsPage — a user's "Написанные рекомендации" page.
 * A thin wrapper over `ProfileEndorsementsList` in `written` mode:
 * endorsements this user wrote about others.
 */
import { ErrorPage } from "@/shared/ui/ErrorPage";
import ProfileSubpageHeader from "./ProfileSubpageHeader.vue";
import ProfileEndorsementsList from "./ProfileEndorsementsList.vue";
import { useProfileSubpage } from "./useProfileSubpage";

const { username, canonicalUsername, notFound, profileLink } =
  useProfileSubpage("Написанные рекомендации");
</script>

<template>
  <ErrorPage v-if="notFound" :code="404" />
  <div v-else class="given-endorsements-page">
    <ProfileSubpageHeader
      label="Написанные рекомендации"
      :username="canonicalUsername"
    >
      Что игрок
      <router-link :to="profileLink">{{ canonicalUsername }}</router-link>
      пишет о других участниках сообщества
    </ProfileSubpageHeader>

    <ProfileEndorsementsList
      :username="username"
      mode="written"
      route-name="given-endorsements"
    />
  </div>
</template>

<style scoped lang="sass">
.given-endorsements-page
  width: 100%
</style>
