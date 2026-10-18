<script setup lang="ts">
/**
 * BlogSettings — the blog management page (dev doc 4.2.3.6.6 "Настройки
 * блога"). Mirrors GameSettings and composes the sections (listed by their
 * on-page headings):
 *  - "Общие настройки" (title / who sees the blog while it is being formed)
 *  - "Управление рубриками" (rubric create / delete)
 *  - "Управление ролями" (assistant invite / remove)
 *  - "Черный список" (blog blacklist)
 *  - "Приглашения" (reader invites)
 *  - "Опасная зона" (owner-only blog delete)
 *
 * As on the game side, the sections do not share one audience: each is drawn
 * only for the viewers whose saves the server would accept. The widths are
 * spelled out at the computeds below, each named after its intention.
 *
 * As there too, a viewer the page admits to nothing, a guest included, gets
 * the site's own "not found" at its address instead of a page with a refusal
 * on it; the links to it are drawn only for those it admits.
 */
import { computed, ref, watch } from "vue";
import { useRoute, useRouter } from "vue-router";
import { storeToRefs } from "pinia";
import { useBlogDetailsStore } from "@/entities/blog";
import {
  restoreSession,
  useAuthStore,
  userIsAdmin,
  userIsSeniorModerator,
} from "@/entities/user";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import Button from "@/shared/ui/Button/Button.vue";
import BlockTitle from "@/shared/ui/Layout/BlockTitle.vue";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import BlogInfoSection from "./settings/BlogInfoSection.vue";
import RubricsSection from "./settings/RubricsSection.vue";
import RolesSection from "./settings/RolesSection.vue";
import BlacklistSection from "./settings/BlacklistSection.vue";
import InvitationsSection from "./settings/InvitationsSection.vue";

const route = useRoute();
const router = useRouter();
const blogStore = useBlogDetailsStore();
const { isOwner, canManage } = storeToRefs(blogStore);
const { user } = storeToRefs(useAuthStore());

// BlogIntention.EditSettings — the information form (BlogService.Update) and
// the invitation list (BlogInvitationService.GetPendingInvitations). The blog
// leads (owner and assistant) and senior moderation; this is also what admits
// a viewer to the page.
const canEditSettings = computed(
  () => canManage.value || userIsSeniorModerator(user.value),
);

// BlogIntention.Edit — the whole blacklist, its READ included, and assistant
// removal (BlogService.RemoveAssistant). The owner and administration: unlike
// the game side, this arm carries no senior-moderator clause, so a senior
// moderator on this page gets no blacklist at all.
const canEditBlog = computed(() => isOwner.value || userIsAdmin(user.value));

// BlogIntention.CreateRubric, InviteAssistant and CancelInvitation are the
// owner's alone, so `isOwner` gates those directly in the template.
//
// BlogIntention.InviteReader — the owner and the assistants.
const canInviteReader = computed(() => canManage.value);

// Who the viewer is, settled: the start of the page asks the server once
// (restoreSession), and a session the browser kept no copy of is named by that
// answer alone. Deciding before it would send an author away from his blog.
// The roles are read off the blog against the viewer, so they follow him
// without another read of the blog.
const sessionSettled = ref(false);
const settle = () => (sessionSettled.value = true);
restoreSession().then(settle, settle);

const refused = computed(() => sessionSettled.value && !canEditSettings.value);

watch(
  refused,
  (value) => {
    if (!value) return;
    void router.replace({
      name: "not-found",
      params: { pathMatch: route.path.slice(1).split("/") },
      query: route.query,
      hash: route.hash,
    });
  },
  { immediate: true, flush: "post" },
);

// --- Danger zone (owner-only blog delete) ---
const confirmDelete = ref(false);

// The request of the confirmation. The window waits for it, prints a refusal in
// itself and closes on success alone, so the interceptor keeps quiet about it.
async function deleteBlog() {
  // Read before the request: the blog belongs to the store, and leaving the
  // blog zone wipes it. Empty only when there is no blog to delete, and then
  // the request is refused before the navigation below is reached. A blog
  // whose owner was removed has no owner to be on this page.
  const author = blogStore.blog?.author?.username ?? "";
  const error = await blogStore.deleteBlog({
    ownsRefusal: true,
    ownsRateLimit: true,
  });
  if (error) return { error };
  // The author's own blog list on their profile, not the site-wide catalogue
  // of blogs: the deleted blog is missing from this list and nowhere else.
  // The list is a tab, and the tab is asked for by name - /users/X/blogs is an
  // old URL the router redirects to the plain profile, which would land the
  // author on "О себе" and show them nothing.
  //
  // No success toast: that missing row is the confirmation. Put the toast back
  // only if this page ever stops leaving for the author's list.
  router.push({
    name: "profile",
    params: { username: author },
    query: { tab: "blogs" },
  });
  return { error: null };
}
</script>

<template>
  <div class="blog-settings">
    <template v-if="canEditSettings">
      <BlogInfoSection />
      <RubricsSection v-if="isOwner" />
      <RolesSection :can-invite="isOwner" :can-remove="canEditBlog" />
      <BlacklistSection v-if="canEditBlog" />
      <InvitationsSection :can-invite="canInviteReader" :can-cancel="isOwner" />

      <section v-if="isOwner" class="settings-section danger-zone">
        <block-title>Опасная зона</block-title>
        <secondary-text class="danger-hint">
          Удаление блога необратимо
        </secondary-text>
        <Button type="button" class="delete-blog" @click="confirmDelete = true">
          Удалить блог
        </Button>
      </section>
    </template>

    <ConfirmDialog
      v-model:show="confirmDelete"
      title="Удалить блог?"
      message="Блог будет удален без возможности восстановления"
      confirm-label="Удалить"
      failure="Не удалось удалить блог"
      :action="deleteBlog"
    />
  </div>
</template>

<style scoped lang="sass">
.blog-settings
  max-width: $grid-step * 200

.settings-section
  margin-bottom: $big
  padding: $medium
  background-color: $bg-element

// Framed, and in the neutral line of the page: the danger is said by the
// heading and the sentence under it, and the confirmation that follows the
// press carries no red either.
.danger-zone
  border: 1px solid $border

.danger-hint
  display: block
  margin-bottom: $small
</style>
