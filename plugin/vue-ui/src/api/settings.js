import { getBridgeService, isBridgeAvailable } from '@/composables/useDotnetBridge';

export const generalDefaults = {
  windowDragEnabled: true,
  volumeScrollEnabled: true,
  desktopIconsToggleEnabled: false,
  taskbarShowDesktopEnabled: false,
  activationModifier: 'alt',
  rightClickResizeEnabled: true,
  preventAltWindowMenu: true,
  excludedApps: '',
  modifierExcludedApps: '',
};

export const findMouseDefaults = {
  enabled: false,
  doubleCtrlEnabled: true,
  shakeEnabled: false,
  spotlightColor: '#8b5cf6',
  spotlightDiameter: 200,
  maskOpacity: 74,
  glowOpacity: 19,
  animationDurationMs: 400,
  holdDurationMs: 800,
  shakeThreshold: 7,
};

export const cursorWrapDefaults = {
  enabled: false,
  autoActivate: false,
  disableWhileDragging: true,
  mode: 'both',
  activation: 'always',
  disableForSingleMonitor: false,
  isActive: false,
};

export function createSettingsApi() {
  if (!isBridgeAvailable()) {
    return createBrowserApi();
  }

  const general = useHostSettings('MouseEnhanceSettings');
  const findMouse = useHostSettings('FindMouseSettings');
  const cursorWrap = useHostSettings('CursorWrapSettings');

  return {
    browser: false,
    general,
    findMouse,
    cursorWrap,
    toggleCursorWrap: () => cursorWrap.call('ToggleActive'),
    applyExclusions: (excludedApps, modifierExcludedApps) => general.call(
      'ApplyExclusions', excludedApps, modifierExcludedApps,
    ),
    pickApplication: () => general.call('PickApplication'),
    cancelPick: () => general.call('CancelPick'),
    preview: () => findMouse.call('Preview'),
  };
}

function useHostSettings(serviceName) {
  let connecting;
  async function call(method, ...args) {
    connecting ??= getBridgeService(serviceName).catch((error) => {
      connecting = undefined;
      throw error;
    });
    const service = await connecting;
    const invoke = service?.[method];
    if (typeof invoke !== 'function') {
      throw new Error('宿主设置服务不可用。');
    }
    return invoke(...args);
  }

  return {
    call,
    load: () => call('Load'),
    save: (field, value) => call('Save', field, JSON.stringify(value)),
  };
}

function createBrowserApi() {
  const general = useBrowserSettings(generalDefaults);
  const cursorWrap = useBrowserSettings(cursorWrapDefaults);
  return {
    browser: true,
    general,
    findMouse: useBrowserSettings(findMouseDefaults),
    cursorWrap: {
      load: cursorWrap.load,
      save: (field, value) => cursorWrap.update(field === 'enabled'
        ? { enabled: value, isActive: value }
        : { [field]: value }),
    },
    toggleCursorWrap: async () => {
      const { enabled, isActive } = await cursorWrap.load();
      return cursorWrap.update({ isActive: enabled && !isActive });
    },
    applyExclusions: async (excludedApps, modifierExcludedApps) => general.update({
      excludedApps, modifierExcludedApps,
    }),
  };
}

function useBrowserSettings(defaults) {
  let settings = { ...defaults };
  function update(values) {
    settings = { ...settings, ...values };
    return { ...settings };
  }
  return {
    update,
    load: async () => ({ ...settings }),
    save: async (field, value) => update({ [field]: value }),
  };
}
