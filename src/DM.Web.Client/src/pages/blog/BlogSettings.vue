<script setup lang="ts">
/**
 * The blog management page. Each section is drawn only for the viewers whose
 * saves the server would accept, by the intentions named at the computeds
 * below; a viewer admitted to nothing gets the site's "not found".
 */
import { computed, ref } from "vue";
import { useRouter } from "vue-router";
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
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import BlogInfoSection from "./settings/BlogInfoSection.vue";
import RubricsSection from "./settings/RubricsSection.vue";
import RolesSection from "./settings/RolesSection.vue";
import BlacklistSection from "./settings/BlacklistSection.vue";
import InvitationsSection from "./settings/InvitationsSection.vue";
import { SettingsSection } from "@/shared/ui/SettingsSection";
import { OWNS_REFUSAL } from "@/shared/api/interceptor";
import { useNotFoundWhen } from "@/shared/lib/composables/useNotFoundWhen";

const router = useRouter();
const blogStore = useBlogDetailsStore();
const { isOwner, canManage } = storeToRefs(blogStore);
const { user } = storeToRefs(useAuthStore());

// BlogIntention.EditSettings: the information form and the invitations. Also
// what admits a viewer to the page.
const canEditSettings = computed(
  () => canManage.value || userIsSeniorModerator(user.value),
);

// BlogIntention.Edit: the blacklist, its read included, and assistant removal;
// no senior-moderator clause here, unlike the game.
const canEditBlog = computed(() => isOwner.value || userIsAdmin(user.value));

// ManageRubrics, InviteAssistant and CancelInvitation are the owner's
// (`isOwner`); InviteReader the owner's and the assistants' (`canManage`).

// Who the viewer is, settled by restoreSession: deciding before it would send
// an author away from his blog.
const sessionSettled = ref(false);
const settle = () => (sessionSettled.value = true);
restoreSession().then(settle, settle);

const refused = computed(() => sessionSettled.value && !canEditSettings.value);

useNotFoundWhen(refused);

const confirmDelete = ref(false);

// The window waits for it, prints a refusal in itself and closes on success alone.
async function deleteBlog() {
  // Read before the request: leaving the blog zone wipes the store.
  const author = blogStore.blog?.author?.username ?? "";
  const error = await blogStore.deleteBlog(OWNS_REFUSAL);
  if (error) return { error };
  // To the author's blog list by its tab name (/users/X/blogs only redirects to
  // the profile). No toast: the missing row is the confirmation.
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
      <InvitationsSection :can-invite="canManage" :can-cancel="isOwner" />

      <SettingsSection v-if="isOwner" class="danger-zone" title="Опасная зона">
        <secondary-text class="danger-hint">
          Удаление блога необратимо
        </secondary-text>
        <Button type="button" class="delete-blog" @click="confirmDelete = true">
          Удалить блог
        </Button>
      </SettingsSection>
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

// Framed in the neutral line: the danger is said in words.
.danger-zone
  border: 1px solid $border

.danger-hint
  display: block
  margin-bottom: $small
</style>
