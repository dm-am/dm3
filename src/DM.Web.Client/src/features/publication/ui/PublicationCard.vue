<script setup lang="ts">
/**
 * PublicationCard — the card for a blog publication.
 *
 * This is the component consumers must render for a publication; the
 * publication is its own entity with its own (future) visual design.
 * TEMPORARY implementation, by product decision (2026-07-11): until that
 * design lands, the publication must look one-to-one like a forum topic
 * card, so internally this delegates to TopicCard. When the publication
 * design arrives, replace the internals HERE — consumers (profile best
 * publication, blog feed) keep rendering PublicationCard and TopicCard
 * stays purely a forum concern.
 *
 * Graceful degradation instead of fake data: the title and the comments
 * count stay plain text, because there is nothing to link them to — the
 * router has no publication page (only blog, blog-feed, publication
 * create/edit and blog-comments), so a link would go nowhere. The
 * moderator warn action is not wired either.
 *
 * Liking is owned HERE rather than by the consumers, which is where it
 * differs from TopicView and CommentItem: those emit up because a store
 * owns the list they sit in. A publication has no such owner — the blog
 * feed reads the blog-details store, the profile spotlight keeps a single
 * publication in a local ref — so emitting up would mean writing the same
 * request twice. The likers list is therefore a local copy of the prop,
 * reseeded whenever the publication itself changes, so the card never
 * writes into its parent's data.
 */
import { computed, ref, watch } from "vue";
import { storeToRefs } from "pinia";
import { blogApi } from "@/entities/blog";
import type { Publication } from "@/entities/blog";
import { useAuthStore } from "@/entities/user";
import type { User } from "@/shared/api/models/common";
import { unwrapResource } from "@/shared/api";
import { notifyFailure } from "@/shared/lib/errors";
import { TopicCard } from "@/features/topic/@x/publication";

const props = withDefaults(
  defineProps<{
    publication: Publication;
    /** Enable content truncation (for embedded/spotlight contexts). */
    truncatable?: boolean;
  }>(),
  {
    truncatable: false,
  },
);

const { user: currentUser } = storeToRefs(useAuthStore());

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

// Mirrors PublicationIntentionResolver on the server (a draft cannot be
// liked) plus the house rule the other two like buttons follow: no heart
// on your own text.
const canLike = computed(
  () => !!currentUser.value && props.publication.isPublished && !isAuthor.value,
);

// One request at a time: the button stays on screen while it is in flight,
// and a second click would race the first into a duplicate like.
const busy = ref(false);

async function toggleLike() {
  if (busy.value || !canLike.value) return;
  busy.value = true;
  if (isLikedByMe.value) {
    const { error } = await blogApi.unlikePublication(props.publication.id);
    if (error) {
      notifyFailure(error, "Не удалось убрать лайк");
    } else {
      const me = currentUser.value?.username;
      likes.value = likes.value.filter((u) => u.username !== me);
    }
  } else {
    const { data, error } = await blogApi.likePublication(props.publication.id);
    const liker = unwrapResource<User>(data);
    if (error) {
      notifyFailure(error, "Не удалось поставить лайк");
    } else if (liker) {
      likes.value = [...likes.value, liker];
    }
  }
  busy.value = false;
}
</script>

<template>
  <!-- publishedUtc is the moment readers care about; createdUtc is the
       draft-creation fallback for data published before the field existed. -->
  <TopicCard
    :title="publication.title"
    :content-html="publication.content"
    :author="publication.author"
    :created-utc="publication.publishedUtc ?? publication.createdUtc"
    :modified-utc="publication.modifiedUtc"
    :comments-count="publication.commentCount"
    :likes="likes"
    :can-like="canLike"
    :is-liked-by-me="isLikedByMe"
    :truncatable="truncatable"
    @toggle-like="toggleLike"
  />
</template>
