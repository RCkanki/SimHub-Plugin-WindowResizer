using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using GameReaderCommon;
using SimHub.Plugins;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace WindowResizer
{
    [PluginDescription("Window layout controller for SimHub")]
    [PluginAuthor("RCkanki")]
    [PluginName("Window Resizer")]
    public class WindowResizerPlugin : IPlugin, IDataPlugin, IWPFSettingsV2, IDisposable
    {
        public PluginManager PluginManager { get; set; }

        public string LeftMenuTitle => "Window Resizer";

        public ImageSource PictureIcon => PictureIconLazy.Value;

        private static readonly Lazy<ImageSource> PictureIconLazy = new Lazy<ImageSource>(LoadPluginIcon);

        private static ImageSource LoadPluginIcon()
        {
            try
            {
                var uri = new Uri("pack://application:,,,/WindowResizer;component/icon.png", UriKind.Absolute);
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = uri;
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch
            {
                return null;
            }
        }

        private ProfileManager _profiles;
        private int _cycleIndex;
        private SettingsControl _settingsControl;
        private readonly Dictionary<Guid, DateTime> _autoDetectTime = new Dictionary<Guid, DateTime>();
        private readonly HashSet<Guid> _autoApplied = new HashSet<Guid>();
        /// <summary>Interval for re-checking whether the applied profile's window still exists (DataUpdate is too frequent to enumerate every tick).</summary>
        private readonly Dictionary<Guid, DateTime> _lastAppliedVerifyUtc = new Dictionary<Guid, DateTime>();
        private readonly Dictionary<Guid, IntPtr> _autoAppliedHwnd = new Dictionary<Guid, IntPtr>();
        private DateTime _lastAutoSearchPollUtc = DateTime.MinValue;
        private const int AutoSearchPollIntervalMs = 250;
        private const int AppliedWindowVerifyIntervalMs = 2000;

        /// <summary>Win32 foreground APIs work more reliably from the thread that received input; controller actions run off the UI thread, so marshal to UI.</summary>
        private Dispatcher _uiDispatcher;
        private SynchronizationContext _uiSyncContext;
        private int _initManagedThreadId;

        public void Init(PluginManager pluginManager)
        {
            PluginManager = pluginManager;

            _initManagedThreadId = Thread.CurrentThread.ManagedThreadId;
            _uiSyncContext = SynchronizationContext.Current;
            TryCaptureUiDispatcher();

            // Create WindowResizer directory under the SimHub base folder.
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var storageDir = Path.Combine(baseDir, "WindowResizer");
            _profiles = new ProfileManager(storageDir);
            _cycleIndex = 0;

            // Register via PluginManager + GetType() (this.AddAction may not reach Controls in some setups).
            // Pass an empty release callback for During actions. Win32 foreground work runs on the UI thread.
            pluginManager.AddAction(
                "WindowResizer.NextProfile",
                GetType(),
                (pm, s) => RunOnUiThread(NextProfile),
                (pm, s) => { });

            pluginManager.AddAction(
                "WindowResizer.PrevProfile",
                GetType(),
                (pm, s) => RunOnUiThread(PrevProfile),
                (pm, s) => { });

            pluginManager.AddAction(
                "WindowResizer.ApplyProfileByName",
                GetType(),
                (pm, profileName) => RunOnUiThread(() => ApplyProfileByName(profileName)),
                (pm, s) => { });

            pluginManager.AddAction(
                "WindowResizer.TestMoveActiveWindow",
                GetType(),
                (pm, s) => RunOnUiThread(() => WindowManager.MoveActiveWindowTo(100, 100, 1280, 720)),
                (pm, s) => { });
        }

        private void TryCaptureUiDispatcher()
        {
            if (_uiDispatcher != null)
            {
                return;
            }

            try
            {
                _uiDispatcher = Application.Current?.Dispatcher;
            }
            catch
            {
                _uiDispatcher = null;
            }

            if (_uiDispatcher == null)
            {
                try
                {
                    _uiDispatcher = Dispatcher.CurrentDispatcher;
                }
                catch
                {
                    _uiDispatcher = null;
                }
            }
        }

        private void RunOnUiThread(Action action)
        {
            if (action == null)
            {
                return;
            }

            TryCaptureUiDispatcher();

            if (_uiDispatcher != null)
            {
                if (_uiDispatcher.CheckAccess())
                {
                    action();
                }
                else
                {
                    try
                    {
                        _uiDispatcher.Invoke(action, DispatcherPriority.Send);
                    }
                    catch
                    {
                        _uiDispatcher.BeginInvoke(DispatcherPriority.Send, action);
                    }
                }

                return;
            }

            if (Thread.CurrentThread.ManagedThreadId == _initManagedThreadId)
            {
                action();
                return;
            }

            if (_uiSyncContext != null)
            {
                try
                {
                    _uiSyncContext.Send(_ => action(), null);
                }
                catch
                {
                    _uiSyncContext.Post(_ => action(), null);
                }

                return;
            }

            action();
        }

        public void DataUpdate(PluginManager pluginManager, ref GameData data)
        {
            if (_profiles == null) return;

            var now = DateTime.UtcNow;
            var allowAutoSearchPoll = (now - _lastAutoSearchPollUtc).TotalMilliseconds >= AutoSearchPollIntervalMs;
            var consumedSearchPoll = false;

            foreach (var profile in _profiles.Profiles)
            {
                if (!profile.AutoApply || string.IsNullOrWhiteSpace(profile.ProcessName))
                    continue;

                if (_autoApplied.Contains(profile.Id))
                {
                    if (!_lastAppliedVerifyUtc.TryGetValue(profile.Id, out var lastVerify) ||
                        (now - lastVerify).TotalMilliseconds >= AppliedWindowVerifyIntervalMs)
                    {
                        _lastAppliedVerifyUtc[profile.Id] = now;

                        if (_autoAppliedHwnd.TryGetValue(profile.Id, out var cached) &&
                            WindowManager.IsWindowAlive(cached) &&
                            WindowManager.IsWindowOwnedByNamedProcess(cached, profile.ProcessName))
                        {
                            continue;
                        }

                        var found = WindowManager.FindWindowForProfile(profile);
                        if (found == IntPtr.Zero)
                        {
                            _autoDetectTime.Remove(profile.Id);
                            _autoApplied.Remove(profile.Id);
                            _lastAppliedVerifyUtc.Remove(profile.Id);
                            _autoAppliedHwnd.Remove(profile.Id);
                        }
                        else
                        {
                            _autoAppliedHwnd[profile.Id] = found;
                        }
                    }

                    continue;
                }

                if (!allowAutoSearchPoll)
                    continue;

                consumedSearchPoll = true;
                var hWnd = WindowManager.FindWindowForProfile(profile);

                if (hWnd == IntPtr.Zero)
                {
                    _autoDetectTime.Remove(profile.Id);
                    _autoApplied.Remove(profile.Id);
                    _autoAppliedHwnd.Remove(profile.Id);
                    continue;
                }

                if (!_autoDetectTime.TryGetValue(profile.Id, out var detectedAt))
                {
                    _autoDetectTime[profile.Id] = now;
                    continue;
                }

                var delayMs = profile.AutoDelayMs;
                if (delayMs < 0) delayMs = 0;

                if ((now - detectedAt).TotalMilliseconds < delayMs)
                {
                    continue;
                }

                _profiles.ApplyProfile(profile, hWnd);
                _autoApplied.Add(profile.Id);
                _autoAppliedHwnd[profile.Id] = hWnd;
            }

            if (consumedSearchPoll)
            {
                _lastAutoSearchPollUtc = now;
            }
        }

        public Control GetWPFSettingsControl(PluginManager pluginManager)
        {
            if (_settingsControl == null)
            {
                _settingsControl = new SettingsControl(_profiles);
                if (_uiDispatcher == null)
                {
                    _uiDispatcher = _settingsControl.Dispatcher;
                }
            }

            return _settingsControl;
        }

        public void End(PluginManager pluginManager)
        {
            _profiles?.FlushSave();
        }

        public void Dispose()
        {
            _profiles?.FlushSave();
        }

        public void NextProfile()
        {
            if (_profiles == null || _profiles.Profiles.Count == 0) return;

            var cycleProfiles = _profiles.Profiles.FindAll(p => p.IncludeInCycle);
            if (cycleProfiles.Count == 0) return;

            if (_cycleIndex >= cycleProfiles.Count)
            {
                _cycleIndex = 0;
            }

            _cycleIndex = (_cycleIndex + 1) % cycleProfiles.Count;
            _profiles.ApplyProfile(cycleProfiles[_cycleIndex]);
        }

        public void PrevProfile()
        {
            if (_profiles == null || _profiles.Profiles.Count == 0) return;

            var cycleProfiles = _profiles.Profiles.FindAll(p => p.IncludeInCycle);
            if (cycleProfiles.Count == 0) return;

            if (_cycleIndex >= cycleProfiles.Count)
            {
                _cycleIndex = 0;
            }

            _cycleIndex = (_cycleIndex - 1 + cycleProfiles.Count) % cycleProfiles.Count;
            _profiles.ApplyProfile(cycleProfiles[_cycleIndex]);
        }

        public void ApplyProfileByName(string name)
        {
            if (_profiles == null || string.IsNullOrWhiteSpace(name)) return;

            var profile = _profiles.Profiles.Find(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
            if (profile == null) return;

            _profiles.ApplyProfile(profile);
        }
    }
}

