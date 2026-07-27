<script setup lang="ts">
import { storeToRefs } from "pinia";
import { useUserStore } from "@/entities/user";
import PageTitle from "@/shared/ui/Layout/PageTitle.vue";
import { CreateGameForm } from "@/features/create-game";
import { LoginPrompt } from "@/features/auth";

const { user } = storeToRefs(useUserStore());
</script>

<template>
  <page-title>Создать игру</page-title>

  <LoginPrompt v-if="!user" action="создать игру" />

  <CreateGameForm v-else />
</template>
