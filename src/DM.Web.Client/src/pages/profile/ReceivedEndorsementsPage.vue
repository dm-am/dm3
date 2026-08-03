<script setup lang="ts">
/**
 * ReceivedEndorsementsPage — a user's "Полученные рекомендации" page.
 * A thin wrapper over `ProfileEndorsementsList` in `received` mode:
 * endorsements other members wrote about this user.
 */
import { ErrorPage } from "@/shared/ui/ErrorPage";
import ProfileSubpageHeader from "./ProfileSubpageHeader.vue";
import ProfileEndorsementsList from "./ProfileEndorsementsList.vue";
import { useProfileSubpage } from "./useProfileSubpage";

const { username, canonicalUsername, notFound, profileLink } =
  useProfileSubpage("Полученные рекомендации");
</script>

<template>
  <ErrorPage v-if="notFound" :code="404" />
  <div v-else class="received-endorsements-page">
    <ProfileSubpageHeader
      label="Полученные рекомендации"
      :username="canonicalUsername"
    >
      Что участники сообщества пишут об игроке
      <router-link :to="profileLink">{{ canonicalUsername }}</router-link>
    </ProfileSubpageHeader>

    <ProfileEndorsementsList
      :username="username"
      mode="received"
      route-name="received-endorsements"
    />
  </div>
</template>

<style scoped lang="sass">
.received-endorsements-page
  width: 100%
</style>
