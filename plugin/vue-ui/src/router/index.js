import { createRouter, createWebHashHistory } from 'vue-router';
import SettingsView from '@/views/SettingsView.vue';

const router = createRouter({
  history: createWebHashHistory(),
  routes: [{ path: '/', name: 'Settings', component: SettingsView }],
});

window.router = {
  push: path => router.push(path),
  replace: path => router.replace(path),
  back: () => router.back(),
  current: () => router.currentRoute.value.path,
};

export default router;
