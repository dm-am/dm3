<script setup lang="ts">
// /forum-topic/{id}, the link of a notification (NotificationDestination.cs):
// the payload carries no board alias or number, so the topic is read first.
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
