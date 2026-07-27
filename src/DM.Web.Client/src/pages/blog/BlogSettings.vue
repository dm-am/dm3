<script setup lang="ts">
/**
 * BlogSettings — the blog management page for owner and assistant (dev doc
 * 4.2.3.6.6 "Настройки блога"). Mirrors GameSettings and composes the
 * sections (listed by their on-page headings):
 *  - "Информация блога" (title / draft visibility / comments switch)
 *  - "Управление рубриками" (rubric create / delete)
 *  - "Управление ролями" (assistant invite / remove)
 *  - "Черный список" (blog blacklist)
 *  - "Приглашения" (reader invites)
 *  - "Опасная зона" (owner-only blog delete)
 */
import { computed, ref } from "vue";
import { useRouter } from "vue-router";
import { storeToRefs } from "pinia";
import { useBlogDetailsStore } from "@/entities/blog";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import PageTitle from "@/shared/ui/Layout/PageTitle.vue";
import BlockTitle from "@/shared/ui/Layout/BlockTitle.vue";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { useToast } from "@/shared/lib/composables/useToast";
import BlogInfoSection from "./settings/BlogInfoSection.vue";
import RubricsSection from "./settings/RubricsSection.vue";
import RolesSection from "./settings/RolesSection.vue";
import BlacklistSection from "./settings/BlacklistSection.vue";
import InvitationsSection from "./settings/InvitationsSection.vue";

const router = useRouter();
const toast = useToast();
const blogStore = useBlogDetailsStore();
const { isOwner, canManage } = storeToRefs(blogStore);

// Settings are open to the blog leads (owner and assistant).
const canEdit = computed(() => canManage.value);

// --- Danger zone (owner-only blog delete) ---
const confirmDelete = ref(false);

async function deleteBlog() {
  confirmDelete.value = false;
  const ok = await blogStore.deleteBlog();
  if (ok) {
    toast.success("Блог удален");
    router.push({ name: "blogs" });
  } else {
    toast.error("Не удалось удалить блог");
  }
}
</script>

<template>
  <div class="blog-settings">
    <page-title>Настройки блога</page-title>

    <secondary-text v-if="!canEdit">
      Настройки блога доступны мастеру блога и ассистентам.
    </secondary-text>

    <template v-else>
      <BlogInfoSection />
      <RubricsSection />
      <RolesSection />
      <BlacklistSection />
      <InvitationsSection />

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
    background-color: rgba($accent-red, 0.1)
</style>
