<script setup lang="ts">
/**
 * Resolver for the topic's comments counter. Asks the server which comment
 * this reader stopped at — the first one he has not read, or the topic's last
 * comment when everything is read — and replaces itself with the canonical
 * topic route pointing at it: the page holding the comment goes into
 * "?number=", the comment itself into the "#comment-{id}" permalink the
 * comments list already scrolls to and highlights.
 *
 * A route of its own rather than a query param on the topic page: the topic
 * page flushes the read marker as soon as it loads, so anything computed after
 * it has mounted is computed for a reader who has just read everything.
 */
import { onMounted } from "vue";
import { useRoute, useRouter } from "vue-router";
import { forumApi } from "@/entities/forum";
import { usePaging } from "@/shared/lib/composables/usePaging";

const route = useRoute();
const router = useRouter();
// The list asks the API for this reader's own page size, so the page holding a
// given comment has to be computed with that same size.
const { commentsPerPage } = usePaging();

onMounted(async () => {
  const alias = String(route.params.alias);
  const num = Number(route.params.num);
  const topic = { name: "topic", params: { alias, num } };

  const { data } = await forumApi.getFirstUnreadComment(alias, num);
  const target = data?.resource;

  // Nothing to land on (a topic with no comments, or a failed request): the
  // topic itself is all there is to show.
  if (!target?.commentId) {
    router.replace(topic);
    return;
  }

  const page = Math.ceil(target.commentNumber / commentsPerPage.value);
  router.replace({
    ...topic,
    query: page > 1 ? { number: String(page) } : {},
    hash: `#comment-${target.commentId}`,
  });
});
</script>

<template>
  <secondary-text>Ищем, где вы остановились...</secondary-text>
</template>
