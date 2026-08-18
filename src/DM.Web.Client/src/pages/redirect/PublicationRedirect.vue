<script setup lang="ts">
// Resolver route for publication deep links that carry only a publication id
// (notification "Перейти"). The canonical route is /blogs/:id/feed/:pubId, and
// a notification payload names the blog in the readable-guid form the blog
// endpoint refuses — while the publication endpoint takes it, because the
// server binds a Guid parameter from that form too. So the publication is
// fetched by the id in the URL and its own blogId is what the canonical route
// is built from. Twin of TopicRedirect; on failure we hand off to the error
// page, which is what tells a draft (403) from a deleted publication (404).
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
