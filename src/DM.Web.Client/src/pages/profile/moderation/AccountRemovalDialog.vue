<script setup lang="ts">
/**
 * "Удаление аккаунта": a form window with the one field the server requires,
 * the reason. It runs the request and closes on success alone; what follows
 * is the opener's, on `success`.
 */
import { computed, ref } from "vue";
import { moderationApi } from "@/entities/moderation";
import Dialog from "@/shared/ui/Layout/Dialog.vue";
import DialogTitle from "@/shared/ui/Layout/DialogTitle.vue";
import Form from "@/shared/ui/Form/Form.vue";
import FormField from "@/shared/ui/Form/FormField.vue";
import { OWNS_REFUSAL } from "@/shared/api/interceptor";

const props = defineProps<{
  targetUsername: string;
}>();

const emit = defineEmits<{
  (e: "success"): void;
  (e: "cancel"): void;
}>();

const reason = ref("");

// A removal without a reason leaves the next moderator no record of who decided what.
const canSubmit = computed(() => reason.value.trim().length > 0);

async function send() {
  const result = await moderationApi.removeAccount(
    props.targetUsername,
    reason.value,
    OWNS_REFUSAL,
  );
  if (!result.error) emit("success");
  return result;
}
</script>

<template>
  <Dialog
    :action="send"
    failure="Не удалось удалить аккаунт"
    :dirty="reason.trim().length > 0"
    discard-text="Набранная причина пропадет"
    @close="emit('cancel')"
  >
    <DialogTitle>Удаление аккаунта</DialogTitle>

    <p class="removal-context">
      Аккаунт: <strong>{{ targetUsername }}</strong>
    </p>
    <p class="removal-context">
      Записи останутся на месте под подписью "Аккаунт удален", имя и почта
      останутся занятыми. Вернуть имя над записями человек сможет через
      обращение в поддержку.
    </p>

    <Form :valid="canSubmit" action="Удалить аккаунт" cancel="Отмена">
      <FormField label="Причина" name="removal-reason" field="reason">
        <textarea
          id="removal-reason"
          v-model="reason"
          placeholder="Номер обращения или что случилось"
          rows="4"
        ></textarea>
      </FormField>
    </Form>
  </Dialog>
</template>

<style scoped lang="sass">
// One field step down to the field.
.removal-context
  margin: 0 0 $medium
</style>
