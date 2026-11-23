<script setup>
import { computed, nextTick, onBeforeUnmount, onMounted, reactive, ref } from 'vue';
import { AppWindow, Crosshair, LocateFixed, MousePointer2, PanelsTopLeft, Repeat2, Volume2 } from 'lucide-vue-next';
import { Button } from '@/components/ui/button';
import SettingsToggle from '@/components/SettingsToggle.vue';
import CursorWrapPanel from '@/views/CursorWrapPanel.vue';
import FindMousePanel from '@/views/FindMousePanel.vue';
import { createSettingsApi, cursorWrapDefaults, findMouseDefaults, generalDefaults } from '@/api/settings';
import { useBridgeService, useBridgeServiceEvent } from '@/composables/useDotnetBridge';
import '@/settings.css';

const sections = [
  { id: 'drag', label: '窗口拖动', icon: AppWindow },
  { id: 'volume', label: '滚轮音量', icon: Volume2 },
  { id: 'desktop', label: '桌面与任务栏', icon: PanelsTopLeft },
  { id: 'find-mouse', label: '寻找鼠标', icon: LocateFixed },
  { id: 'cursor-wrap', label: '光标环绕', icon: Repeat2 },
];

const exclusionFields = [
  { field: 'excludedApps', label: '排除窗口', description: '匹配的窗口不会被拖动或调整大小。' },
  { field: 'modifierExcludedApps', label: '排除修饰键', description: '匹配的前台应用保留 Alt 或 Win 的原有行为。' },
];

const api = createSettingsApi();
const defaults = { general: generalDefaults, findMouse: findMouseDefaults, cursorWrap: cursorWrapDefaults };
const models = reactive({ general: { ...generalDefaults }, findMouse: { ...findMouseDefaults }, cursorWrap: { ...cursorWrapDefaults } });
const committed = { general: { ...generalDefaults }, findMouse: { ...findMouseDefaults }, cursorWrap: { ...cursorWrapDefaults } };
const pending = { general: new Map(), findMouse: new Map(), cursorWrap: new Map() };
const cursorWrapService = useBridgeService('CursorWrapSettings');
const drafts = reactive({ excludedApps: '', modifierExcludedApps: '' });
const active = ref('drag');
const ready = ref(false);
const loading = ref(false);
const saving = ref(0);
const applying = ref(false);
const picking = ref(null);
const status = ref('');
const isError = ref(false);
let tail = Promise.resolve();
let pickSession;

const general = computed(() => models.general);
const hasDrafts = computed(() => exclusionFields.some(({ field }) => drafts[field] !== general.value[field]));

useBridgeServiceEvent(cursorWrapService, 'StateChanged', snapshot => accept('cursorWrap', snapshot));

function activate(id, focus = false) {
  const changed = active.value !== id;
  if (changed && pickSession) {
    void cancelPick();
  }
  active.value = id;
  void nextTick(() => {
    if (changed) window.scrollTo({ top: 0, left: 0 });
    if (focus) document.getElementById('tab-' + id)?.focus();
  });
}

function navigateByKey(event) {
  const index = sections.findIndex(section => section.id === active.value);
  let next;
  switch (event.key) {
    case 'ArrowDown':
    case 'ArrowRight':
      next = (index + 1) % sections.length;
      break;
    case 'ArrowUp':
    case 'ArrowLeft':
      next = (index + sections.length - 1) % sections.length;
      break;
    case 'Home':
      next = 0;
      break;
    case 'End':
      next = sections.length - 1;
      break;
    default:
      return;
  }
  event.preventDefault();
  activate(sections[next].id, true);
}

function setStatus(message = '', error = false) {
  status.value = message;
  isError.value = error;
}

function accept(group, response) {
  if (!response || typeof response !== 'object') {
    throw new Error('宿主未返回配置。');
  }
  committed[group] = { ...defaults[group], ...response };
  sync(group);
}

function sync(group) {
  for (const field of Object.keys(defaults[group])) {
    if (!pending[group].has(field)) {
      models[group][field] = committed[group][field];
    }
  }
}

async function load() {
  loading.value = true;
  setStatus('正在读取设置…');
  try {
    const [generalSettings, findMouseSettings, cursorWrapSettings] = await Promise.all([
      api.general.load(), api.findMouse.load(), api.cursorWrap.load(),
    ]);
    accept('general', generalSettings);
    accept('findMouse', findMouseSettings);
    accept('cursorWrap', cursorWrapSettings);
    for (const { field } of exclusionFields) {
      drafts[field] = models.general[field];
    }
    ready.value = true;
    setStatus();
  } catch (error) {
    setStatus(error.message || '无法读取设置。', true);
  } finally {
    loading.value = false;
  }
}

function enqueue(work) {
  saving.value++;
  const job = tail.then(work).finally(() => saving.value--);
  tail = job.catch(() => {});
  return job;
}

function save(group, field, value) {
  models[group][field] = value;
  const ticket = {};
  pending[group].set(field, ticket);
  setStatus();
  void enqueue(async () => {
    try {
      accept(group, await api[group].save(field, value));
      if (!isError.value) {
        setStatus('已保存。');
      }
    } catch (error) {
      setStatus(error.message || '设置未保存。', true);
    } finally {
      if (pending[group].get(field) === ticket) {
        pending[group].delete(field);
      }
      sync(group);
    }
  });
}

function normalizeExclusion(text) {
  return text.split(/\r?\n/).map(line => line.trim()).filter(Boolean).join('\n');
}

async function applyExclusions() {
  if (applying.value) return;
  applying.value = true;
  const snapshot = { ...drafts };
  setStatus();
  try {
    await enqueue(async () => {
      accept('general', await api.applyExclusions(
        normalizeExclusion(snapshot.excludedApps),
        normalizeExclusion(snapshot.modifierExcludedApps),
      ));
      for (const { field } of exclusionFields) {
        if (drafts[field] === snapshot[field]) {
          drafts[field] = models.general[field];
        }
      }
      setStatus('已应用排除列表。');
    });
  } catch (error) {
    setStatus(error.message || '排除列表未应用。', true);
  } finally {
    applying.value = false;
  }
}

async function beginPick(event, field) {
  if (api.browser || !ready.value || pickSession || event.button !== 0 || event.pointerType !== 'mouse') {
    return;
  }
  event.preventDefault();
  const session = {};
  pickSession = session;
  picking.value = field;
  setStatus('拖到目标窗口后松开。按 Esc 取消。');
  try {
    const filename = (await api.pickApplication())?.trim();
    if (pickSession !== session) return;
    if (!filename) {
      setStatus('未选择窗口。');
      return;
    }
    const entries = normalizeExclusion(drafts[field]).split('\n');
    if (entries.some(entry => entry.toLowerCase() === filename.toLowerCase())) {
      setStatus('列表中已有 ' + filename + '。');
      return;
    }
    const before = drafts[field];
    const separator = before && !before.endsWith('\n') ? '\n' : '';
    drafts[field] = before + separator + filename;
    setStatus('已添加 ' + filename + '，应用列表后生效。');
  } catch (error) {
    if (pickSession === session) {
      setStatus(error.message || '无法拾取窗口。', true);
    }
  } finally {
    if (pickSession === session) {
      pickSession = undefined;
      picking.value = null;
    }
  }
}

async function cancelPick() {
  if (!pickSession) return;
  pickSession = undefined;
  picking.value = null;
  try {
    await api.cancelPick();
    if (!pickSession) setStatus('已取消拖选。');
  } catch (error) {
    setStatus(error.message || '无法取消拖选。', true);
  }
}

function handleEscape(event) {
  if (event.key === 'Escape' && pickSession) {
    event.preventDefault();
    void cancelPick();
  }
}

async function previewNative() {
  await tail;
  try {
    await api.preview();
    setStatus('已启动桌面预览。');
  } catch (error) {
    setStatus(error.message || '无法启动桌面预览。', true);
  }
}

function toggleCursorWrap() {
  return enqueue(async () => {
    accept('cursorWrap', await api.toggleCursorWrap());
  });
}

onMounted(() => {
  window.addEventListener('keydown', handleEscape);
  void load();
});

onBeforeUnmount(() => {
  window.removeEventListener('keydown', handleEscape);
  if (pickSession) void cancelPick();
});
</script>

<template>
  <div class="settings-shell">
    <aside class="settings-sidebar">
      <p class="settings-brand"><MousePointer2 class="size-4" aria-hidden="true" /> 鼠标增强</p>
      <div class="settings-tabs" role="tablist" aria-label="设置分类" @keydown="navigateByKey">
        <button
          v-for="section in sections"
          :id="'tab-' + section.id"
          :key="section.id"
          type="button"
          role="tab"
          :aria-selected="active === section.id"
          :aria-controls="'panel-' + section.id"
          :tabindex="active === section.id ? 0 : -1"
          @click="activate(section.id)"
        >
          <component :is="section.icon" class="size-4 shrink-0" aria-hidden="true" />
          {{ section.label }}
        </button>
      </div>
    </aside>

    <main class="settings-content">
      <section
        :id="'panel-' + active"
        role="tabpanel"
        :aria-labelledby="'tab-' + active"
        :aria-busy="loading"
        class="settings-page"
      >
        <template v-if="active === 'drag' || active === 'volume'">
          <header class="settings-heading">
            <h1>{{ active === 'drag' ? '窗口拖动' : '滚轮音量' }}</h1>
            <p v-if="active === 'drag'">按住修饰键，在窗口任意位置拖动或调整大小。</p>
            <p v-else>按住修饰键滚动滚轮，即可调整系统音量。</p>
          </header>

          <div class="settings-panel">
            <SettingsToggle
              v-if="active === 'drag'"
              id="windowDragEnabled"
              label="启用窗口拖动"
              description="按住修饰键并用鼠标左键拖动窗口。"
              :model-value="general.windowDragEnabled"
              :disabled="!ready"
              @update:model-value="save('general', 'windowDragEnabled', $event)"
            />
            <SettingsToggle
              v-else
              id="volumeScrollEnabled"
              label="启用滚轮调音量"
              description="按住修饰键时，滚轮调整音量。"
              :model-value="general.volumeScrollEnabled"
              :disabled="!ready"
              @update:model-value="save('general', 'volumeScrollEnabled', $event)"
            />

            <fieldset class="setting-row setting-modifier" :disabled="!ready">
              <legend class="sr-only">修饰键</legend>
              <div>
                <p class="control-label">修饰键</p>
                <p class="control-hint">窗口拖动与滚轮音量共用。</p>
              </div>
              <div class="modifier-options">
                <label v-for="choice in ['alt', 'win']" :key="choice">
                  <input
                    type="radio"
                    :name="active + '-modifier'"
                    :value="choice"
                    :checked="general.activationModifier === choice"
                    @change="save('general', 'activationModifier', choice)"
                  >
                  {{ choice === 'alt' ? 'Alt' : 'Win' }}
                </label>
              </div>
            </fieldset>

            <template v-if="active === 'drag'">
              <SettingsToggle
                id="rightClickResizeEnabled"
                label="右键调整大小"
                description="按住修饰键并用右键拖动窗口边缘。"
                :model-value="general.rightClickResizeEnabled"
                :disabled="!ready"
                @update:model-value="save('general', 'rightClickResizeEnabled', $event)"
              />
              <SettingsToggle
                id="preventAltWindowMenu"
                label="抑制 Alt 窗口菜单"
                :model-value="general.preventAltWindowMenu"
                :disabled="!ready"
                @update:model-value="save('general', 'preventAltWindowMenu', $event)"
              />
            </template>
          </div>

          <section v-if="active === 'drag'" class="exclusion-section" aria-labelledby="exclusion-title">
            <h2 id="exclusion-title">排除列表</h2>
            <div class="exclusion-grid">
              <div v-for="{ field, label, description } in exclusionFields" :key="field" class="settings-panel exclusion-editor">
                <label class="control-label" :for="field">{{ label }}</label>
                <p :id="field + '-hint'" class="control-hint">{{ description }}</p>
                <textarea
                  :id="field"
                  v-model="drafts[field]"
                  :disabled="!ready"
                  :aria-describedby="field + '-hint'"
                  rows="3"
                  spellcheck="false"
                  placeholder="每行一个进程名或窗口标题关键字"
                />
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  :disabled="!ready || api.browser || picking !== null || applying"
                  :aria-label="'拖选窗口添加到' + label"
                  @pointerdown="beginPick($event, field)"
                >
                  <Crosshair class="size-4" aria-hidden="true" />
                  {{ picking === field ? '正在拖选…' : '按住拖选窗口' }}
                </Button>
              </div>
            </div>
            <div class="exclusion-actions">
              <Button
                type="button"
                :disabled="!ready || !hasDrafts || applying || picking !== null"
                @click="applyExclusions"
              >{{ applying ? '正在应用…' : '应用排除列表' }}</Button>
              <span v-if="hasDrafts && !applying" class="control-hint">有未应用的更改</span>
            </div>
          </section>
        </template>

        <template v-else-if="active === 'desktop'">
          <header class="settings-heading">
            <h1>桌面与任务栏</h1>
            <p>设置桌面和任务栏上的双击操作。</p>
          </header>
          <div class="settings-panel">
            <SettingsToggle
              id="desktopIconsToggleEnabled"
              label="双击桌面切换图标"
              description="双击桌面空白处，隐藏或显示桌面图标。"
              :model-value="general.desktopIconsToggleEnabled"
              :disabled="!ready"
              @update:model-value="save('general', 'desktopIconsToggleEnabled', $event)"
            />
            <SettingsToggle
              id="taskbarShowDesktopEnabled"
              label="双击任务栏显示桌面"
              description="双击任务栏空白处，显示或还原桌面。"
              :model-value="general.taskbarShowDesktopEnabled"
              :disabled="!ready"
              @update:model-value="save('general', 'taskbarShowDesktopEnabled', $event)"
            />
          </div>
        </template>

        <FindMousePanel
          v-else-if="active === 'find-mouse'"
          :settings="models.findMouse"
          :ready="ready"
          :browser="api.browser"
          @save="(field, value) => save('findMouse', field, value)"
          @preview="previewNative"
        />

        <CursorWrapPanel
          v-else
          :settings="models.cursorWrap"
          :ready="ready"
          :toggle-active="toggleCursorWrap"
          @save="(field, value) => save('cursorWrap', field, value)"
          @error="message => setStatus(message, true)"
        />
      </section>

      <footer class="settings-status">
        <p v-if="api.browser">浏览器预览 · 设置仅在当前页面生效</p>
        <p role="status" :class="{ 'settings-error': isError }">
          {{ saving && !isError ? '正在保存…' : status }}
        </p>
        <Button v-if="!ready && !loading" variant="outline" size="sm" @click="load">重新读取</Button>
      </footer>
    </main>
  </div>
</template>
