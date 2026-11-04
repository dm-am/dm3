<script setup lang="ts">
/**
 * Removing somebody's account, as a senior moderator: the button of the
 * section "Аккаунт", and what follows its window. The profile then answers
 * 404, so the moderator is taken to "Удаленные пользователи", narrowed to the
 * name, where the row he made is the whole answer.
 */
import { reactive, toRef } from "vue";
import { useRouter } from "vue-router";
import { useModal } from "vue-final-modal";
import Button from "@/shared/ui/Button/Button.vue";
import AccountRemovalDialog from "./AccountRemovalDialog.vue";

const props = defineProps<{
  targetUsername: string;
}>();

const emit = defineEmits<{
  (e: "updated"): void;
}>();

const router = useRouter();

const { open, close } = useModal({
  component: AccountRemovalDialog,
  attrs: reactive({
    targetUsername: toRef(props, "targetUsername"),
    onSuccess: () => {
      close();
      // Told before the page goes, so a refused navigation leaves no tab read from a gone account.
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
  <Button type="button" @click="open">Удалить аккаунт</Button>
</template>
