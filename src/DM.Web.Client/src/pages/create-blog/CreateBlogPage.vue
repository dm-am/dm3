<script setup lang="ts">
import { storeToRefs } from "pinia";
import { useUserStore } from "@/entities/user";
import PageTitle from "@/shared/ui/Layout/PageTitle.vue";
import { CreateBlogForm } from "@/features/create-blog";
import { LoginPrompt } from "@/features/auth";

const { user } = storeToRefs(useUserStore());
</script>

<template>
  <page-title>Создание блога</page-title>

  <LoginPrompt v-if="!user" action="создать блог" />

  <CreateBlogForm v-else />
</template>
