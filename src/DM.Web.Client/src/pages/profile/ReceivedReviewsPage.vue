<script setup lang="ts">
/**
 * ReceivedReviewsPage — a user's "Полученные оценки" page.
 * A thin wrapper over `ProfileRatedPostsList` in `received` mode: posts
 * authored by this user that received at
 * least one review.
 */
import { computed } from "vue";
import { useRoute } from "vue-router";
import {
  joinTitleSegments,
  useDocumentTitle,
} from "@/shared/lib/composables/useDocumentTitle";
import { ErrorPage } from "@/shared/ui/ErrorPage";
import ProfileSubpageHeader from "./ProfileSubpageHeader.vue";
import ProfileRatedPostsList from "./ProfileRatedPostsList.vue";
import { useProfileSubpageUser } from "./useProfileSubpageUser";

const route = useRoute();
const username = computed(() => route.params.username as string);

const { notFound, canonicalUsername } = useProfileSubpageUser(username);

const profileLink = computed(() => ({
  name: "profile" as const,
  params: { username: username.value },
}));

useDocumentTitle(() =>
  joinTitleSegments(canonicalUsername.value, "Полученные оценки постов"),
);
</script>

<template>
  <ErrorPage v-if="notFound" :code="404" />
  <div v-else class="received-reviews-page">
    <ProfileSubpageHeader
      label="Полученные оценки постов"
      :username="canonicalUsername"
    >
      Посты игрока
      <router-link :to="profileLink">{{ canonicalUsername }}</router-link
      >, оцененные хотя бы раз другими участниками сообщества
    </ProfileSubpageHeader>

    <ProfileRatedPostsList
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
