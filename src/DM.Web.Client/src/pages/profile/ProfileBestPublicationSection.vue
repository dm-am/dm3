<script setup lang="ts">
/**
 * ProfileBestPublicationSection — the user's most-liked published publication
 * across every blog they author. Sibling to ProfileBestPostSection but for
 * blogs: same role (spotlight inside the relevant profile tab), same
 * empty / loading affordances (a GamePostSkeleton while loading, matching
 * ProfileBestPostSection).
 *
 * Backend contract: `GET /v1/users/{username}/best-publication` returns
 * an envelope with `resource: Publication | null`. `null` is the success
 * case for "no publications yet" — the API doesn't 404 in that case, so
 * we branch on the resource being null instead of catching errors.
 *
 * Rendering: the publication goes through PublicationCard — the
 * publication's own presentational card. (Its current internals mirror the
 * topic card by product decision; that is PublicationCard's concern, not
 * this page's.)
 */
import { onMounted, ref, watch } from "vue";
import { blogApi } from "@/entities/blog";
import type { Publication } from "@/entities/blog";
import { PublicationCard } from "@/features/publication";
import { GamePostSkeleton } from "@/shared/ui/Skeleton";
import { SecondaryText } from "@/shared/ui/Layout";
import { ErrorState } from "@/shared/ui/ErrorState";

const props = defineProps<{
  username: string;
}>();

const publication = ref<Publication | null>(null);
const loaded = ref(false);
const loading = ref(false);
const error = ref(false);

async function fetchBest(username: string) {
  loading.value = true;
  loaded.value = false;
  error.value = false;
  const { data, error: fetchError } =
    await blogApi.getUserBestPublication(username);
  if (fetchError) {
    error.value = true;
  } else {
    publication.value = data?.resource ?? null;
  }
  loading.value = false;
  loaded.value = true;
}

onMounted(() => fetchBest(props.username));
watch(
  () => props.username,
  (next) => fetchBest(next),
);
</script>

<template>
  <section class="profile-best-publication">
    <GamePostSkeleton v-if="loading && !loaded" />

    <ErrorState
      v-else-if="error"
      message="Не удалось загрузить лучшую публикацию"
      :retry="() => fetchBest(username)"
    />

    <SecondaryText v-else-if="!publication"> Нет публикаций </SecondaryText>

    <PublicationCard v-else :publication="publication" truncatable />
  </section>
</template>
