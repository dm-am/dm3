<script setup lang="ts">
/**
 * Taking a colleague's second factor off, as the second administrator.
 *
 * The only way a privileged account gets its factor off at all: the mailed path
 * is closed for the ranks that owe a factor, and that closure is the reason the
 * site has two administrators. It hands the caller nothing - no session of the
 * other account and none of its rights - only a way in by password for its
 * owner, and the end of every session it had.
 *
 * Gated on the viewer's role rather than on a permission from the server: the
 * moderated profile carries no flag for this action and none for "that account
 * has a factor" either. So the button is drawn for an administrator looking at
 * somebody else, and an account without a factor is answered by the server in
 * its own words. The one case worth not drawing is the viewer's own profile,
 * where the server refuses by design and the settings page is the right place.
 */
import { computed, ref } from "vue";
import { accountApi, userIsAdmin } from "@/entities/user";
import { useAuthStore } from "@/shared/stores";
import Button from "@/shared/ui/Button/Button.vue";
import SecondaryText from "@/shared/ui/Layout/SecondaryText.vue";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import { useToast } from "@/shared/lib/composables/useToast";
import { notifyFailure } from "@/shared/lib/errors";

const props = defineProps<{
  targetUsername: string;
}>();

const emit = defineEmits<{
  (e: "updated"): void;
}>();

const auth = useAuthStore();
const toast = useToast();

const confirming = ref(false);
const clearing = ref(false);

const visible = computed(
  () =>
    userIsAdmin(auth.user) &&
    auth.user?.username.toLowerCase() !== props.targetUsername.toLowerCase(),
);

async function clear() {
  clearing.value = true;
  const { error } = await accountApi.clearTwoFactorFor(props.targetUsername);
  clearing.value = false;

  if (error) {
    // The server answers "Второй фактор не включен" when there was nothing to
    // take off, which is the one thing the client cannot know in advance.
    notifyFailure(error, "Не удалось снять второй фактор");
    return;
  }

  confirming.value = false;
  toast.success("Второй фактор снят, сессии завершены");
  emit("updated");
}
</script>

<template>
  <div v-if="visible" class="mod-section">
    <h4 class="mod-section_title">Второй фактор</h4>

    <SecondaryText>
      Снять второй фактор коллеге, потерявшему телефон. Снятие по письму для его
      ранга закрыто, и другого способа вернуть ему вход нет.
    </SecondaryText>

    <div class="mod-two-factor_actions">
      <Button type="button" @click="confirming = true">
        Снять второй фактор
      </Button>
    </div>

    <ConfirmDialog
      v-model:show="confirming"
      title="Снять второй фактор"
      message="Второй фактор будет снят, все сессии этого аккаунта завершатся. Права модератора и администратора не заработают, пока он не настроит второй фактор заново. Запись об этом уйдет в журналы безопасности обоих аккаунтов."
      confirm-label="Снять"
      danger
      :loading="clearing"
      @confirm="clear"
      @cancel="confirming = false"
    />
  </div>
</template>

<style scoped lang="sass">
.mod-section
  margin-bottom: $medium

.mod-section_title
  margin: 0 0 $small 0
  color: $heading

.mod-two-factor_actions
  display: flex
  gap: $small
  margin-top: $small
</style>
