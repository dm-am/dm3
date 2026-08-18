<script setup lang="ts">
/**
 * BlogSettings — the blog management page (dev doc 4.2.3.6.6 "Настройки
 * блога"). Mirrors GameSettings and composes the sections (listed by their
 * on-page headings):
 *  - "Информация блога" (title / draft visibility / comments switch)
 *  - "Управление рубриками" (rubric create / delete)
 *  - "Управление ролями" (assistant invite / remove)
 *  - "Черный список" (blog blacklist)
 *  - "Приглашения" (reader invites)
 *  - "Опасная зона" (owner-only blog delete)
 *
 * As on the game side, the sections do not share one audience: each is drawn
 * only for the viewers whose saves the server would accept. The widths are
 * spelled out at the computeds below, each named after its intention.
 */
import { computed, ref } from "vue";
import { useRouter } from "vue-router";
import { storeToRefs } from "pinia";
import { useBlogDetailsStore } from "@/entities/blog";
import {
  useAuthStore,
  userIsAdmin,
  userIsSeniorModerator,
} from "@/entities/user";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import BlockTitle from "@/shared/ui/Layout/BlockTitle.vue";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { LoginPrompt } from "@/features/auth";
import { useToast } from "@/shared/lib/composables/useToast";
import BlogInfoSection from "./settings/BlogInfoSection.vue";
import RubricsSection from "./settings/RubricsSection.vue";
import RolesSection from "./settings/RolesSection.vue";
import BlacklistSection from "./settings/BlacklistSection.vue";
import InvitationsSection from "./settings/InvitationsSection.vue";
import { notifyFailure } from "@/shared/lib/errors";

const router = useRouter();
const toast = useToast();
const blogStore = useBlogDetailsStore();
const { isOwner, canManage } = storeToRefs(blogStore);
const { user } = storeToRefs(useAuthStore());

// BlogIntention.EditSettings — the information form (BlogService.Update) and
// the invitation list (BlogInvitationService.GetPendingInvitations). The blog
// leads (owner and assistant) and senior moderation; this is also what admits
// a viewer to the page. The refusal names the blog roles only — staff powers
// are not interface copy.
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

// --- Danger zone (owner-only blog delete) ---
const confirmDelete = ref(false);

async function deleteBlog() {
  confirmDelete.value = false;
  const error = await blogStore.deleteBlog();
  if (error) {
    notifyFailure(error, "Не удалось удалить блог");
    return;
  }
  toast.success("Блог удален");
  router.push({ name: "blogs" });
}
</script>

<template>
  <div class="blog-settings">
    <LoginPrompt v-if="!user" action="управлять блогом" />

    <secondary-text v-else-if="!canEditSettings">
      Настройки блога доступны мастеру блога и ассистентам.
    </secondary-text>

    <template v-else>
      <BlogInfoSection />
      <RubricsSection v-if="isOwner" />
      <RolesSection :can-invite="isOwner" :can-remove="canEditBlog" />
      <BlacklistSection v-if="canEditBlog" />
      <InvitationsSection :can-invite="canInviteReader" :can-cancel="isOwner" />

      <section v-if="isOwner" class="settings-section danger-zone">
        <block-title>Опасная зона</block-title>
        <secondary-text class="danger-hint">
          Удаление блога необратимо.
        </secondary-text>
        <button type="button" class="delete-blog" @click="confirmDelete = true">
          Удалить блог
        </button>
      </section>
    </template>

    <ConfirmDialog
      :show="confirmDelete"
      title="Удаление блога"
      message="Удалить этот блог? Действие необратимо."
      confirm-label="Удалить блог"
      danger
      @confirm="deleteBlog"
      @cancel="confirmDelete = false"
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
  border-radius: $border-radius

.danger-zone
  border: 1px solid $accent-red

.danger-hint
  display: block
  margin-bottom: $small

.delete-blog
  padding: $small $medium
  border: 1px solid $accent-red
  border-radius: $border-radius
  background: none
  color: $accent-red
  cursor: pointer
  font: inherit

  &:hover
    +tint($accent-red, 10%)
</style>
