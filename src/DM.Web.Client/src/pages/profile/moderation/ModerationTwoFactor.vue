<script setup lang="ts">
/**
 * Taking a colleague's second factor off, as the second administrator: the
 * only way a privileged account loses its factor. Nothing on the wire says
 * whether the account has one, so the server answers that in its own words;
 * after a confirmed press the block says what it did in place of the button.
 */
import { ref } from "vue";
import { accountApi } from "@/entities/user";
import Button from "@/shared/ui/Button/Button.vue";
import { ConfirmDialog } from "@/shared/ui/ConfirmDialog";
import { OWNS_REFUSAL } from "@/shared/api/interceptor";

const props = defineProps<{
  targetUsername: string;
}>();

const emit = defineEmits<{
  (e: "updated"): void;
}>();

const confirming = ref(false);

/** The factor is off, and this screen is the one that took it off. */
const cleared = ref(false);

// The window waits for the request, says a refusal in itself and closes on
// success alone; "Двухфакторная аутентификация не включена" is the server's.
async function clear() {
  const result = await accountApi.clearTwoFactorFor(
    props.targetUsername,
    OWNS_REFUSAL,
  );
  if (result.error) return result;

  cleared.value = true;
  emit("updated");
  return result;
}
</script>

<template>
  <div>
    <!-- Both halves of the press in one line; role="status" announces it. -->
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
.mod-two-factor_state
  margin: 0
</style>
