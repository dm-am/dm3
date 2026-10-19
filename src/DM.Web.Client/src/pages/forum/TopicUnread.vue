<script setup lang="ts">
/**
 * Resolves the comments counter: the server names the comment this reader
 * stopped at, and the page replaces itself with the topic route at its page and
 * "#comment-{id}". A route of its own, since the topic page marks the topic
 * read as soon as it loads.
 */
import { onMounted } from "vue";
import { useRoute, useRouter } from "vue-router";
import { forumApi } from "@/entities/forum";
import { usePaging } from "@/shared/lib/composables/usePaging";

const route = useRoute();
const router = useRouter();
// The list asks for the reader's page size, so the page is computed with it.
const { pageSize } = usePaging();

onMounted(async () => {
  const alias = String(route.params.alias);
  const num = Number(route.params.num);
  const topic = { name: "topic", params: { alias, num } };

  const { data } = await forumApi.getFirstUnreadComment(alias, num);
  const target = data?.resource;

  // Nothing to land on: the topic itself.
  if (!target?.commentId) {
    router.replace(topic);
    return;
  }

  const page = Math.ceil(target.commentNumber / pageSize.value);
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
