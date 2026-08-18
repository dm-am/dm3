<script setup lang="ts">
/**
 * ModerationWatch — "Наблюдение модерации", a block of the profile's moderation
 * panel (doc 4.2.2.19).
 *
 * It lives here and not on "Нарушители" because the two answer different
 * questions. That page is a computed list: active warning points and active
 * bans, recomputed on every read and gone when they expire. This is a switch a
 * moderator throws by hand and nothing clears on its own, and both halves of it
 * — whether it is on, and whether the viewer may touch it — are served on the
 * moderated profile and nowhere else.
 *
 * The block gates itself on the permission the server sends, the way the
 * violations block gates its own buttons: the endpoint is Moderator+ while the
 * panel around it opens wider, and a control the server would answer 403 to is
 * a control this panel must not draw.
 *
 * The word the domain uses for the people this is aimed at does not appear in
 * the interface. What a moderator needs to know is what the switch does: from
 * now on this user's new games and blogs start in premoderation.
 */
import { ref } from "vue";
import type { ModerationPermissions } from "@/shared/api/models/moderation";
import type { Username } from "@/shared/api/models/community";
import { moderationApi } from "@/entities/moderation";
import Button from "@/shared/ui/Button/Button.vue";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { notifyFailure } from "@/shared/lib/errors";

const props = defineProps<{
  /** Whether the watch is on right now. */
  underWatch: boolean;
  permissions: ModerationPermissions;
  targetUsername: string;
}>();

const emit = defineEmits<{
  (e: "updated"): void;
}>();

const saving = ref(false);

async function toggleWatch() {
  saving.value = true;
  const { error } = await moderationApi.setModerationWatch(
    props.targetUsername as Username,
    !props.underWatch,
  );
  saving.value = false;
  if (error) {
    notifyFailure(
      error,
      props.underWatch
        ? "Не удалось снять наблюдение"
        : "Не удалось взять под наблюдение",
    );
    return;
  }
  // The endpoint answers with the rebuilt profile, but the flag is read from
  // the parent's copy, so the parent is the one that has to refetch.
  emit("updated");
}
</script>

<template>
  <div v-if="permissions.canSetModerationWatch" class="mod-section">
    <h4 class="mod-section_title">Наблюдение модерации</h4>

    <p class="mod-watch_state">
      <template v-if="underWatch">
        Пользователь <strong>под наблюдением</strong>
      </template>
      <template v-else>Пользователь не под наблюдением</template>
    </p>

    <SecondaryText>
      Пока наблюдение включено, каждая новая игра и каждый новый блог этого
      пользователя начинают с премодерации. Наблюдение не снимается само.
    </SecondaryText>

    <div class="mod-watch_actions">
      <Button type="button" :loading="saving" @click="toggleWatch">
        {{ underWatch ? "Снять наблюдение" : "Взять под наблюдение" }}
      </Button>
    </div>
  </div>
</template>

<style scoped lang="sass">
.mod-section
  margin-bottom: $medium

.mod-section_title
  margin: 0 0 $small 0
  color: $heading

.mod-watch_state
  margin: 0 0 $minor 0

.mod-watch_actions
  display: flex
  gap: $small
  margin-top: $small
</style>
