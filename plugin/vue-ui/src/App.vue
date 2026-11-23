<script setup>
import { onMounted } from 'vue';
import { useThemeStore } from './store/useThemeStore';
import { Toaster } from '@/components/ui/sonner';
import { Loading } from '@/components/ui/loading';
import 'vue-sonner/style.css';

const themeStore = useThemeStore();

onMounted(() => {
  // 初始化主题（自动跟随系统）
  themeStore.initTheme();
});
</script>

<template>
  <router-view />
  <Toaster />
  <Loading />
</template>
