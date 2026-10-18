<script setup lang="ts">
/**
 * Removing somebody's account, as a senior moderator: the button of the
 * section "Аккаунт" of the profile's "Модерация" tab, and what follows the
 * window it opens (AccountRemovalDialog).
 *
 * The site has no button for this anywhere else and is not going to get one:
 * the owner asked for removal to stay an exception that goes through
 * moderation, so the path a reader is told about is a letter to support, and
 * this is where that letter ends. Advertising it would turn "in exceptional
 * cases" into a self-service door.
 *
 * Gated on the viewer's rank rather than on a flag from the server, like the
 * second-factor control beside it: the moderated profile carries no permission
 * for this action. The server refuses over an equal rank and over an
 * administrator on its own, and the one case worth not drawing is the viewer's
 * own profile.
 *
 * A removal ends this screen, so it ends by leaving it. The profile behind the
 * tab cannot be made to read as removed: every read of a profile goes through
 * the user lookup, which drops removed rows, so from this moment both this page
 * and its moderation answer 404 - and a 404 reads as "нет такого имени", not
 * as "аккаунт удален". Staying would leave a living-looking profile over a tab
 * that has just taken the account away.
 *
 * "Удаленные пользователи" is the one screen that can see a removed account at all,
 * and it takes the name out of the address, so the moderator lands on the row
 * he has just made - with the rank, the dates, and "Восстановить" on it if the
 * removal was a mistake. Nothing is said out loud on top of that: the row is
 * the whole of what happened, and the sessions went with the account, which
 * authentication refuses by name from here on (AuthenticationService).
 */
import { computed, reactive, toRef } from "vue";
import { useRouter } from "vue-router";
import { useModal } from "vue-final-modal";
import { userIsSeniorModerator } from "@/entities/user";
import { useAuthStore } from "@/shared/stores";
import Button from "@/shared/ui/Button/Button.vue";
import AccountRemovalDialog from "./AccountRemovalDialog.vue";

const props = defineProps<{
  targetUsername: string;
}>();

const emit = defineEmits<{
  (e: "updated"): void;
}>();

const auth = useAuthStore();
const router = useRouter();

const visible = computed(
  () =>
    userIsSeniorModerator(auth.user) &&
    auth.user?.username.toLowerCase() !== props.targetUsername.toLowerCase(),
);

const { open, close } = useModal({
  component: AccountRemovalDialog,
  attrs: reactive({
    targetUsername: toRef(props, "targetUsername"),
    onSuccess: () => {
      close();
      // Told before the page goes: if the navigation is ever refused, the
      // profile left behind must not keep a tab read from an account that is
      // gone.
      emit("updated");
      void router.push({
        name: "moderation-removed-accounts",
        query: { search: props.targetUsername },
      });
    },
    onCancel: () => close(),
  }),
});
</script>

<template>
  <div v-if="visible" class="mod-removal_actions">
    <Button type="button" @click="open">Удалить аккаунт</Button>
  </div>
</template>

<style scoped lang="sass">
// One control of the row of the section "Аккаунт", beside the second factor.
.mod-removal_actions
  display: flex
  flex-shrink: 0
</style>
