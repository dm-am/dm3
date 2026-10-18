<script setup lang="ts">
/**
 * "Удаление аккаунта": the window a senior moderator removes an account in.
 *
 * A form window (Dialog, 580) with the one field the server requires, the
 * reason. The field used to stand open on the profile, under the second-factor
 * control and next to the moderator's everyday actions, with an explanation in
 * three sentences above it; the one irreversible action of the panel now sits
 * behind its button and says what it does where it is done.
 *
 * The window runs the request (Dialog `action`): it waits shut, prints a
 * refusal over its buttons or under the field the server named, and closes on
 * success alone. The reason is typed text, so the window asks before it is
 * dropped. What follows a removal - leaving a profile that now answers 404 for
 * the list of removed accounts - is the opener's, on `success`.
 */
import { computed, ref } from "vue";
import { moderationApi } from "@/entities/moderation";
import Dialog from "@/shared/ui/Layout/Dialog.vue";
import DialogTitle from "@/shared/ui/Layout/DialogTitle.vue";
import Form from "@/shared/ui/Form/Form.vue";
import FormField from "@/shared/ui/Form/FormField.vue";

const props = defineProps<{
  targetUsername: string;
}>();

const emit = defineEmits<{
  (e: "success"): void;
  (e: "cancel"): void;
}>();

const reason = ref("");

// An account removed with nothing written down leaves the next moderator with
// a missing person and no record of who decided what.
const canSubmit = computed(() => reason.value.trim().length > 0);

async function send() {
  const result = await moderationApi.removeAccount(
    props.targetUsername,
    reason.value.trim(),
    { ownsRefusal: true, ownsRateLimit: true },
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
// One field step down to the field, as between two fields.
.removal-context
  margin: 0 0 $medium
</style>
