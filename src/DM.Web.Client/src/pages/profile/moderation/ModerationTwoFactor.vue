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
 *
 * Afterwards the block says what the press did, in place of the button, the
 * way the password form says "Пароль изменен, остальные сессии завершены" in
 * place of its fields (AccountSecuritySection). Before the press there is no
 * state to say - nothing on the wire tells this screen whether the account has
 * a factor - but a press the server confirmed leaves it known. A reload puts
 * the offer back, because the knowledge belongs to this press and not to the
 * profile.
 *
 * The line carries both halves of what the endpoint did, and there is no toast
 * on top of it. The ended sessions have no screen of their own - the site
 * lists nobody's sessions but the reader's own (AccountSessionsSection) - and
 * that used to be the argument for a toast. It is not one: the reader is
 * looking at this block when the answer arrives, and a line that already
 * stands there has room for the second half. A toast would say the same thing
 * from the corner and be gone in seconds, while the line stays as long as the
 * page does. The words are the confirmation's own ("будет отключена, все
 * сессии этого аккаунта завершатся"), now in the past tense, so the promise
 * and its receipt read the same.
 *
 * The block is one button of the section "Аккаунт" of the profile's
 * "Модерация" tab, beside the removal of the account; the heading is the
 * section's. The button says the full term, the one the confirmation and the
 * glossary use, and the line that stands in its place after the press breaks
 * as any text does. The paragraph that used to
 * explain when the action is for (a colleague who lost the phone) is gone
 * rather than moved into the confirmation: a confirmation says one sentence,
 * the consequence (confirmCopy.spec.ts), and the administrators who press
 * this know the case.
 */
import { computed, ref } from "vue";
import { accountApi, userIsAdmin } from "@/entities/user";
import { useAuthStore } from "@/shared/stores";
import Button from "@/shared/ui/Button/Button.vue";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";

const props = defineProps<{
  targetUsername: string;
}>();

const emit = defineEmits<{
  (e: "updated"): void;
}>();

const auth = useAuthStore();

const confirming = ref(false);

/** The factor is off, and this screen is the one that took it off. */
const cleared = ref(false);

const visible = computed(
  () =>
    userIsAdmin(auth.user) &&
    auth.user?.username.toLowerCase() !== props.targetUsername.toLowerCase(),
);

// The request of the confirmation. The window waits for it, prints a refusal in
// itself and closes on success alone, so the interceptor keeps quiet about it.
// The server answers "Двухфакторная аутентификация не включена" when there was
// nothing to take off, which is the one thing the client cannot know in
// advance, and the window prints that sentence as it stands.
async function clear() {
  const result = await accountApi.clearTwoFactorFor(props.targetUsername, {
    ownsRefusal: true,
    ownsRateLimit: true,
  });
  if (result.error) return result;

  cleared.value = true;
  emit("updated");
  return result;
}
</script>

<template>
  <div v-if="visible" class="mod-two-factor">
    <!-- Both halves of the press in the one line, and no toast beside it: the
         ended sessions are not shown by any screen, but the reader is looking
         right here, and this line outlives a toast by the life of the page.
         role="status" keeps the announcement the toast used to make. -->
    <p v-if="cleared" class="mod-two-factor_state" role="status">
      Двухфакторная аутентификация отключена, сессии завершены
    </p>

    <div v-else class="mod-two-factor_actions">
      <Button type="button" @click="confirming = true">
        Отключить двухфакторную аутентификацию
      </Button>
    </div>

    <ConfirmDialog
      v-model:show="confirming"
      title="Отключить двухфакторную аутентификацию?"
      message="Сессии этого аккаунта завершатся, а входить он будет по одному паролю, пока не настроит защиту заново"
      confirm-label="Отключить"
      failure="Не удалось отключить двухфакторную аутентификацию"
      :action="clear"
    />
  </div>
</template>

<style scoped lang="sass">
// One control of the row of the section "Аккаунт", beside the removal.
.mod-two-factor
  display: flex
  flex-shrink: 0

.mod-two-factor_state
  margin: 0
  align-self: center
</style>
