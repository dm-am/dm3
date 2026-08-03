<script setup lang="ts">
/**
 * GivenEndorsementsPage — a user's "Написанные рекомендации" page.
 * A thin wrapper over `ProfileEndorsementsList` in `written` mode:
 * endorsements this user wrote about others.
 */
import { computed } from "vue";
import { useRoute } from "vue-router";
import {
  joinTitleSegments,
  useDocumentTitle,
} from "@/shared/lib/composables/useDocumentTitle";
import { ErrorPage } from "@/shared/ui/ErrorPage";
import ProfileSubpageHeader from "./ProfileSubpageHeader.vue";
import ProfileEndorsementsList from "./ProfileEndorsementsList.vue";
import { useProfileSubpageUser } from "./useProfileSubpageUser";

const route = useRoute();
const username = computed(() => route.params.username as string);

const { notFound, canonicalUsername } = useProfileSubpageUser(username);

const profileLink = computed(() => ({
  name: "profile" as const,
  params: { username: username.value },
}));

useDocumentTitle(() =>
  joinTitleSegments(canonicalUsername.value, "Написанные рекомендации"),
);
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
