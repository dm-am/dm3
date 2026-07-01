<script setup lang="ts">
/**
 * GivenEndorsementsPage — «Написанные рекомендации» пользователя.
 * Тонкая обертка над `UserEndorsementsList` в режиме `written`:
 * рекомендации, которые этот пользователь написал о других.
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

useDocumentTitle(() => `Написанные рекомендации — ${username.value}`);
</script>

<template>
  <div class="given-endorsements-page">
    <ProfileSubpageHeader label="Написанные рекомендации" :username="username">
      Что игрок
      <router-link :to="profileLink">{{ username }}</router-link>
      пишет о других участниках сообщества
    </ProfileSubpageHeader>

    <UserEndorsementsList
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
