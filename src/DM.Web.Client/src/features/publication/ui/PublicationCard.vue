<script setup lang="ts">
// Drawn by TopicCard for now; the like is owned here, since a publication has no store to own it.
import { computed, ref, watch } from "vue";
import type { RouteLocationRaw } from "vue-router";
import { storeToRefs } from "pinia";
import { blogApi } from "@/entities/blog";
import type { Publication } from "@/entities/blog";
import { useAuthStore } from "@/entities/user";
import type { User } from "@/shared/api/models/common";
import { unwrapResource } from "@/shared/api";
import { useActionResult } from "@/shared/lib/composables/useActionResult";
import { ActionResult } from "@/shared/ui/ActionResult";
import { TopicCard } from "@/features/topic/@x/publication";

const props = withDefaults(
  defineProps<{
    publication: Publication;
    truncatable?: boolean;
    /** The card is the publication page: no title of its own and no self-link. */
    standalone?: boolean;
    /** The feed hands its URL id, so the readable public id survives the hop. */
    blogId?: string;
    editTo?: RouteLocationRaw | null;
  }>(),
  {
    truncatable: false,
    standalone: false,
    editTo: null,
  },
);

const { user: currentUser } = storeToRefs(useAuthStore());

const publicationRoute = computed(() =>
  props.standalone
    ? null
    : {
        name: "blog-publication",
        params: {
          id: props.blogId ?? props.publication.blogId,
          pubId: props.publication.id,
        },
      },
);

const likes = ref<Publication["likes"]>([...props.publication.likes]);
watch(
  () => props.publication,
  (next) => {
    likes.value = [...(next.likes ?? [])];
  },
);

const isAuthor = computed(
  () => currentUser.value?.username === props.publication.author?.username,
);

const isLikedByMe = computed(
  () =>
    !!currentUser.value &&
    likes.value.some((u) => u.username === currentUser.value?.username),
);

// Nobody likes his own text.
const canLike = computed(() => !!currentUser.value && !isAuthor.value);

// One request at a time: a second click would race the first into a duplicate like.
const busy = ref(false);

const likeResult = useActionResult();

async function toggleLike() {
  if (busy.value || !canLike.value) return;
  busy.value = true;
  likeResult.clear();
  if (isLikedByMe.value) {
    const { error } = await blogApi.unlikePublication(props.publication.id);
    if (error) {
      likeResult.refuse(error, "Не удалось убрать отметку");
    } else {
      const me = currentUser.value?.username;
      likes.value = likes.value.filter((u) => u.username !== me);
    }
  } else {
    const { data, error } = await blogApi.likePublication(props.publication.id);
    const liker = unwrapResource<User>(data);
    if (error) {
      likeResult.refuse(error, "Не удалось отметить");
    } else if (liker) {
      likes.value = [...likes.value, liker];
    }
  }
  busy.value = false;
}
</script>

<template>
  <TopicCard
    :title="standalone ? undefined : publication.title"
    :title-to="publicationRoute"
    :comments-to="publicationRoute"
    :content-html="publication.content"
    :author="publication.author"
    :created-utc="publication.createdUtc"
    :modified-utc="publication.modifiedUtc"
    :comments-count="publication.commentCount"
    :likes="likes"
    :can-like="canLike"
    :is-liked-by-me="isLikedByMe"
    :edit-to="editTo"
    :truncatable="truncatable"
    @toggle-like="toggleLike"
  />
  <ActionResult :result="likeResult" />
</template>
