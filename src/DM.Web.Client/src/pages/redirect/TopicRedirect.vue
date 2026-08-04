<script setup lang="ts">
// Resolver route for forum-topic deep links that only carry a topic id
// (notification "Перейти"). The canonical topic route is /forum/:alias/:num,
// but a notification payload does not carry the board alias or per-board
// number — so we fetch the topic by id (the backend Guid binder also accepts
// the readable-guid form used in payloads) and replace the URL with the
// canonical route. On failure we hand off to the error page.
import { onMounted } from "vue";
import { useRoute, useRouter } from "vue-router";
import { forumApi, type TopicId } from "@/entities/forum";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";

const route = useRoute();
const router = useRouter();

onMounted(async () => {
  const topicId = String(route.params.topicId);
  const { data, error } = await forumApi.getTopic(topicId as TopicId);
  const topic = data?.resource;

  if (topic && topic.board?.alias && topic.topicNumber != null) {
    router.replace({
      name: "topic",
      params: { alias: topic.board.alias, num: topic.topicNumber },
    });
  } else {
    router.replace({
      name: "error",
      params: { code: error?.status ?? 404 },
    });
  }
});
</script>

<template>
  <SecondaryText>Открываем топик...</SecondaryText>
</template>
