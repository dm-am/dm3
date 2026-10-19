<script setup lang="ts">
// Only the controls: where the toolbar stands and what a press does are the feed's.
import { computed } from "vue";
import type { RouteLocationRaw } from "vue-router";
import type { User } from "@/shared/api/models/common/user";
import { Tooltip } from "@/shared/ui/Tooltip";
import { SvgIcon } from "@/shared/ui/Icon";
import { symbols } from "@/shared/lib/utils/icons";
import { getLikesTooltip } from "@/shared/lib/utils/chat";

const props = defineProps<{
  /** "Подтвердить удаление" and "Отмена" stand in place of the controls. */
  confirmingDelete: boolean;
  /** Who likes the message: the like's tooltip lists them. */
  likes: readonly Pick<User, "username">[];
  /** The reader likes the message: "Нравится" is pressed. */
  liked: boolean;
  canLike: boolean;
  canEdit: boolean;
  canDelete: boolean;
  /** The reader may warn the author: a moderator in the global chat. */
  canWarn: boolean;
  /** Where the link to the message leads. */
  linkTo: RouteLocationRaw;
}>();

const emit = defineEmits<{
  like: [];
  edit: [];
  delete: [];
  "confirm-delete": [];
  "cancel-delete": [];
  warn: [];
}>();

// The word names the button; its tooltip lists the likers, as LikeBadge's does.
const likersText = computed(() => getLikesTooltip(props.likes));
</script>

<template>
  <div class="msg-toolbar">
    <template v-if="confirmingDelete">
      <Tooltip text="Подтвердить удаление">
        <button
          type="button"
          class="toolbar-btn toolbar-btn-delete-confirm"
          aria-label="Подтвердить удаление"
          @click="emit('confirm-delete')"
        >
          <SvgIcon name="trash" />
        </button>
      </Tooltip>
      <Tooltip text="Отмена">
        <button
          type="button"
          class="toolbar-btn toolbar-btn-cancel"
          aria-label="Отмена"
          @click="emit('cancel-delete')"
        >
          {{ symbols.close }}
        </button>
      </Tooltip>
    </template>
    <template v-else>
      <Tooltip v-if="canLike" :text="likersText" :disabled="!likersText">
        <button
          type="button"
          class="toolbar-btn toolbar-btn-text"
          :class="{ active: liked }"
          :aria-pressed="liked"
          @click="emit('like')"
        >
          Нравится
        </button>
      </Tooltip>
      <Tooltip v-if="canEdit" text="Редактировать">
        <button
          type="button"
          class="toolbar-btn"
          aria-label="Редактировать"
          @click="emit('edit')"
        >
          <SvgIcon name="pencil" />
        </button>
      </Tooltip>
      <Tooltip v-if="canDelete" text="Удалить">
        <button
          type="button"
          class="toolbar-btn"
          aria-label="Удалить"
          @click="emit('delete')"
        >
          <SvgIcon name="trash" />
        </button>
      </Tooltip>
      <Tooltip v-if="canWarn" text="Оставить предупреждение">
        <button
          type="button"
          class="toolbar-btn toolbar-btn-warn"
          aria-label="Оставить предупреждение"
          @click="emit('warn')"
        >
          {{ symbols.warning }}
        </button>
      </Tooltip>
      <!-- Not the current page, so it never says it is -->
      <Tooltip text="Ссылка на сообщение">
        <router-link
          class="toolbar-btn"
          :to="linkTo"
          aria-label="Ссылка на сообщение"
          :aria-current="undefined"
        >
          <SvgIcon name="anchor" />
        </router-link>
      </Tooltip>
    </template>
  </div>
</template>

<style scoped lang="sass">
@use "@/assets/styles/MessageToolbar" as *

// The toolbar's look, from its one partial.
+message-toolbar
</style>
