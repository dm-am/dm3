<script setup lang="ts">
import { ref, computed } from "vue";
import { storeToRefs } from "pinia";
import { useCommunityStore } from "@/stores/community";
import { useUserStore } from "@/stores";
import TheLoader from "@/components/TheLoader.vue";
import SecondaryText from "@/components/layout/SecondaryText.vue";
import TextArea from "@/components/inputs/TextArea.vue";
import TheButton from "@/components/inputs/TheButton.vue";

const communityStore = useCommunityStore();
const { selectedUser: user, editableUser } = storeToRefs(communityStore);
const { user: currentUser } = storeToRefs(useUserStore());

const isEditing = ref(false);
const isSaving = ref(false);
const editedInfo = ref("");

const isCurrentUser = computed(() => {
  const currentLogin = currentUser.value?.login;
  const profileLogin = user.value?.login;
  return !!(currentLogin && profileLogin && currentLogin === profileLogin);
});

const startEdit = async () => {
  if (!user.value) return;

  await communityStore.fetchEditableUser(user.value.login);
  editedInfo.value = editableUser.value?.info || "";
  isEditing.value = true;
};

const cancelEdit = () => {
  isEditing.value = false;
  editedInfo.value = "";
};

const saveEdit = async () => {
  if (!user.value) return;

  isSaving.value = true;
  const { error } = await communityStore.updateUser(user.value.login, {
    info: editedInfo.value,
  });
  isSaving.value = false;

  if (!error) {
    isEditing.value = false;
    editedInfo.value = "";
  }
};
</script>

<template>
  <the-loader v-if="!user" :big="true" />

  <template v-else>
    <template v-if="isEditing">
      <text-area
        v-model="editedInfo"
        :disabled="isSaving"
        class="info-editor"
      />
      <div class="edit-actions">
        <the-button :loading="isSaving" @click="saveEdit">
          Сохранить
        </the-button>
        <the-button :disabled="isSaving" @click="cancelEdit">
          Отмена
        </the-button>
      </div>
    </template>

    <template v-else>
      <div v-if="user.info" v-html="user.info" />
      <secondary-text v-else>
        Пользователь ничего о себе не написал...
      </secondary-text>

      <the-button v-if="isCurrentUser" class="edit-button" @click="startEdit">
        Редактировать
      </the-button>
    </template>
  </template>
</template>

<style scoped lang="sass">
@import "src/assets/styles/Variables"

.info-editor
  margin-bottom: $small

  :deep(textarea)
    min-height: $grid-step * 50

.edit-actions
  display: flex
  gap: $small

.edit-button
  margin-top: $medium
</style>
