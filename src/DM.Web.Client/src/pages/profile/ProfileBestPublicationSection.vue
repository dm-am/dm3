<script setup lang="ts">
/**
 * The user's most-liked publication across his blogs. The endpoint answers no
 * publication with a null resource, not a 404.
 */
import { onMounted, ref, watch } from "vue";
import { blogApi } from "@/entities/blog";
import type { Publication } from "@/entities/blog";
import { PublicationCard } from "@/features/publication";
import { GamePostSkeleton } from "@/shared/ui/Skeleton";
import { SecondaryText } from "@/shared/ui/Layout";
import { ErrorState } from "@/shared/ui/ErrorState";
import { useGuardedRequest } from "@/shared/lib/composables/useGuardedRequest";

const props = defineProps<{
  username: string;
}>();

const publication = ref<Publication | null>(null);
const loaded = ref(false);

// The profile can change under the section: the guard drops the older answer.
const { loading, error, run } = useGuardedRequest({
  message: "Не удалось загрузить лучшую публикацию",
  clearErrorOnStart: true,
});

function fetchBest(username: string) {
  loaded.value = false;
  return run(
    () => blogApi.getUserBestPublication(username),
    (data) => {
      publication.value = data?.resource ?? null;
    },
  ).finally(() => {
    loaded.value = true;
  });
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
      :message="error"
      :retry="() => fetchBest(username)"
    />

    <SecondaryText v-else-if="!publication"> Нет публикаций </SecondaryText>

    <PublicationCard v-else :publication="publication" truncatable />
  </section>
</template>
