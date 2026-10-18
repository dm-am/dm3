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
 * The title and the comments count lead to the publication's own page
 * (`blog-publication`), which is also where the counter's discussion lives.
 * They used to be plain text because the router had no such route and a link
 * would have gone nowhere; the card is rendered on that page too, and there
 * it says `standalone` — the shell heading already names the publication, so
 * the card draws no title of its own and no link back to the page it is on.
 * The blog is addressed by whatever the owner of the card knows: the feed
 * passes the id from its own URL so the readable public id survives, and
 * everyone else falls back to the guid the publication carries.
 *
 * The moderator warn action is not wired. "Редактировать" is a link to the
 * edit page, handed in by the page that renders the card: the card does not
 * know who may edit, the feed does (the blog it manages).
 *
 * Liking is owned HERE rather than by the consumers, which is where it
 * differs from TopicView and CommentItem: TopicView emits up and CommentItem
 * calls a function its list hands in, because a store owns the list they sit
 * in. A publication has no such owner — the blog
 * feed reads the blog-details store, the profile spotlight keeps a single
 * publication in a local ref — so emitting up would mean writing the same
 * request twice. The likers list is therefore a local copy of the prop,
 * reseeded whenever the publication itself changes, so the card never
 * writes into its parent's data.
 */
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
    /** Enable content truncation (for embedded/spotlight contexts). */
    truncatable?: boolean;
    /**
     * The card IS the publication page. The page's own heading names the
     * publication, so the card renders no title and links nowhere: a title
     * repeated under the heading is a second name for one text, and a
     * self-link is a link to the page the reader is already on.
     */
    standalone?: boolean;
    /**
     * How to address the blog in the link. The feed hands over the id standing
     * in its own URL, so the readable public id survives the hop and the zone
     * shell sees no change of blog to reload. A card with no better answer
     * falls back to the guid the publication carries: both open the blog.
     */
    blogId?: string;
    /** The edit page of the publication, for a viewer who may edit it. */
    editTo?: RouteLocationRaw | null;
  }>(),
  {
    truncatable: false,
    standalone: false,
    blogId: undefined,
    editTo: null,
  },
);

const { user: currentUser } = storeToRefs(useAuthStore());

// Where the title and the comments counter lead. Null on the page of the
// publication itself, which is the same rule the topic card follows there.
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

// Mirrors PublicationIntentionResolver on the server (a draft cannot be
// liked) plus the house rule the other two like buttons follow: no heart
// on your own text.
const canLike = computed(
  () => !!currentUser.value && props.publication.isPublished && !isAuthor.value,
);

// One request at a time: the button stays on screen while it is in flight,
// and a second click would race the first into a duplicate like.
const busy = ref(false);

// A refused like changes nothing on the heart, so a line under the card says
// it, where the press was (the topic of the forum says it the same way).
const likeResult = useActionResult();

async function toggleLike() {
  if (busy.value || !canLike.value) return;
  busy.value = true;
  likeResult.clear();
  if (isLikedByMe.value) {
    const { error } = await blogApi.unlikePublication(props.publication.id);
    if (error) {
      likeResult.refuse(error, "Не удалось убрать лайк");
    } else {
      const me = currentUser.value?.username;
      likes.value = likes.value.filter((u) => u.username !== me);
    }
  } else {
    const { data, error } = await blogApi.likePublication(props.publication.id);
    const liker = unwrapResource<User>(data);
    if (error) {
      likeResult.refuse(error, "Не удалось поставить лайк");
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
    :title="standalone ? undefined : publication.title"
    :title-to="publicationRoute"
    :comments-to="publicationRoute"
    :content-html="publication.content"
    :author="publication.author"
    :created-utc="publication.publishedUtc ?? publication.createdUtc"
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
