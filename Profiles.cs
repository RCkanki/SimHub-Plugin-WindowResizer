using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Newtonsoft.Json;

namespace WindowResizer
{
    public class Profile
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = "New Profile";
        public string ProcessName { get; set; } = string.Empty;
        public string WindowTitleContains { get; set; }
        // Unused: native window class filter not used (any key in profiles.json is ignored on load).
        // public string WindowClassContains { get; set; }
        public int X { get; set; } = 0;
        public int Y { get; set; } = 0;
        public int Width { get; set; } = 1280;
        public int Height { get; set; } = 720;
        public bool RemoveBorders { get; set; }
        public bool BringToFront { get; set; }
        public bool BringToFrontTakeFocus { get; set; }
        public bool IncludeInCycle { get; set; }
        public bool AutoApply { get; set; }
        public int AutoDelayMs { get; set; } = 0;
    }

    public class ProfileManager
    {
        private readonly string _storagePath;
        private readonly string _storageDirectory;
        private readonly object _saveSync = new object();
        private Timer _debouncedSaveTimer;
        private const int SaveDebounceMilliseconds = 500;

        // Exposes the profile storage directory for other components.
        public static string StorageDirectory { get; private set; }

        public List<Profile> Profiles { get; private set; } = new List<Profile>();

        /// <summary>Incremented on save/touch; used to invalidate NEXT/PREV caches, etc.</summary>
        public int ProfilesRevision { get; private set; }

        public ProfileManager(string storageDirectory)
        {
            _storageDirectory = storageDirectory;
            StorageDirectory = storageDirectory;
            _storagePath = Path.Combine(storageDirectory, "profiles.json");
            Directory.CreateDirectory(storageDirectory);
            Load();
        }

        public void Load()
        {
            if (!File.Exists(_storagePath))
            {
                Profiles = new List<Profile>();
                FlushSave();
                return;
            }

            var json = File.ReadAllText(_storagePath);
            Profiles = JsonConvert.DeserializeObject<List<Profile>>(json) ?? new List<Profile>();

            // Back-compat: normalize legacy "ProcessName.exe" to "ProcessName".
            var changed = false;
            foreach (var p in Profiles)
            {
                if (!string.IsNullOrEmpty(p.ProcessName) &&
                    p.ProcessName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    p.ProcessName = p.ProcessName.Substring(0, p.ProcessName.Length - 4);
                    changed = true;
                }
            }

            if (changed)
            {
                FlushSave();
            }
        }

        /// <summary>Debounced disk write (coalesces rapid Save calls into one flush).</summary>
        public void Save()
        {
            TouchProfilesRevision();
            lock (_saveSync)
            {
                if (_debouncedSaveTimer == null)
                {
                    _debouncedSaveTimer = new Timer(OnDebouncedSaveFire, null, SaveDebounceMilliseconds, Timeout.Infinite);
                }
                else
                {
                    _debouncedSaveTimer.Change(SaveDebounceMilliseconds, Timeout.Infinite);
                }
            }
        }

        /// <summary>Flush to disk immediately (shutdown, first-run create, normalization write-back).</summary>
        public void FlushSave()
        {
            lock (_saveSync)
            {
                _debouncedSaveTimer?.Dispose();
                _debouncedSaveTimer = null;
                WriteProfilesCore();
            }
        }

        /// <summary>Bump revision without saving (e.g. form apply; disk save happens later).</summary>
        public void TouchProfilesRevision()
        {
            ProfilesRevision++;
        }

        private void OnDebouncedSaveFire(object state)
        {
            lock (_saveSync)
            {
                _debouncedSaveTimer?.Dispose();
                _debouncedSaveTimer = null;
                WriteProfilesCore();
            }
        }

        private void WriteProfilesCore()
        {
            var json = JsonConvert.SerializeObject(Profiles, Formatting.Indented);
            File.WriteAllText(_storagePath, json);
        }

        /// <param name="knownHwnd">HWND from a recent <see cref="WindowManager.FindWindowForProfile"/>; if zero, resolves again internally.</param>
        public void ApplyProfile(Profile profile, IntPtr knownHwnd = default)
        {
            var hWnd = knownHwnd != IntPtr.Zero && WindowManager.IsWindowAlive(knownHwnd)
                ? knownHwnd
                : WindowManager.FindWindowForProfile(profile);

            if (hWnd == IntPtr.Zero)
            {
                return;
            }

            WindowManager.SetBorderless(hWnd, profile.RemoveBorders);

            var willBring = profile.BringToFront || profile.BringToFrontTakeFocus;
            WindowManager.ResizeAndMoveWindow(
                hWnd,
                profile.X,
                profile.Y,
                profile.Width,
                profile.Height);

            if (willBring)
            {
                WindowManager.BringToFront(hWnd, profile.BringToFrontTakeFocus);
            }
        }
    }
}
