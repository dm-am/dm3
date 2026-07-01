<script setup lang="ts">
/**
 * ReceivedEndorsementsPage — «Полученные рекомендации» пользователя.
 * Тонкая обертка над `UserEndorsementsList` в режиме `received`:
 * рекомендации, которые другие участники написали об этом пользователе.
 */
import { computed } from "vue";
import { useRoute } from "vue-router";
import { useDocumentTitle } from "@/shared/lib/composables/useDocumentTitle";
import ProfileSubpageHeader from "./ProfileSubpageHeader.vue";
import UserEndorsementsList from "./UserEndorsementsList.vue";

const route = useRoute();
const username = computed(() => route.params.username as string);

const profileLink = computed(() => ({
  name: "profile" as const,
  params: { username: username.value },
}));

useDocumentTitle(() => `Полученные рекомендации — ${username.value}`);
</script>

<template>
  <div class="received-endorsements-page">
    <ProfileSubpageHeader label="Полученные рекомендации" :username="username">
      Что участники сообщества пишут об игроке
      <router-link :to="profileLink">{{ username }}</router-link>
    </ProfileSubpageHeader>

    <UserEndorsementsList
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
