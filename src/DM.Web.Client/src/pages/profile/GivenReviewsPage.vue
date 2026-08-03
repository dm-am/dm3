<script setup lang="ts">
/**
 * GivenReviewsPage — a user's "Поставленные оценки" page.
 * A thin wrapper over `ProfileRatedPostsList` in `given` mode: other users' posts
 * where this user left at least one review
 * (see the `reviewerUsername` backend filter in PostsQuery).
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
  joinTitleSegments(canonicalUsername.value, "Поставленные оценки постов"),
);
</script>

<template>
  <ErrorPage v-if="notFound" :code="404" />
  <div v-else class="given-reviews-page">
    <ProfileSubpageHeader
      label="Поставленные оценки постов"
      :username="canonicalUsername"
    >
      Чужие посты, которые оценил игрок
      <router-link :to="profileLink">{{ canonicalUsername }}</router-link>
    </ProfileSubpageHeader>

    <ProfileRatedPostsList
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
