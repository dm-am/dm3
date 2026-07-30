<script setup lang="ts">
/**
 * UsernameChangeRejectDialog — reject a username change request with a
 * required reason. Dialog twin of the former hand-rolled modal in
 * ModerationUsernameChanges.vue.
 */
import { ref, computed } from "vue";
import {
  moderationApi,
  UsernameChangeRequestStatus,
  type UsernameChangeRequest,
} from "@/entities/moderation";
import Dialog from "@/shared/ui/Layout/Dialog.vue";
import DialogTitle from "@/shared/ui/Layout/DialogTitle.vue";
import Form from "@/shared/ui/Form/Form.vue";
import FormField from "@/shared/ui/Form/FormField.vue";

const props = defineProps<{
  request: UsernameChangeRequest;
}>();

const emit = defineEmits<{
  (e: "success", request: UsernameChangeRequest): void;
  (e: "cancel"): void;
}>();

const comment = ref("");
const sending = ref(false);
const error = ref<string | null>(null);

const canSubmit = computed(() => comment.value.trim().length > 0);

async function submit() {
  if (!canSubmit.value || sending.value) return;
  sending.value = true;
  error.value = null;
  try {
    await moderationApi.resolveUsernameChangeRequest(props.request.id, {
      status: UsernameChangeRequestStatus.Rejected,
      comment: comment.value,
    });
    emit("success", props.request);
  } catch {
    error.value = "Не удалось отклонить запрос";
  } finally {
    sending.value = false;
  }
}
</script>

<template>
  <Dialog narrow>
    <DialogTitle>Отклонение запроса</DialogTitle>

    <p class="reject-context">
      Отклонить запрос на смену имени от
      <strong>{{ request.currentUsername }}</strong
      >?
    </p>

    <Form
      :valid="canSubmit"
      :loading="sending"
      action="Отклонить"
      cancel="Отмена"
      @submit="submit"
      @cancel="emit('cancel')"
    >
      <FormField
        label="Причина отклонения"
        name="reject-reason"
        :errors="error ? [error] : []"
      >
        <textarea
          id="reject-reason"
          v-model="comment"
          placeholder="Укажите причину отклонения…"
          rows="4"
        ></textarea>
      </FormField>
    </Form>
  </Dialog>
</template>

<style scoped lang="sass">
.reject-context
  margin: 0 0 $small
</style>
