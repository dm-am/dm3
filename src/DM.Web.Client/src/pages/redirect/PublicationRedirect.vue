<script setup lang="ts">
// /publication/{id}, the link of a notification (NotificationDestination.cs):
// the payload names the blog in the readable-guid form the blog endpoint
// refuses, so the publication is read first and its blogId builds the route.
// The error page tells a draft (403) from a deleted publication (404).
import { onMounted } from "vue";
import { useRoute, useRouter } from "vue-router";
import { blogApi } from "@/entities/blog";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";

const route = useRoute();
const router = useRouter();

onMounted(async () => {
  const pubId = String(route.params.pubId);
  const { data, error } = await blogApi.getPublication(pubId);
  const publication = data?.resource;

  if (publication?.blogId) {
    router.replace({
      name: "blog-publication",
      params: { id: publication.blogId, pubId: publication.id },
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
  <SecondaryText>Открываем публикацию...</SecondaryText>
</template>
