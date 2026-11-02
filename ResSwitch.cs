// ResSwitch — быстрое переключение разрешения экрана из трея, по горячим клавишам и по запуску игр.
// Сборка: build.bat (нужен только .NET Framework 4.x, который есть в Windows).

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ResSwitch
{
    static class Native
    {
        public const int ENUM_CURRENT_SETTINGS = -1;
        public const int DM_BITSPERPEL = 0x40000;
        public const int DM_PELSWIDTH = 0x80000;
        public const int DM_PELSHEIGHT = 0x100000;
        public const int DM_DISPLAYFREQUENCY = 0x400000;
        public const int DM_DISPLAYFIXEDOUTPUT = 0x20000000;
        public const int CDS_UPDATEREGISTRY = 0x1;
        public const int CDS_TEST = 0x2;
        public const int DISP_CHANGE_BADMODE = -2;
        public const int WM_HOTKEY = 0x0312;
        public const uint MOD_ALT = 0x1, MOD_CONTROL = 0x2, MOD_SHIFT = 0x4, MOD_WIN = 0x8, MOD_NOREPEAT = 0x4000;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
            public short dmSpecVersion, dmDriverVersion, dmSize, dmDriverExtra;
            public int dmFields;
            public int dmPositionX, dmPositionY;
            public int dmDisplayOrientation, dmDisplayFixedOutput;
            public short dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel, dmPelsWidth, dmPelsHeight, dmDisplayFlags, dmDisplayFrequency;
            public int dmICMMethod, dmICMIntent, dmMediaType, dmDitherType, dmReserved1, dmReserved2, dmPanningWidth, dmPanningHeight;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct DISPLAY_DEVICE
        {
            public int cb;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceString;
            public int StateFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceID;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceKey;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern bool EnumDisplaySettings(string deviceName, int modeNum, ref DEVMODE devMode);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern bool EnumDisplayDevices(string device, uint devNum, ref DISPLAY_DEVICE displayDevice, uint flags);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int ChangeDisplaySettingsEx(string deviceName, ref DEVMODE devMode, IntPtr hwnd, int flags, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint vk);

        [DllImport("user32.dll")]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        public static extern bool SetProcessDPIAware();

        [DllImport("user32.dll")]
        public static extern bool DestroyIcon(IntPtr handle);

        // QueryDisplayConfig — постоянный путь монитора и его название из EDID
        public const uint QDC_ONLY_ACTIVE_PATHS = 2;
        public const int DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME = 1, DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME = 2;

        [StructLayout(LayoutKind.Sequential)]
        public struct LUID { public uint LowPart; public int HighPart; }

        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_PATH_SOURCE_INFO { public LUID adapterId; public uint id, modeInfoIdx, statusFlags; }

        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_PATH_TARGET_INFO
        {
            public LUID adapterId;
            public uint id, modeInfoIdx, outputTechnology, rotation, scaling, refreshNum, refreshDen, scanLineOrdering;
            public int targetAvailable;
            public uint statusFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_PATH_INFO { public DISPLAYCONFIG_PATH_SOURCE_INFO sourceInfo; public DISPLAYCONFIG_PATH_TARGET_INFO targetInfo; public uint flags; }

        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_MODE_INFO { public uint infoType, id; public LUID adapterId; public ulong d0, d1, d2, d3, d4, d5; }

        [StructLayout(LayoutKind.Sequential)]
        public struct DISPLAYCONFIG_DEVICE_INFO_HEADER { public int type, size; public LUID adapterId; public uint id; }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct DISPLAYCONFIG_TARGET_DEVICE_NAME
        {
            public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
            public uint flags, outputTechnology;
            public ushort edidManufactureId, edidProductCodeId;
            public uint connectorInstance;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string monitorFriendlyDeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string monitorDevicePath;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct DISPLAYCONFIG_SOURCE_DEVICE_NAME
        {
            public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string viewGdiDeviceName;
        }

        [DllImport("user32.dll")]
        public static extern int GetDisplayConfigBufferSizes(uint flags, out uint numPaths, out uint numModes);

        [DllImport("user32.dll")]
        public static extern int QueryDisplayConfig(uint flags, ref uint numPaths, [Out] DISPLAYCONFIG_PATH_INFO[] paths, ref uint numModes, [Out] DISPLAYCONFIG_MODE_INFO[] modes, IntPtr topology);

        [DllImport("user32.dll")]
        public static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_TARGET_DEVICE_NAME info);

        [DllImport("user32.dll")]
        public static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_SOURCE_DEVICE_NAME info);

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        [DllImport("kernel32.dll")]
        public static extern bool AttachConsole(int pid);

        [DllImport("kernel32.dll")]
        public static extern IntPtr GetStdHandle(int n);
    }

    class DisplayInfo
    {
        public string Device;   // \\.\DISPLAY1
        public string Adapter;  // NVIDIA GeForce RTX 4070 Ti
        public string Monitor;
        public bool Primary;
        public string Name;     // из EDID: VG259QM
        public string Id;       // постоянный: AUS258D#UID4352 (модель#выход видеокарты), не зависит от номера DISPLAYn
        public string Short { get { return Device.Replace(@"\\.\", ""); } }
        public string Model { get { return Id == null ? null : Id.Split('#')[0]; } }
        public string Title { get { return string.IsNullOrEmpty(Name) ? Short : Name; } }
    }

    class Mode
    {
        public int W, H, Hz;
        public override string ToString() { return W + "x" + H + "@" + Hz; }
    }

    class Profile
    {
        public string Name;
        public string Display;
        public int W, H, Hz;
        public int Scaling = -1;
        public string Hotkey;
        public List<string> Games = new List<string>();
    }

    static class Display
    {
        public static Native.DEVMODE NewDevMode()
        {
            var dm = new Native.DEVMODE();
            dm.dmDeviceName = "";
            dm.dmFormName = "";
            dm.dmSize = (short)Marshal.SizeOf(typeof(Native.DEVMODE));
            return dm;
        }

        static List<DisplayInfo> cache;
        static int cacheTime;

        // Список вызывается при каждой перерисовке панели — кэшируем на секунду.
        public static List<DisplayInfo> List()
        {
            if (cache == null || unchecked(Environment.TickCount - cacheTime) > 1000)
            {
                cache = Enumerate();
                cacheTime = Environment.TickCount;
            }
            return cache;
        }

        public static DisplayInfo Find(string dev)
        {
            return List().FirstOrDefault(d => d.Device.Equals(dev, StringComparison.OrdinalIgnoreCase));
        }

        public static string Title(string dev)
        {
            var d = Find(dev);
            return d != null ? d.Title : dev.Replace(@"\\.\", "");
        }

        // \\.\DISPLAY1 -> (название, ID монитора) через QueryDisplayConfig
        static Dictionary<string, KeyValuePair<string, string>> Targets()
        {
            var map = new Dictionary<string, KeyValuePair<string, string>>(StringComparer.OrdinalIgnoreCase);
            try
            {
                uint np, nm;
                if (Native.GetDisplayConfigBufferSizes(Native.QDC_ONLY_ACTIVE_PATHS, out np, out nm) != 0) return map;
                var paths = new Native.DISPLAYCONFIG_PATH_INFO[np];
                var modes = new Native.DISPLAYCONFIG_MODE_INFO[nm];
                if (Native.QueryDisplayConfig(Native.QDC_ONLY_ACTIVE_PATHS, ref np, paths, ref nm, modes, IntPtr.Zero) != 0) return map;
                for (int i = 0; i < np; i++)
                {
                    var src = new Native.DISPLAYCONFIG_SOURCE_DEVICE_NAME();
                    src.header.type = Native.DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME;
                    src.header.size = Marshal.SizeOf(src);
                    src.header.adapterId = paths[i].sourceInfo.adapterId;
                    src.header.id = paths[i].sourceInfo.id;
                    if (Native.DisplayConfigGetDeviceInfo(ref src) != 0) continue;

                    var tgt = new Native.DISPLAYCONFIG_TARGET_DEVICE_NAME();
                    tgt.header.type = Native.DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME;
                    tgt.header.size = Marshal.SizeOf(tgt);
                    tgt.header.adapterId = paths[i].targetInfo.adapterId;
                    tgt.header.id = paths[i].targetInfo.id;
                    if (Native.DisplayConfigGetDeviceInfo(ref tgt) != 0) continue;

                    string id = null;
                    if ((tgt.flags & 4) != 0) // edidIdsValid
                        id = PnpId(tgt.edidManufactureId, tgt.edidProductCodeId) + "#UID" + paths[i].targetInfo.id;
                    if (!map.ContainsKey(src.viewGdiDeviceName))
                        map[src.viewGdiDeviceName] = new KeyValuePair<string, string>(tgt.monitorFriendlyDeviceName, id);
                }
            }
            catch { } // старые системы без QueryDisplayConfig
            return map;
        }

        // Код модели как в диспетчере устройств: производитель из EDID (3 буквы по 5 бит, big-endian) + код продукта — AUS258D
        static string PnpId(ushort mfg, ushort product)
        {
            int v = ((mfg & 0xFF) << 8) | (mfg >> 8);
            var sb = new StringBuilder();
            foreach (int shift in new[] { 10, 5, 0 }) sb.Append((char)('A' - 1 + ((v >> shift) & 31)));
            return sb.ToString() + product.ToString("X4");
        }

        static List<DisplayInfo> Enumerate()
        {
            var targets = Targets();
            var list = new List<DisplayInfo>();
            for (uint i = 0; ; i++)
            {
                var dd = new Native.DISPLAY_DEVICE();
                dd.cb = Marshal.SizeOf(dd);
                if (!Native.EnumDisplayDevices(null, i, ref dd, 0)) break;
                if ((dd.StateFlags & 1) == 0) continue; // не подключён к рабочему столу
                var info = new DisplayInfo { Device = dd.DeviceName, Adapter = dd.DeviceString, Primary = (dd.StateFlags & 4) != 0 };
                var mon = new Native.DISPLAY_DEVICE();
                mon.cb = Marshal.SizeOf(mon);
                if (Native.EnumDisplayDevices(dd.DeviceName, 0, ref mon, 0)) info.Monitor = mon.DeviceString;
                KeyValuePair<string, string> t;
                if (targets.TryGetValue(dd.DeviceName, out t))
                {
                    info.Name = string.IsNullOrEmpty(t.Key) ? null : t.Key;
                    info.Id = t.Value;
                }
                list.Add(info);
            }
            // одинаковые модели различаем номером, чтобы названия в меню не совпадали
            foreach (var grp in list.Where(d => !string.IsNullOrEmpty(d.Name)).GroupBy(d => d.Name).Where(gr => gr.Count() > 1))
                foreach (var d in grp) d.Name += " (" + d.Short + ")";
            return list;
        }

        // Display в настройках -> "\\.\DISPLAYn":
        //   "" / primary — основной; AUS258D#UID4352 — ID монитора (если разъём сменили — подойдёт и просто модель AUS258D);
        //   VG259QM — название монитора; 2 / DISPLAY2 — номер (может меняться после перезагрузки).
        public static string Resolve(string spec)
        {
            var list = List();
            if (string.IsNullOrWhiteSpace(spec) || spec.Trim().Equals("primary", StringComparison.OrdinalIgnoreCase))
            {
                var p = list.FirstOrDefault(d => d.Primary);
                return p != null ? p.Device : null;
            }
            spec = spec.Trim();
            int n;
            if (int.TryParse(spec, out n)) return @"\\.\DISPLAY" + n;
            if (spec.StartsWith(@"\\.\")) return spec;

            var byId = list.FirstOrDefault(d => d.Id != null && d.Id.Equals(spec, StringComparison.OrdinalIgnoreCase));
            if (byId != null) return byId.Device;
            var model = spec.Split('#')[0];
            var byModel = list.Where(d => d.Model != null && d.Model.Equals(model, StringComparison.OrdinalIgnoreCase)).ToList();
            if (byModel.Count == 1) return byModel[0].Device;
            var byName = list.Where(d => d.Name != null && d.Name.Equals(spec, StringComparison.OrdinalIgnoreCase)).ToList();
            if (byName.Count == 1) return byName[0].Device;

            if (spec.StartsWith("DISPLAY", StringComparison.OrdinalIgnoreCase)) return @"\\.\" + spec.ToUpperInvariant();
            return null; // такого монитора сейчас нет
        }

        // Что записать в Display= для этого монитора: ID, если его удалось получить, иначе номер.
        public static string Spec(string dev)
        {
            var d = Find(dev);
            if (d != null && d.Id != null) return d.Id;
            return dev.Replace(@"\\.\DISPLAY", "");
        }

        public static List<Mode> Modes(string dev)
        {
            var seen = new HashSet<string>();
            var list = new List<Mode>();
            var dm = NewDevMode();
            for (int i = 0; Native.EnumDisplaySettings(dev, i, ref dm); i++)
            {
                if (dm.dmBitsPerPel < 32) continue;
                var m = new Mode { W = dm.dmPelsWidth, H = dm.dmPelsHeight, Hz = dm.dmDisplayFrequency };
                if (seen.Add(m.ToString())) list.Add(m);
            }
            return list.OrderByDescending(m => m.W).ThenByDescending(m => m.H).ThenByDescending(m => m.Hz).ToList();
        }

        public static bool Current(string dev, out Native.DEVMODE dm)
        {
            dm = NewDevMode();
            return Native.EnumDisplaySettings(dev, Native.ENUM_CURRENT_SETTINGS, ref dm);
        }

        // hz <= 0 — максимальная частота для этого разрешения; scaling < 0 — не трогать масштабирование.
        public static int Change(string dev, int w, int h, int hz, int scaling, bool persist, bool testOnly)
        {
            var cands = Modes(dev).Where(m => m.W == w && m.H == h).ToList();
            if (cands.Count == 0) return Native.DISP_CHANGE_BADMODE;
            int useHz = hz <= 0 ? cands.Max(m => m.Hz) : cands.OrderBy(m => Math.Abs(m.Hz - hz)).First().Hz;

            Native.DEVMODE dm;
            if (!Current(dev, out dm)) return -1;
            dm.dmPelsWidth = w;
            dm.dmPelsHeight = h;
            dm.dmDisplayFrequency = useHz;
            dm.dmBitsPerPel = 32;
            dm.dmFields = Native.DM_PELSWIDTH | Native.DM_PELSHEIGHT | Native.DM_DISPLAYFREQUENCY | Native.DM_BITSPERPEL;
            if (scaling >= 0)
            {
                dm.dmDisplayFixedOutput = scaling;
                dm.dmFields |= Native.DM_DISPLAYFIXEDOUTPUT;
            }
            int r = Native.ChangeDisplaySettingsEx(dev, ref dm, IntPtr.Zero, Native.CDS_TEST, IntPtr.Zero);
            if (r != 0 || testOnly) return r;
            return Native.ChangeDisplaySettingsEx(dev, ref dm, IntPtr.Zero, persist ? Native.CDS_UPDATEREGISTRY : 0, IntPtr.Zero);
        }

        public static int Restore(string dev, Native.DEVMODE dm, bool persist)
        {
            dm.dmFields = Native.DM_PELSWIDTH | Native.DM_PELSHEIGHT | Native.DM_DISPLAYFREQUENCY | Native.DM_BITSPERPEL | Native.DM_DISPLAYFIXEDOUTPUT;
            return Native.ChangeDisplaySettingsEx(dev, ref dm, IntPtr.Zero, persist ? Native.CDS_UPDATEREGISTRY : 0, IntPtr.Zero);
        }

        // Область монитора на рабочем столе — по текущему режиму (Screen кэширует старые размеры).
        public static Rectangle Bounds(string dev)
        {
            Native.DEVMODE dm;
            if (dev == null || !Current(dev, out dm)) return Screen.PrimaryScreen.Bounds;
            return new Rectangle(dm.dmPositionX, dm.dmPositionY, dm.dmPelsWidth, dm.dmPelsHeight);
        }

        public static string Describe(Native.DEVMODE dm)
        {
            return dm.dmPelsWidth + "×" + dm.dmPelsHeight + " @ " + dm.dmDisplayFrequency + " Гц";
        }

        public static string ErrorText(int code)
        {
            switch (code)
            {
                case 0: return "успешно";
                case 1: return "нужна перезагрузка";
                case -1: return "драйвер не смог применить режим";
                case -2: return "монитор не поддерживает этот режим";
                case -3: return "не удалось записать в реестр";
                case -5: return "неверные параметры (проверьте Display)";
                default: return "ошибка " + code;
            }
        }

        public static bool ParseMode(string s, out int w, out int h, out int hz)
        {
            w = h = hz = 0;
            if (string.IsNullOrWhiteSpace(s)) return false;
            s = s.Trim().ToLowerInvariant().Replace('х', 'x').Replace('×', 'x').Replace(" ", "");
            var at = s.Split('@');
            var wh = at[0].Split('x');
            if (wh.Length != 2 || !int.TryParse(wh[0], out w) || !int.TryParse(wh[1], out h)) return false;
            if (at.Length > 1 && !int.TryParse(at[1].Replace("hz", "").Replace("гц", ""), out hz)) return false;
            return true;
        }

        public static int ParseScaling(string s)
        {
            switch ((s ?? "").Trim().ToLowerInvariant())
            {
                case "default": case "0": case "aspect": return 0;
                case "stretch": case "1": return 1;
                case "center": case "centre": case "2": return 2;
                default: return -1;
            }
        }

        public static readonly string[] ScalingKeys = { "default", "stretch", "center" };
        public static readonly string[] ScalingNames ={ "По умолчанию (дисплей/драйвер)", "Растянуть на весь экран", "По центру (без масштабирования)" };
    }

    class Config
    {
        public int ConfirmSeconds = 10;
        public bool GameWatch = true;
        public int PollMs = 2000;
        public List<Profile> Profiles = new List<Profile>();
        public List<Profile> Favorites = new List<Profile>();
        public List<string> Errors = new List<string>();

        public static string FilePath
        {
            get { return Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "ResSwitch.ini"); }
        }

        public static Config Load()
        {
            if (!File.Exists(FilePath)) File.WriteAllText(FilePath, DefaultText(), new UTF8Encoding(true));
            var cfg = new Config();
            string section = null;
            Profile p = null;
            int lineNo = 0;
            foreach (var raw in File.ReadAllLines(FilePath, Encoding.UTF8))
            {
                lineNo++;
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith(";") || line.StartsWith("#")) continue;
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    section = line.Substring(1, line.Length - 2).Trim().ToLowerInvariant();
                    if (section == "profile")
                    {
                        p = new Profile();
                        cfg.Profiles.Add(p);
                    }
                    continue;
                }
                int eq = line.IndexOf('=');
                if (eq < 0) { cfg.Errors.Add("строка " + lineNo + ": нет '='"); continue; }
                var key = line.Substring(0, eq).Trim().ToLowerInvariant();
                var val = line.Substring(eq + 1).Trim();

                if (section == "settings")
                {
                    int n;
                    if (key == "confirmseconds" && int.TryParse(val, out n)) cfg.ConfirmSeconds = n;
                    else if (key == "gamewatch") cfg.GameWatch = val.Equals("true", StringComparison.OrdinalIgnoreCase) || val == "1";
                    else if (key == "pollms" && int.TryParse(val, out n)) cfg.PollMs = Math.Max(500, n);
                }
                else if (section == "profile" && p != null)
                {
                    switch (key)
                    {
                        case "name": p.Name = val; break;
                        case "display": p.Display = val; break;
                        case "mode":
                            if (!Display.ParseMode(val, out p.W, out p.H, out p.Hz)) cfg.Errors.Add("строка " + lineNo + ": не понял режим '" + val + "'");
                            break;
                        case "scaling": p.Scaling = Display.ParseScaling(val); break;
                        case "hotkey": p.Hotkey = val; break;
                        case "game":
                            foreach (var g in val.Split(','))
                            {
                                var name = g.Trim();
                                if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) name = name.Substring(0, name.Length - 4);
                                if (name.Length > 0) p.Games.Add(name);
                            }
                            break;
                    }
                }
                else if (section == "favorites" && key == "fav")
                {
                    var f = ParseFavorite(val);
                    if (f != null) cfg.Favorites.Add(f);
                    else cfg.Errors.Add("строка " + lineNo + ": не понял избранное '" + val + "'");
                }
            }
            cfg.Profiles.RemoveAll(x => x.W == 0 || x.H == 0);
            foreach (var x in cfg.Profiles)
                if (string.IsNullOrEmpty(x.Name)) x.Name = x.W + "×" + x.H + (x.Hz > 0 ? " @ " + x.Hz : "");
            return cfg;
        }

        // Fav=1:1920x1080@144:stretch — монитор:режим[:масштабирование]
        static Profile ParseFavorite(string val)
        {
            var parts = val.Split(':');
            if (parts.Length < 2) return null;
            var p = new Profile { Display = parts[0].Trim() };
            if (!Display.ParseMode(parts[1], out p.W, out p.H, out p.Hz) || p.W == 0 || p.H == 0) return null;
            if (parts.Length > 2) p.Scaling = Display.ParseScaling(parts[2]);
            return p;
        }

        static string FormatFavorite(Profile p)
        {
            return p.Display + ":" + p.W + "x" + p.H + (p.Hz > 0 ? "@" + p.Hz : "")
                + (p.Scaling >= 0 && p.Scaling < Display.ScalingKeys.Length ? ":" + Display.ScalingKeys[p.Scaling] : "");
        }

        // Переписывает только секцию [Favorites] (в конце файла), остальное — как было, с комментариями.
        public static void SaveFavorites(List<Profile> favs)
        {
            var lines = new List<string>();
            bool skip = false;
            if (File.Exists(FilePath))
                foreach (var raw in File.ReadAllLines(FilePath, Encoding.UTF8))
                {
                    var t = raw.Trim();
                    if (t.StartsWith("[") && t.EndsWith("]"))
                        skip = t.Substring(1, t.Length - 2).Trim().Equals("favorites", StringComparison.OrdinalIgnoreCase);
                    if (!skip) lines.Add(raw);
                }
            while (lines.Count > 0 && lines[lines.Count - 1].Trim().Length == 0) lines.RemoveAt(lines.Count - 1);
            if (favs.Count > 0)
            {
                lines.Add("");
                lines.Add("[Favorites]");
                foreach (var f in favs) lines.Add("Fav=" + FormatFavorite(f));
            }
            File.WriteAllLines(FilePath, lines, new UTF8Encoding(true));
        }

        static string DefaultText()
        {
            var sb = new StringBuilder();
            sb.AppendLine("; ResSwitch — настройки. После правки: меню в трее → «Перезагрузить настройки».");
            sb.AppendLine(";");
            sb.AppendLine("; [Settings]");
            sb.AppendLine(";   ConfirmSeconds — сколько секунд ждать подтверждения после ручной смены (0 = не спрашивать).");
            sb.AppendLine(";                    Если не нажать «Оставить», вернётся прежнее разрешение.");
            sb.AppendLine(";   GameWatch      — автосмена разрешения при запуске игр (true/false)");
            sb.AppendLine(";");
            sb.AppendLine("; [Profile] — один профиль:");
            sb.AppendLine(";   Name     — название в меню");
            sb.AppendLine(";   Display  — монитор: его ID (AUS258D#UID4352 — меню трея → монитор → «Скопировать ID»), название (VG259QM),");
            sb.AppendLine(";              primary или номер 1, 2 ... (номер DISPLAYn может меняться после перезагрузки). По умолчанию primary");
            sb.AppendLine(";   Mode     — 1920x1080@144  (частоту можно не указывать — возьмётся максимальная)");
            sb.AppendLine(";   Scaling  — keep | default | stretch | center  (по умолчанию keep — не трогать)");
            sb.AppendLine(";   Hotkey   — например Ctrl+Alt+1, Ctrl+Shift+F5, Win+Alt+Num2");
            sb.AppendLine(";   Game     — exe игры, например cs2.exe (несколько — через запятую).");
            sb.AppendLine(";              При запуске игры профиль включится сам, после выхода вернётся прежнее разрешение.");
            sb.AppendLine(";");
            sb.AppendLine("; [Favorites] — избранные режимы, панель по левому клику дописывает их сама:");
            sb.AppendLine(";   Fav=1:1920x1080@144:stretch   (монитор:режим[:масштабирование])");
            sb.AppendLine();
            sb.AppendLine("[Settings]");
            sb.AppendLine("ConfirmSeconds=10");
            sb.AppendLine("GameWatch=true");
            sb.AppendLine();

            int hk = 1;
            foreach (var d in Display.List().OrderByDescending(x => x.Primary))
            {
                Native.DEVMODE cur;
                if (!Display.Current(d.Device, out cur)) continue;
                var num = Display.Spec(d.Device);
                sb.AppendLine("[Profile]");
                sb.AppendLine("Name=" + d.Title + " текущее " + cur.dmPelsWidth + "×" + cur.dmPelsHeight + " @ " + cur.dmDisplayFrequency);
                sb.AppendLine("Display=" + num);
                sb.AppendLine("Mode=" + cur.dmPelsWidth + "x" + cur.dmPelsHeight + "@" + cur.dmDisplayFrequency);
                if (cur.dmDisplayFixedOutput >= 0 && cur.dmDisplayFixedOutput < Display.ScalingKeys.Length)
                    sb.AppendLine("Scaling=" + Display.ScalingKeys[cur.dmDisplayFixedOutput]);
                if (hk <= 9) sb.AppendLine("Hotkey=Ctrl+Alt+" + hk++);
                sb.AppendLine();

                if (d.Primary)
                {
                    var modes = Display.Modes(d.Device);
                    if (cur.dmPelsWidth != 1920 && modes.Any(m => m.W == 1920 && m.H == 1080))
                    {
                        sb.AppendLine("[Profile]");
                        sb.AppendLine("Name=" + d.Title + " 1920×1080");
                        sb.AppendLine("Display=" + num);
                        sb.AppendLine("Mode=1920x1080");
                        sb.AppendLine("Hotkey=Ctrl+Alt+" + hk++);
                        sb.AppendLine();
                    }
                    sb.AppendLine("[Profile]");
                    sb.AppendLine("Name=" + d.Title + " 1280×960 растянутое");
                    sb.AppendLine("Display=" + num);
                    sb.AppendLine("Mode=1280x960");
                    sb.AppendLine("Scaling=stretch");
                    sb.AppendLine("Hotkey=Ctrl+Alt+" + hk++);
                    sb.AppendLine("; Game=cs2.exe");
                    sb.AppendLine();
                }
            }
            return sb.ToString();
        }
    }

    class HotkeyWindow : NativeWindow, IDisposable
    {
        readonly Action<int> onHotkey;
        public HotkeyWindow(Action<int> onHotkey)
        {
            this.onHotkey = onHotkey;
            CreateHandle(new CreateParams());
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_HOTKEY) onHotkey(m.WParam.ToInt32());
            base.WndProc(ref m);
        }

        public void Dispose() { DestroyHandle(); }

        public static bool Parse(string s, out uint mods, out uint vk)
        {
            mods = Native.MOD_NOREPEAT;
            vk = 0;
            foreach (var part in s.Split('+'))
            {
                var p = part.Trim().ToLowerInvariant();
                if (p == "ctrl" || p == "control") mods |= Native.MOD_CONTROL;
                else if (p == "alt") mods |= Native.MOD_ALT;
                else if (p == "shift") mods |= Native.MOD_SHIFT;
                else if (p == "win") mods |= Native.MOD_WIN;
                else if (p.Length == 1 && char.IsDigit(p[0])) vk = (uint)(Keys.D0 + (p[0] - '0'));
                else if (p.StartsWith("num") && p.Length == 4 && char.IsDigit(p[3])) vk = (uint)(Keys.NumPad0 + (p[3] - '0'));
                else
                {
                    Keys k;
                    if (!Enum.TryParse(p, true, out k)) return false;
                    vk = (uint)k;
                }
            }
            return vk != 0;
        }
    }

    static class Glyph
    {
        // Segoe Fluent Icons / Segoe MDL2 Assets
        public const string More = "\uE712", Settings = "\uE713", Monitor = "\uE7F4", Game = "\uE7FC",
            Undo = "\uE7A7", Refresh = "\uE72C", Power = "\uE7E8", Check = "\uE73E",
            ChevronDown = "\uE70D", ChevronUp = "\uE70E", Star = "\uE734", StarFill = "\uE735", Close = "\uE711";
    }

    // Цвета и шрифты в стиле Windows 11: светлая/тёмная тема и акцентный цвет берутся из системы.
    static class Theme
    {
        public static bool Dark, TaskbarDark, Win11;
        public static float Scale = 1f;
        public static Color Accent, OnAccent, Bg, Surface, Menu, Footer, Border, Fg, Fg2, Fg3;
        public static readonly Font Title, Body, BodyBold, Small, Caption, Icons, IconsSmall;

        public const TextFormatFlags LeftText = TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding
            | TextFormatFlags.PreserveGraphicsTranslateTransform; // учитывать прокрутку панели
        public const TextFormatFlags CenterText = LeftText | TextFormatFlags.HorizontalCenter;
        public const TextFormatFlags RightText = LeftText | TextFormatFlags.Right;

        const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

        static Theme()
        {
            using (var g = Graphics.FromHwnd(IntPtr.Zero)) Scale = g.DpiX / 96f;
            int build = 0;
            try
            {
                using (var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                    if (k != null) int.TryParse(Convert.ToString(k.GetValue("CurrentBuildNumber")), out build);
            }
            catch { }
            Win11 = build >= 22000;

            var iconFace = FontExists("Segoe Fluent Icons") ? "Segoe Fluent Icons" : "Segoe MDL2 Assets";
            Title = new Font("Segoe UI Semibold", 12f);
            Body = new Font("Segoe UI", 9.75f);
            BodyBold = new Font("Segoe UI Semibold", 9.75f);
            Small = new Font("Segoe UI", 8.25f);
            Caption = new Font("Segoe UI Semibold", 8f);
            Icons = new Font(iconFace, 11f);
            IconsSmall = new Font(iconFace, 8.5f);
            Refresh();
        }

        public static void Refresh()
        {
            Dark = RegInt(PersonalizeKey, "AppsUseLightTheme", 1) == 0;
            TaskbarDark = RegInt(PersonalizeKey, "SystemUsesLightTheme", 0) == 0;
            Accent = ReadAccent();
            OnAccent = Luma(Accent) > 0.55 ? Color.Black : Color.White;
            if (Dark)
            {
                Bg = C(32, 32, 32); Surface = C(45, 45, 45); Menu = C(44, 44, 44); Footer = C(32, 32, 32);
                Border = C(62, 62, 62); Fg = C(255, 255, 255); Fg2 = C(200, 200, 200); Fg3 = C(120, 120, 120);
            }
            else
            {
                Bg = C(243, 243, 243); Surface = C(251, 251, 251); Menu = C(249, 249, 249); Footer = C(243, 243, 243);
                Border = C(222, 222, 222); Fg = C(27, 27, 27); Fg2 = C(96, 96, 96); Fg3 = C(160, 160, 160);
            }
        }

        static Color ReadAccent()
        {
            try
            {
                // Палитра акцента: в тёмной теме Windows берёт светлый оттенок, в светлой — тёмный.
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Accent"))
                {
                    var pal = k == null ? null : k.GetValue("AccentPalette") as byte[];
                    if (pal != null && pal.Length >= 32)
                    {
                        int i = (Dark ? 1 : 4) * 4;
                        return C(pal[i], pal[i + 1], pal[i + 2]);
                    }
                }
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM"))
                {
                    var v = k == null ? null : k.GetValue("AccentColor");
                    if (v is int) { int c = (int)v; return C(c & 0xFF, (c >> 8) & 0xFF, (c >> 16) & 0xFF); }
                }
            }
            catch { }
            return Dark ? C(76, 194, 255) : C(0, 95, 184);
        }

        static int RegInt(string key, string name, int def)
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(key))
                {
                    var v = k == null ? null : k.GetValue(name);
                    return v is int ? (int)v : def;
                }
            }
            catch { return def; }
        }

        static bool FontExists(string name)
        {
            using (var f = new Font(name, 10f)) return f.Name.Equals(name, StringComparison.OrdinalIgnoreCase);
        }

        static Color C(int r, int g, int b) { return Color.FromArgb(r, g, b); }
        static double Luma(Color c) { return (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255; }

        public static int S(float v) { return (int)Math.Round(v * Scale); }

        public static Color Blend(Color a, Color b, float t)
        {
            return Color.FromArgb((int)(a.R * t + b.R * (1 - t)), (int)(a.G * t + b.G * (1 - t)), (int)(a.B * t + b.B * (1 - t)));
        }

        public static Color Hover(Color c) { return Blend(Fg, c, Dark ? 0.07f : 0.05f); }
        public static Color Press(Color c) { return Blend(Fg, c, Dark ? 0.04f : 0.08f); }

        public static void RoundCorners(IntPtr hwnd)
        {
            if (!Win11) return;
            int round = 2; // DWMWCP_ROUND
            Native.DwmSetWindowAttribute(hwnd, 33, ref round, 4);
        }

        public static GraphicsPath Round(RectangleF r, float rad)
        {
            var p = new GraphicsPath();
            float d = Math.Min(rad * 2, Math.Min(r.Width, r.Height));
            if (d < 1) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void Fill(Graphics g, Rectangle r, float rad, Color c)
        {
            if (g == null) return;
            var old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var p = Round(r, rad))
            using (var b = new SolidBrush(c)) g.FillPath(b, p);
            g.SmoothingMode = old;
        }

        public static void Stroke(Graphics g, Rectangle r, float rad, Color c)
        {
            if (g == null) return;
            var old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var p = Round(new RectangleF(r.X + 0.5f, r.Y + 0.5f, r.Width - 1, r.Height - 1), rad))
            using (var pen = new Pen(c)) g.DrawPath(pen, p);
            g.SmoothingMode = old;
        }

        public static void Label(Graphics g, string s, Font f, Rectangle r, Color c, TextFormatFlags flags)
        {
            if (g != null) TextRenderer.DrawText(g, s, f, r, c, flags);
        }

        public static void Sym(Graphics g, string glyph, Font f, Rectangle r, Color c)
        {
            if (g != null) TextRenderer.DrawText(g, glyph, f, r, c, CenterText);
        }

        public static int TextWidth(string s, Font f)
        {
            return TextRenderer.MeasureText(s, f, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width;
        }
    }

    // Меню в трее в цветах темы: скруглённая подсветка, тонкие разделители, галочки акцентом.
    class ModernRenderer : ToolStripProfessionalRenderer
    {
        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            using (var b = new SolidBrush(Theme.Menu)) e.Graphics.FillRectangle(b, e.AffectedBounds);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            if (Theme.Win11) return; // рамку и скругление рисует DWM
            using (var p = new Pen(Theme.Border)) e.Graphics.DrawRectangle(p, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
        }

        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e) { }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Enabled || !(e.Item.Selected || e.Item.Pressed)) return;
            var r = new Rectangle(Theme.S(4), 0, e.Item.Width - Theme.S(8), e.Item.Height);
            Theme.Fill(e.Graphics, r, Theme.S(4), Theme.Hover(Theme.Menu));
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            int y = e.Item.Height / 2;
            using (var p = new Pen(Theme.Border)) e.Graphics.DrawLine(p, Theme.S(4), y, e.Item.Width - Theme.S(4), y);
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            var mi = e.Item as ToolStripMenuItem;
            bool shortcut = mi != null && !string.IsNullOrEmpty(mi.ShortcutKeyDisplayString) && e.Text == mi.ShortcutKeyDisplayString;
            var color = !e.Item.Enabled ? Theme.Fg3 : shortcut ? Theme.Fg2 : Theme.Fg;
            TextRenderer.DrawText(e.Graphics, e.Text, e.TextFont, e.TextRectangle, color, e.TextFormat);
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            var r = e.ImageRectangle;
            r.Inflate(Theme.S(2), Theme.S(2));
            Theme.Sym(e.Graphics, Glyph.Check, Theme.IconsSmall, r, Theme.Accent);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = e.Item != null && !e.Item.Enabled ? Theme.Fg3 : Theme.Fg2;
            base.OnRenderArrow(e);
        }
    }

    // Окно без рамки, целиком нарисованное вручную. Draw() вызывается и для отрисовки, и (с g == null) для замера высоты;
    // Hot() регистрирует кликабельную область и сообщает, наведена ли на неё мышь.
    class PaintedForm : Form
    {
        class Spot { public Rectangle R; public Action Click; }
        readonly List<Spot> spots = new List<Spot>();
        Point mouse = new Point(-1, -1);
        Rectangle hotRect = Rectangle.Empty;
        bool down;
        int scroll;               // прокрутка, если содержимое выше окна
        protected int ContentHeight;

        public PaintedForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            KeyPreview = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x80;       // WS_EX_TOOLWINDOW — не показывать в Alt+Tab
                cp.ClassStyle |= 0x20000; // CS_DROPSHADOW
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.RoundCorners(Handle);
        }

        protected static int S(float v) { return Theme.S(v); }

        protected virtual int Draw(Graphics g) { return 0; }

        protected int Measure()
        {
            spots.Clear();
            return Draw(null);
        }

        protected void ResetMouse()
        {
            mouse = ToContent(PointToClient(Cursor.Position));
            down = false;
        }

        Point ToContent(Point p) { return new Point(p.X, p.Y + scroll); }

        protected void ScrollTop() { scroll = 0; }

        protected void ClampScroll()
        {
            scroll = Math.Max(0, Math.Min(scroll, ContentHeight - ClientSize.Height));
        }

        protected bool Hot(Rectangle r, Action click)
        {
            spots.Add(new Spot { R = r, Click = click });
            return r.Contains(mouse);
        }

        protected bool Pressed(Rectangle r) { return down && r.Contains(mouse); }

        Spot SpotAt(Point p)
        {
            for (int i = spots.Count - 1; i >= 0; i--)
                if (spots[i].R.Contains(p)) return spots[i];
            return null;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            spots.Clear();
            ClampScroll();
            e.Graphics.TranslateTransform(0, -scroll);
            Draw(e.Graphics);
            e.Graphics.ResetTransform();
            int h = ClientSize.Height;
            if (ContentHeight > h)
            {
                int thumb = Math.Max(S(24), h * h / ContentHeight);
                int ty = (int)((long)scroll * (h - thumb) / (ContentHeight - h));
                Theme.Fill(e.Graphics, new Rectangle(ClientSize.Width - S(6), ty + S(2), S(3), thumb - S(4)), S(1.5f), Theme.Fg3);
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if (ContentHeight > ClientSize.Height)
            {
                scroll -= e.Delta * S(48) / 120;
                ClampScroll();
                mouse = ToContent(e.Location);
                hotRect = Rectangle.Empty;
                Invalidate();
            }
            base.OnMouseWheel(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            mouse = ToContent(e.Location);
            var s = SpotAt(mouse);
            var r = s != null ? s.R : Rectangle.Empty;
            if (r != hotRect) { hotRect = r; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            mouse = new Point(-1, -1);
            if (hotRect != Rectangle.Empty) { hotRect = Rectangle.Empty; Invalidate(); }
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) { down = true; Invalidate(); }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            bool was = down;
            down = false;
            Invalidate();
            if (e.Button == MouseButtons.Left && was)
            {
                var s = SpotAt(ToContent(e.Location));
                if (s != null && s.Click != null) s.Click();
            }
            base.OnMouseUp(e);
        }

        protected void IconButton(Graphics g, Rectangle r, string glyph, Action click)
        {
            if (Hot(r, click)) Theme.Fill(g, r, S(5), Pressed(r) ? Theme.Press(Theme.Bg) : Theme.Hover(Theme.Bg));
            Theme.Sym(g, glyph, Theme.Icons, r, Theme.Fg);
        }

        protected void Button(Graphics g, Rectangle r, string glyph, string label, Action click, bool accent = false)
        {
            bool hot = Hot(r, click), pressed = Pressed(r);
            Color fill, fg;
            if (accent)
            {
                fill = !hot ? Theme.Accent : Theme.Blend(Theme.Bg, Theme.Accent, pressed ? 0.2f : 0.1f);
                fg = Theme.OnAccent;
            }
            else
            {
                fill = !hot ? Theme.Surface : pressed ? Theme.Press(Theme.Surface) : Theme.Hover(Theme.Surface);
                fg = Theme.Fg;
            }
            Theme.Fill(g, r, S(5), fill);
            if (!accent) Theme.Stroke(g, r, S(5), Theme.Border);

            int tw = Theme.TextWidth(label, Theme.Body);
            int iw = glyph == null ? 0 : S(16), gap = glyph == null ? 0 : S(8);
            int sx = r.X + (r.Width - iw - gap - tw) / 2;
            if (glyph != null) Theme.Sym(g, glyph, Theme.IconsSmall, new Rectangle(sx, r.Y, iw, r.Height), fg);
            Theme.Label(g, label, Theme.Body, new Rectangle(sx + iw + gap, r.Y, tw + S(2), r.Height), fg, Theme.LeftText);
        }
    }

    // Панель по левому клику на значке: мониторы, профили, переключатели.
    class Flyout : PaintedForm
    {
        readonly TrayApp app;
        public int HiddenAt;
        bool anchorTop, anchorLeft;
        Rectangle area;
        Point lastAt;
        string expanded;  // монитор, для которого раскрыт выбор режима
        bool allRes;
        readonly Dictionary<string, List<Mode>> modes = new Dictionary<string, List<Mode>>();
        static readonly string[] ScalingShort = { "масштаб по умолчанию", "растянуто", "по центру" };
        static readonly string[] ScalingChips = { "По умолчанию", "Растянуть", "По центру" };
        const int ResLimit = 15;

        class Chip
        {
            public string Text;
            public bool Selected;
            public Action Click;
            public Chip(string text, bool selected, Action click) { Text = text; Selected = selected; Click = selected ? null : click; }
        }

        public Flyout(TrayApp app)
        {
            this.app = app;
            Text = "ResSwitch";
            Deactivate += delegate { HideNow(); };
        }

        public void HideNow()
        {
            if (!Visible) return;
            Hide();
            HiddenAt = Environment.TickCount;
        }

        public void ShowNear(Point at, bool keepScroll = false)
        {
            if (!keepScroll) ScrollTop();
            Theme.Refresh();
            BackColor = Theme.Bg;
            modes.Clear();
            lastAt = at;
            var scr = Screen.FromPoint(at);
            area = scr.WorkingArea;
            anchorTop = area.Top > scr.Bounds.Top;    // панель задач сверху
            anchorLeft = area.Left > scr.Bounds.Left; // панель задач слева
            Place(Measure());
            Show();
            ResetMouse();
            Activate();
            Native.SetForegroundWindow(Handle);
        }

        void Place(int h)
        {
            int w = S(344), m = S(12);
            int x = anchorLeft ? area.Left + m : area.Right - w - m;
            ContentHeight = h;
            h = Math.Min(h, area.Height - m * 2);
            int y = anchorTop ? area.Top + m : area.Bottom - h - m;
            Bounds = new Rectangle(x, y, w, h);
            ClampScroll();
        }

        public void Relayout()
        {
            if (!Visible) return;
            Place(Measure());
            Invalidate();
        }

        List<Mode> ModesOf(string dev)
        {
            List<Mode> list;
            if (!modes.TryGetValue(dev, out list)) modes[dev] = list = Display.Modes(dev);
            return list;
        }

        // Режим из панели: панель прячется на время подтверждения и открывается снова уже с новым режимом.
        void Apply(string dev, int w, int h, int hz, int scaling)
        {
            HideNow();
            BeginInvoke((Action)delegate
            {
                app.ApplyManual(dev, w, h, hz, scaling);
                ShowNear(lastAt, true);
            });
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) HideNow();
            base.OnKeyDown(e);
        }

        protected override int Draw(Graphics g)
        {
            int pad = S(12), w = S(344), x = pad, cw = w - pad * 2, y = pad;
            if (g != null) g.Clear(Theme.Bg);

            Theme.Label(g, "ResSwitch", Theme.Title, new Rectangle(x + S(4), y, cw - S(84), S(32)), Theme.Fg, Theme.LeftText);
            var more = new Rectangle(x + cw - S(32), y, S(32), S(32));
            IconButton(g, more, Glyph.More, delegate { HideNow(); BeginInvoke((Action)app.ShowFullMenu); });
            IconButton(g, new Rectangle(more.X - S(36), y, S(32), S(32)), Glyph.Settings, delegate { HideNow(); app.OpenSettings(); });
            y += S(42);

            var mons = new List<KeyValuePair<DisplayInfo, Native.DEVMODE>>();
            foreach (var d in Display.List())
            {
                Native.DEVMODE cur;
                if (Display.Current(d.Device, out cur)) mons.Add(new KeyValuePair<DisplayInfo, Native.DEVMODE>(d, cur));
            }
            if (mons.Count > 0)
            {
                // сначала замер (фон карточки рисуется до содержимого), потом отрисовка
                var card = new Rectangle(x, y, cw, MonitorCard(null, x, y, cw, mons));
                Theme.Fill(g, card, S(8), Theme.Surface);
                Theme.Stroke(g, card, S(8), Theme.Border);
                MonitorCard(g, x, y, cw, mons);
                y = card.Bottom + S(12);
            }

            var game = app.ActiveGame;
            if (game != null)
            {
                var r = new Rectangle(x, y, cw, S(40));
                Theme.Fill(g, r, S(8), Theme.Blend(Theme.Accent, Theme.Bg, Theme.Dark ? 0.22f : 0.14f));
                Theme.Sym(g, Glyph.Game, Theme.Icons, new Rectangle(r.X + S(10), r.Y, S(24), r.Height), Theme.Accent);
                Theme.Label(g, "Игровой режим: " + game.Name, Theme.BodyBold, new Rectangle(r.X + S(44), r.Y, r.Width - S(54), r.Height), Theme.Fg, Theme.LeftText);
                y += r.Height + S(12);
            }

            Theme.Label(g, "ИЗБРАННОЕ", Theme.Caption, new Rectangle(x + S(4), y, cw, S(20)), Theme.Fg2, Theme.LeftText);
            y += S(24);
            if (app.Cfg.Favorites.Count == 0)
            {
                Theme.Label(g, "Раскройте монитор выше и нажмите «Добавить в избранное»", Theme.Small, new Rectangle(x + S(4), y, cw, S(22)), Theme.Fg3, Theme.LeftText);
                y += S(26);
            }
            else y = Favorites(g, x, y, cw, app.Cfg.Favorites);
            y += S(8);

            Theme.Label(g, "ПРОФИЛИ", Theme.Caption, new Rectangle(x + S(4), y, cw, S(20)), Theme.Fg2, Theme.LeftText);
            y += S(24);
            if (app.Cfg.Profiles.Count == 0)
            {
                Theme.Label(g, "Профилей нет — добавьте их в файле настроек", Theme.Body, new Rectangle(x + S(4), y, cw, S(28)), Theme.Fg3, Theme.LeftText);
                y += S(32);
            }
            foreach (var p in app.Cfg.Profiles)
            {
                Tile(g, new Rectangle(x, y, cw, S(56)), p);
                y += S(60);
            }

            y += S(4);
            if (g != null) using (var pen = new Pen(Theme.Border)) g.DrawLine(pen, x, y, x + cw, y);
            y += S(8);

            ToggleRow(g, new Rectangle(x, y, cw, S(40)), Glyph.Game, "Автосмена по играм", app.Cfg.GameWatch, app.ToggleGames);
            y += S(42);
            bool auto = TrayApp.IsAutostart();
            ToggleRow(g, new Rectangle(x, y, cw, S(40)), Glyph.Power, "Запускать вместе с Windows", auto, delegate { TrayApp.SetAutostart(!auto); });
            y += S(50);

            int bw = (cw - S(8)) / 2;
            Button(g, new Rectangle(x, y, bw, S(34)), Glyph.Undo, "Вернуть исходное", delegate { app.RestoreStartup(); Relayout(); });
            Button(g, new Rectangle(x + bw + S(8), y, cw - bw - S(8), S(34)), Glyph.Refresh, "Перечитать", delegate { app.ReloadConfig(); Relayout(); });
            y += S(34) + pad;
            return y;
        }

        int MonitorCard(Graphics g, int x, int y, int cw, List<KeyValuePair<DisplayInfo, Native.DEVMODE>> mons)
        {
            int ry = y + S(4), rowH = S(40);
            foreach (var kv in mons)
            {
                var d = kv.Key;
                var cur = kv.Value;
                string dev = d.Device;
                bool open = dev == expanded;
                var r = new Rectangle(x + S(4), ry, cw - S(8), rowH);
                if (Hot(r, delegate { expanded = open ? null : dev; allRes = false; Relayout(); }))
                    Theme.Fill(g, r, S(6), Pressed(r) ? Theme.Press(Theme.Surface) : Theme.Hover(Theme.Surface));
                Theme.Sym(g, Glyph.Monitor, Theme.Icons, new Rectangle(r.X + S(6), r.Y, S(24), r.Height), Theme.Accent);
                string mode = cur.dmPelsWidth + "×" + cur.dmPelsHeight + "  " + cur.dmDisplayFrequency + " Гц";
                int modeW = Theme.TextWidth(mode, Theme.BodyBold), modeX = r.Right - S(34) - modeW;
                Theme.Label(g, d.Title + (d.Primary ? " · основной" : ""), Theme.Body, new Rectangle(r.X + S(40), r.Y, modeX - S(10) - r.X - S(40), r.Height), Theme.Fg2, Theme.LeftText);
                Theme.Label(g, mode, Theme.BodyBold, new Rectangle(modeX, r.Y, modeW + S(2), r.Height), Theme.Fg, Theme.LeftText);
                Theme.Sym(g, open ? Glyph.ChevronUp : Glyph.ChevronDown, Theme.IconsSmall, new Rectangle(r.Right - S(30), r.Y, S(24), r.Height), Theme.Fg2);
                ry += rowH;
                if (open) ry = Picker(g, x + S(14), ry, cw - S(28), dev, cur);
            }
            return ry + S(4) - y;
        }

        // Плитки избранного в два столбца; при наведении появляется крестик «убрать».
        int Favorites(Graphics g, int x, int y, int w, List<Profile> favs)
        {
            int gap = S(6), fw = (w - gap) / 2, fh = S(48);
            bool many = Display.List().Count > 1;
            for (int i = 0; i < favs.Count; i++)
            {
                var f = favs[i];
                var r = new Rectangle(x + (i % 2) * (fw + gap), y + (i / 2) * (fh + gap), fw, fh);
                int state = TrayApp.ProfileState(f);
                bool active = state == 1, enabled = state >= 0;
                bool over = Hot(r, enabled ? (Action)delegate { HideNow(); BeginInvoke((Action)(() => app.ApplyProfile(f))); } : null);

                var fill = active ? Theme.Blend(Theme.Accent, Theme.Surface, Theme.Dark ? 0.22f : 0.12f) : Theme.Surface;
                if (over && enabled) fill = Pressed(r) ? Theme.Press(fill) : Theme.Hover(fill);
                Theme.Fill(g, r, S(8), fill);
                Theme.Stroke(g, r, S(8), active ? Theme.Blend(Theme.Accent, fill, 0.45f) : Theme.Border);
                if (active) Theme.Fill(g, new Rectangle(r.X + S(5), r.Y + r.Height / 2 - S(8), S(3), S(16)), S(1.5f), Theme.Accent);

                var parts = new List<string> { f.Hz > 0 ? f.Hz + " Гц" : "макс. Гц" };
                if (many)
                {
                    var dev = Display.Resolve(f.Display);
                    if (dev != null) parts.Add(Display.Title(dev));
                }
                if (f.Scaling > 0 && f.Scaling < ScalingShort.Length) parts.Add(ScalingShort[f.Scaling]);
                int tx = r.X + S(14), tw = r.Width - S(14) - S(30);
                Theme.Label(g, f.W + "×" + f.H, Theme.BodyBold, new Rectangle(tx, r.Y + S(5), tw, S(22)), enabled ? Theme.Fg : Theme.Fg3, Theme.LeftText);
                Theme.Label(g, string.Join(" · ", parts), Theme.Small, new Rectangle(tx, r.Y + S(26), tw, S(16)), enabled ? Theme.Fg2 : Theme.Fg3, Theme.LeftText);

                if (over)
                {
                    var xr = new Rectangle(r.Right - S(30), r.Y + (r.Height - S(24)) / 2, S(24), S(24));
                    if (Hot(xr, delegate { app.RemoveFavorite(f); Relayout(); }))
                        Theme.Fill(g, xr, S(4), Theme.Hover(fill));
                    Theme.Sym(g, Glyph.Close, Theme.IconsSmall, xr, Theme.Fg2);
                }
            }
            return y + (favs.Count + 1) / 2 * (fh + gap) - gap;
        }

        int Picker(Graphics g, int x, int y, int w, string dev, Native.DEVMODE cur)
        {
            var all = ModesOf(dev);
            int cw = cur.dmPelsWidth, ch = cur.dmPelsHeight, chz = cur.dmDisplayFrequency;
            y += S(2);

            bool fav = app.FindFavorite(dev, cur) != null;
            Button(g, new Rectangle(x, y, w, S(32)), fav ? Glyph.StarFill : Glyph.Star, fav ? "Убрать из избранного" : "Добавить в избранное",
                delegate { app.ToggleFavorite(dev, cur); Relayout(); });
            y += S(36);

            var res = all.GroupBy(m => m.W + "×" + m.H).Select(gr => gr.First()).ToList();
            bool collapse = !allRes && res.Count > ResLimit + 1;
            var shown = collapse ? res.Take(ResLimit).ToList() : res;
            if (collapse && !shown.Any(m => m.W == cw && m.H == ch))
            {
                var current = res.FirstOrDefault(m => m.W == cw && m.H == ch);
                if (current != null) shown[shown.Count - 1] = current;
            }
            var chips = new List<Chip>();
            foreach (var m in shown)
            {
                var mode = m;
                chips.Add(new Chip(m.W + "×" + m.H, m.W == cw && m.H == ch, delegate { Apply(dev, mode.W, mode.H, 0, -1); }));
            }
            if (collapse) chips.Add(new Chip("Все (" + res.Count + ")", false, delegate { allRes = true; Relayout(); }));
            else if (allRes) chips.Add(new Chip("Свернуть", false, delegate { allRes = false; Relayout(); }));
            y = Chips(g, "РАЗРЕШЕНИЕ", x, y, w, 4, chips);

            chips = new List<Chip>();
            foreach (var m in all.Where(x2 => x2.W == cw && x2.H == ch))
            {
                int hz = m.Hz;
                chips.Add(new Chip(hz + " Гц", hz == chz, delegate { Apply(dev, cw, ch, hz, -1); }));
            }
            y = Chips(g, "ЧАСТОТА", x, y, w, 4, chips);

            chips = new List<Chip>();
            for (int i = 0; i < ScalingChips.Length; i++)
            {
                int scaling = i;
                chips.Add(new Chip(ScalingChips[i], cur.dmDisplayFixedOutput == i, delegate { Apply(dev, cw, ch, chz, scaling); }));
            }
            y = Chips(g, "МАСШТАБИРОВАНИЕ", x, y, w, 3, chips);
            return y + S(6);
        }

        int Chips(Graphics g, string caption, int x, int y, int w, int cols, List<Chip> chips)
        {
            Theme.Label(g, caption, Theme.Caption, new Rectangle(x, y, w, S(24)), Theme.Fg2, Theme.LeftText);
            y += S(26);
            int gap = S(6), chipW = (w - gap * (cols - 1)) / cols, chipH = S(30);
            for (int i = 0; i < chips.Count; i++)
            {
                var c = chips[i];
                var r = new Rectangle(x + (i % cols) * (chipW + gap), y + (i / cols) * (chipH + gap), chipW, chipH);
                bool hot = Hot(r, c.Click);
                var fill = c.Selected ? Theme.Accent : Theme.Blend(Theme.Fg, Theme.Surface, Theme.Dark ? 0.06f : 0.05f);
                if (hot && !c.Selected) fill = Pressed(r) ? Theme.Press(fill) : Theme.Hover(fill);
                Theme.Fill(g, r, S(5), fill);
                Theme.Label(g, c.Text, Theme.Small, r, c.Selected ? Theme.OnAccent : Theme.Fg, Theme.CenterText);
            }
            int rows = (chips.Count + cols - 1) / cols;
            return y + rows * (chipH + gap) + S(4);
        }

        void Tile(Graphics g, Rectangle r, Profile p)
        {
            int state = TrayApp.ProfileState(p);
            bool active = state == 1, enabled = state >= 0;
            bool hot = enabled && Hot(r, delegate { HideNow(); BeginInvoke((Action)(() => app.ApplyProfile(p))); });

            var fill = active ? Theme.Blend(Theme.Accent, Theme.Surface, Theme.Dark ? 0.22f : 0.12f) : Theme.Surface;
            if (hot) fill = Pressed(r) ? Theme.Press(fill) : Theme.Hover(fill);
            Theme.Fill(g, r, S(8), fill);
            Theme.Stroke(g, r, S(8), active ? Theme.Blend(Theme.Accent, fill, 0.45f) : Theme.Border);
            if (active) Theme.Fill(g, new Rectangle(r.X + S(5), r.Y + r.Height / 2 - S(10), S(3), S(20)), S(1.5f), Theme.Accent);

            int right = r.Right - S(12);
            if (!string.IsNullOrEmpty(p.Hotkey))
            {
                int tw = Theme.TextWidth(p.Hotkey, Theme.Small);
                var kr = new Rectangle(right - tw - S(14), r.Y + (r.Height - S(22)) / 2, tw + S(14), S(22));
                Theme.Fill(g, kr, S(4), Theme.Blend(Theme.Fg, fill, 0.06f));
                Theme.Stroke(g, kr, S(4), Theme.Border);
                Theme.Label(g, p.Hotkey, Theme.Small, kr, Theme.Fg2, Theme.CenterText);
                right = kr.X - S(10);
            }
            int tx = r.X + S(16);
            Theme.Label(g, p.Name, Theme.BodyBold, new Rectangle(tx, r.Y + S(8), right - tx, S(22)), enabled ? Theme.Fg : Theme.Fg3, Theme.LeftText);
            Theme.Label(g, Describe(p), Theme.Small, new Rectangle(tx, r.Y + S(30), right - tx, S(18)), enabled ? Theme.Fg2 : Theme.Fg3, Theme.LeftText);
        }

        static string Describe(Profile p)
        {
            var parts = new List<string> { p.W + "×" + p.H + (p.Hz > 0 ? " @ " + p.Hz + " Гц" : "") };
            var dev = Display.Resolve(p.Display);
            if (dev != null) parts.Add(Display.Title(dev));
            if (p.Scaling >= 0 && p.Scaling < ScalingShort.Length) parts.Add(ScalingShort[p.Scaling]);
            if (p.Games.Count > 0) parts.Add("игра: " + string.Join(", ", p.Games));
            return string.Join(" · ", parts);
        }

        void ToggleRow(Graphics g, Rectangle r, string glyph, string label, bool on, Action toggle)
        {
            bool hot = Hot(r, delegate { toggle(); Relayout(); });
            if (hot) Theme.Fill(g, r, S(6), Pressed(r) ? Theme.Press(Theme.Bg) : Theme.Hover(Theme.Bg));
            Theme.Sym(g, glyph, Theme.Icons, new Rectangle(r.X + S(6), r.Y, S(24), r.Height), Theme.Fg2);
            Theme.Label(g, label, Theme.Body, new Rectangle(r.X + S(40), r.Y, r.Width - S(100), r.Height), Theme.Fg, Theme.LeftText);
            Switch(g, new Rectangle(r.Right - S(52), r.Y + (r.Height - S(20)) / 2, S(40), S(20)), on, hot);
        }

        static void Switch(Graphics g, Rectangle r, bool on, bool hot)
        {
            if (g == null) return;
            int rad = r.Height / 2;
            var old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (on)
            {
                Theme.Fill(g, r, rad, hot ? Theme.Blend(Theme.Bg, Theme.Accent, 0.1f) : Theme.Accent);
                int d = r.Height - S(8);
                using (var b = new SolidBrush(Theme.OnAccent)) g.FillEllipse(b, r.Right - S(4) - d, r.Y + S(4), d, d);
            }
            else
            {
                if (hot) Theme.Fill(g, r, rad, Theme.Hover(Theme.Bg));
                Theme.Stroke(g, r, rad, Theme.Fg2);
                int d = r.Height - S(10);
                using (var b = new SolidBrush(Theme.Fg2)) g.FillEllipse(b, r.X + S(5), r.Y + S(5), d, d);
            }
            g.SmoothingMode = old;
        }
    }

    class ConfirmForm : PaintedForm
    {
        readonly double total;
        readonly string what;
        readonly Stopwatch clock = new Stopwatch();
        readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();

        public ConfirmForm(int seconds, string what, Rectangle screen)
        {
            total = seconds;
            this.what = what;
            Text = "ResSwitch";
            Theme.Refresh();
            BackColor = Theme.Menu;
            var size = new Size(S(420), S(206));
            Bounds = new Rectangle(screen.X + (screen.Width - size.Width) / 2, screen.Y + (screen.Height - size.Height) / 2, size.Width, size.Height);

            timer.Interval = 30;
            timer.Tick += delegate
            {
                if (clock.Elapsed.TotalSeconds >= total) { timer.Stop(); DialogResult = DialogResult.Cancel; }
                else Invalidate();
            };
            Shown += delegate { ResetMouse(); Activate(); Native.SetForegroundWindow(Handle); clock.Start(); timer.Start(); };
            FormClosed += delegate { timer.Dispose(); };
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter) DialogResult = DialogResult.OK;
            else if (e.KeyCode == Keys.Escape) DialogResult = DialogResult.Cancel;
            base.OnKeyDown(e);
        }

        protected override int Draw(Graphics g)
        {
            int w = ClientSize.Width, h = ClientSize.Height, pad = S(24), footer = S(72);
            g.Clear(Theme.Menu);
            using (var b = new SolidBrush(Theme.Footer)) g.FillRectangle(b, 0, h - footer, w, footer);
            using (var pen = new Pen(Theme.Border)) g.DrawLine(pen, 0, h - footer, w, h - footer);

            var ic = new Rectangle(pad, pad, S(40), S(40));
            Theme.Fill(g, ic, S(20), Theme.Blend(Theme.Accent, Theme.Menu, Theme.Dark ? 0.22f : 0.14f));
            Theme.Sym(g, Glyph.Monitor, Theme.Icons, ic, Theme.Accent);
            int tx = ic.Right + S(14);
            Theme.Label(g, "Оставить это разрешение?", Theme.Title, new Rectangle(tx, pad - S(2), w - tx - pad, S(24)), Theme.Fg, Theme.LeftText);
            Theme.Label(g, what, Theme.Body, new Rectangle(tx, pad + S(22), w - tx - pad, S(20)), Theme.Fg2, Theme.LeftText);

            double left = Math.Max(0, total - clock.Elapsed.TotalSeconds);
            var bar = new Rectangle(pad, S(86), w - pad * 2, S(4));
            Theme.Fill(g, bar, S(2), Theme.Border);
            int fw = (int)(bar.Width * left / total);
            if (fw > 0) Theme.Fill(g, new Rectangle(bar.X, bar.Y, fw, bar.Height), S(2), Theme.Accent);
            Theme.Label(g, "Прежний режим вернётся через " + (int)Math.Ceiling(left) + " с", Theme.Small,
                new Rectangle(pad, bar.Bottom + S(8), bar.Width, S(18)), Theme.Fg2, Theme.LeftText);

            int bh = S(32), bw = S(120), by = h - footer + (footer - bh) / 2;
            var back = new Rectangle(w - pad - bw, by, bw, bh);
            Button(g, new Rectangle(back.X - S(8) - bw, by, bw, bh), null, "Оставить", delegate { DialogResult = DialogResult.OK; }, true);
            Button(g, back, null, "Вернуть", delegate { DialogResult = DialogResult.Cancel; });
            return h;
        }
    }

    class TrayApp : ApplicationContext
    {
        Config cfg;
        readonly NotifyIcon icon;
        readonly ContextMenuStrip menu;
        readonly Flyout flyout;
        readonly HotkeyWindow hotkeys;
        readonly System.Windows.Forms.Timer gameTimer = new System.Windows.Forms.Timer();
        readonly Dictionary<string, Native.DEVMODE> startup = new Dictionary<string, Native.DEVMODE>();
        readonly List<int> hotkeyIds = new List<int>();
        Profile activeGame;
        string activeGameDevice;
        Native.DEVMODE gameSaved;
        bool busy;
        bool? iconGame;

        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        public TrayApp()
        {
            foreach (var d in Display.List())
            {
                Native.DEVMODE dm;
                if (Display.Current(d.Device, out dm)) startup[d.Device] = dm;
            }

            ToolStripManager.Renderer = new ModernRenderer();
            menu = new ContextMenuStrip { Font = Theme.Body, Padding = new Padding(0, Theme.S(4), 0, Theme.S(4)) };
            menu.Items.Add("...");
            menu.Opening += (s, e) => { flyout.HideNow(); Theme.Refresh(); BuildMenu(); e.Cancel = false; };
            menu.HandleCreated += (s, e) => Theme.RoundCorners(menu.Handle);

            icon = new NotifyIcon { ContextMenuStrip = menu };
            RefreshIcon(true);
            icon.Visible = true;
            icon.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) ToggleFlyout(); };

            flyout = new Flyout(this);
            hotkeys = new HotkeyWindow(OnHotkey);
            gameTimer.Tick += (s, e) => GameTick();
            LoadConfig(true);
            UpdateTooltip();
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        }

        internal Config Cfg { get { return cfg; } }
        internal Profile ActiveGame { get { return activeGame; } }

        void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            Theme.Refresh();
            RefreshIcon(true);
        }

        void ToggleFlyout()
        {
            if (flyout.Visible) { flyout.HideNow(); return; }
            // клик по значку сначала закрывает панель (потеря фокуса) — не открывать её тут же снова
            if (unchecked(Environment.TickCount - flyout.HiddenAt) < 400) return;
            flyout.ShowNear(Cursor.Position);
        }

        internal void ShowFullMenu()
        {
            var mi = typeof(NotifyIcon).GetMethod("ShowContextMenu", BindingFlags.Instance | BindingFlags.NonPublic);
            if (mi != null) mi.Invoke(icon, null);
        }

        void LoadConfig(bool first)
        {
            try { cfg = Config.Load(); }
            catch (Exception ex)
            {
                Notify("Ошибка чтения настроек: " + ex.Message, ToolTipIcon.Error);
                if (cfg == null) cfg = new Config();
                return;
            }

            foreach (var id in hotkeyIds) Native.UnregisterHotKey(hotkeys.Handle, id);
            hotkeyIds.Clear();
            var problems = new List<string>(cfg.Errors);
            for (int i = 0; i < cfg.Profiles.Count; i++)
            {
                var p = cfg.Profiles[i];
                if (string.IsNullOrWhiteSpace(p.Hotkey)) continue;
                uint mods, vk;
                if (!HotkeyWindow.Parse(p.Hotkey, out mods, out vk)) { problems.Add("не понял клавиши «" + p.Hotkey + "»"); continue; }
                int id = i + 1;
                if (Native.RegisterHotKey(hotkeys.Handle, id, mods, vk)) hotkeyIds.Add(id);
                else problems.Add(p.Hotkey + " уже занято другой программой");
            }

            gameTimer.Interval = cfg.PollMs;
            gameTimer.Enabled = cfg.GameWatch && cfg.Profiles.Any(p => p.Games.Count > 0);

            if (problems.Count > 0) Notify(string.Join("\n", problems), ToolTipIcon.Warning);
            else if (!first) Notify("Настройки загружены: профилей " + cfg.Profiles.Count, ToolTipIcon.Info);
        }

        internal void ReloadConfig() { LoadConfig(false); }

        void OnHotkey(int id)
        {
            int idx = id - 1;
            if (idx >= 0 && idx < cfg.Profiles.Count) ApplyProfile(cfg.Profiles[idx]);
        }

        internal void ApplyProfile(Profile p)
        {
            var dev = Display.Resolve(p.Display);
            if (dev == null) { Notify("Монитор не найден: " + p.Display, ToolTipIcon.Error); return; }
            ApplyManual(dev, p.W, p.H, p.Hz, p.Scaling);
        }

        // -1 — монитор недоступен, 0 — не активен, 1 — этот режим сейчас включён
        internal static int ProfileState(Profile p)
        {
            var dev = Display.Resolve(p.Display);
            Native.DEVMODE cur;
            if (dev == null || !Display.Current(dev, out cur)) return -1;
            return cur.dmPelsWidth == p.W && cur.dmPelsHeight == p.H && (p.Hz <= 0 || cur.dmDisplayFrequency == p.Hz) ? 1 : 0;
        }

        internal Profile FindFavorite(string dev, Native.DEVMODE cur)
        {
            return cfg.Favorites.FirstOrDefault(f => Display.Resolve(f.Display) == dev && f.W == cur.dmPelsWidth && f.H == cur.dmPelsHeight
                && (f.Hz <= 0 || f.Hz == cur.dmDisplayFrequency));
        }

        // Добавляет текущий режим монитора в избранное или убирает его оттуда.
        internal void ToggleFavorite(string dev, Native.DEVMODE cur)
        {
            var fav = FindFavorite(dev, cur);
            if (fav != null) cfg.Favorites.Remove(fav);
            else
            {
                int scaling = cur.dmDisplayFixedOutput;
                cfg.Favorites.Add(new Profile
                {
                    Display = Display.Spec(dev),
                    W = cur.dmPelsWidth, H = cur.dmPelsHeight, Hz = cur.dmDisplayFrequency,
                    Scaling = scaling >= 0 && scaling < Display.ScalingKeys.Length ? scaling : -1
                });
            }
            SaveFavorites();
        }

        internal void RemoveFavorite(Profile f)
        {
            cfg.Favorites.Remove(f);
            SaveFavorites();
        }

        void SaveFavorites()
        {
            try { Config.SaveFavorites(cfg.Favorites); }
            catch (Exception ex) { Notify("Не удалось сохранить избранное: " + ex.Message, ToolTipIcon.Error); }
        }

        internal void ApplyManual(string dev, int w, int h, int hz, int scaling)
        {
            if (busy) return;
            busy = true;
            try
            {
                Native.DEVMODE old;
                if (!Display.Current(dev, out old)) { Notify("Не удалось прочитать режим " + dev, ToolTipIcon.Error); return; }
                int r = Display.Change(dev, w, h, hz, scaling, true, false);
                if (r != 0) { Notify(w + "×" + h + ": " + Display.ErrorText(r), ToolTipIcon.Error); return; }

                if (cfg.ConfirmSeconds > 0)
                {
                    Native.DEVMODE now;
                    Display.Current(dev, out now);
                    using (var f = new ConfirmForm(cfg.ConfirmSeconds, Display.Describe(now), Display.Bounds(dev)))
                    {
                        if (f.ShowDialog() != DialogResult.OK) Display.Restore(dev, old, true);
                    }
                }
            }
            finally
            {
                busy = false;
                UpdateTooltip();
            }
        }

        void GameTick()
        {
            if (!cfg.GameWatch || busy) return;
            var running = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pr in Process.GetProcesses())
            {
                try { running.Add(pr.ProcessName); } catch { }
                pr.Dispose();
            }

            if (activeGame != null)
            {
                if (activeGame.Games.Any(running.Contains)) return;
                Display.Restore(activeGameDevice, gameSaved, false);
                activeGame = null;
                UpdateTooltip();
                return;
            }

            foreach (var p in cfg.Profiles)
            {
                if (p.Games.Count == 0 || !p.Games.Any(running.Contains)) continue;
                var dev = Display.Resolve(p.Display);
                Native.DEVMODE saved;
                if (dev == null || !Display.Current(dev, out saved)) continue;
                int r = Display.Change(dev, p.W, p.H, p.Hz, p.Scaling, false, false);
                activeGame = p; // даже при ошибке — чтобы не долбить драйвер каждые 2 секунды
                activeGameDevice = dev;
                gameSaved = saved;
                if (r != 0) Notify(p.Name + ": " + Display.ErrorText(r), ToolTipIcon.Error);
                UpdateTooltip();
                return;
            }
        }

        void RestoreGame()
        {
            if (activeGame == null) return;
            Display.Restore(activeGameDevice, gameSaved, false);
            activeGame = null;
        }

        internal void RestoreStartup()
        {
            RestoreGame();
            foreach (var kv in startup) Display.Restore(kv.Key, kv.Value, true);
            UpdateTooltip();
        }

        internal void ToggleGames()
        {
            cfg.GameWatch = !cfg.GameWatch;
            if (!cfg.GameWatch) RestoreGame();
            gameTimer.Enabled = cfg.GameWatch && cfg.Profiles.Any(p => p.Games.Count > 0);
            UpdateTooltip();
        }

        internal void OpenSettings()
        {
            if (!File.Exists(Config.FilePath)) Config.Load();
            Process.Start("notepad.exe", "\"" + Config.FilePath + "\"");
        }

        void BuildMenu()
        {
            foreach (ToolStripItem it in menu.Items.Cast<ToolStripItem>().ToList()) it.Dispose();
            menu.Items.Clear();
            var displays = Display.List();

            if (cfg.Profiles.Count == 0) menu.Items.Add(new ToolStripMenuItem("Нет профилей — добавьте в настройках") { Enabled = false });
            foreach (var p in cfg.Profiles)
            {
                var item = new ToolStripMenuItem(p.Name) { ShortcutKeyDisplayString = p.Hotkey };
                int state = ProfileState(p);
                item.Checked = state == 1;
                item.Enabled = state >= 0;
                if (p.Games.Count > 0) item.ToolTipText = "Автоматически для: " + string.Join(", ", p.Games);
                var prof = p;
                item.Click += (s, e) => ApplyProfile(prof);
                menu.Items.Add(item);
            }
            if (cfg.Favorites.Count > 0) menu.Items.Add(new ToolStripSeparator());
            foreach (var f in cfg.Favorites)
            {
                var dev = Display.Resolve(f.Display);
                var item = new ToolStripMenuItem("★ " + f.W + "×" + f.H + (f.Hz > 0 ? " @ " + f.Hz + " Гц" : "") + (dev != null ? " · " + Display.Title(dev) : ""));
                int state = ProfileState(f);
                item.Checked = state == 1;
                item.Enabled = state >= 0;
                var fav = f;
                item.Click += (s, e) => ApplyProfile(fav);
                menu.Items.Add(item);
            }
            menu.Items.Add(new ToolStripSeparator());

            foreach (var d in displays)
            {
                Native.DEVMODE cur;
                if (!Display.Current(d.Device, out cur)) continue;
                var dev = d.Device;
                var head = new ToolStripMenuItem(d.Title + (d.Primary ? " (основной)" : "") + ": " + Display.Describe(cur));
                head.ToolTipText = d.Short + " · " + d.Adapter + (d.Id != null ? "\nID: " + d.Id : "");

                foreach (var grp in Display.Modes(dev).GroupBy(m => m.W + "×" + m.H))
                {
                    var resItem = new ToolStripMenuItem(grp.Key);
                    var first = grp.First();
                    resItem.Checked = first.W == cur.dmPelsWidth && first.H == cur.dmPelsHeight;
                    foreach (var m in grp)
                    {
                        var mode = m;
                        var hzItem = new ToolStripMenuItem(m.Hz + " Гц")
                        {
                            Checked = resItem.Checked && m.Hz == cur.dmDisplayFrequency
                        };
                        hzItem.Click += (s, e) => ApplyManual(dev, mode.W, mode.H, mode.Hz, -1);
                        resItem.DropDownItems.Add(hzItem);
                    }
                    head.DropDownItems.Add(resItem);
                }

                head.DropDownItems.Add(new ToolStripSeparator());
                var scale = new ToolStripMenuItem("Масштабирование");
                for (int i = 0; i < Display.ScalingNames.Length; i++)
                {
                    int mode = i;
                    var si = new ToolStripMenuItem(Display.ScalingNames[i]) { Checked = cur.dmDisplayFixedOutput == i };
                    int w = cur.dmPelsWidth, h = cur.dmPelsHeight, hz = cur.dmDisplayFrequency;
                    si.Click += (s, e) => ApplyManual(dev, w, h, hz, mode);
                    scale.DropDownItems.Add(si);
                }
                head.DropDownItems.Add(scale);
                if (d.Id != null)
                {
                    var id = d.Id;
                    var copy = new ToolStripMenuItem("Скопировать ID для настроек (" + id + ")");
                    copy.Click += (s, e) => { try { Clipboard.SetText(id); } catch (ExternalException) { } };
                    head.DropDownItems.Add(copy);
                }
                menu.Items.Add(head);
            }
            menu.Items.Add(new ToolStripSeparator());

            var restore = new ToolStripMenuItem("Вернуть как было при запуске");
            restore.Click += (s, e) => RestoreStartup();
            menu.Items.Add(restore);

            var games = new ToolStripMenuItem("Автосмена по играм") { Checked = cfg.GameWatch };
            if (activeGame != null) games.Text += " — сейчас: " + activeGame.Name;
            games.Click += (s, e) => ToggleGames();
            menu.Items.Add(games);

            var open = new ToolStripMenuItem("Открыть настройки");
            open.Click += (s, e) => OpenSettings();
            menu.Items.Add(open);

            var reload = new ToolStripMenuItem("Перезагрузить настройки");
            reload.Click += (s, e) => ReloadConfig();
            menu.Items.Add(reload);

            var auto = new ToolStripMenuItem("Запускать вместе с Windows") { Checked = IsAutostart() };
            auto.Click += (s, e) => SetAutostart(!IsAutostart());
            menu.Items.Add(auto);

            menu.Items.Add(new ToolStripSeparator());
            var exit = new ToolStripMenuItem("Выход");
            exit.Click += (s, e) => Quit();
            menu.Items.Add(exit);

            StyleMenu(menu.Items);
        }

        // Повыше пункты, скруглённые подменю, тот же шрифт во всех уровнях.
        void StyleMenu(ToolStripItemCollection items)
        {
            foreach (ToolStripItem it in items)
            {
                var mi = it as ToolStripMenuItem;
                if (mi == null) continue;
                mi.Padding = new Padding(0, Theme.S(3), 0, Theme.S(3));
                if (!mi.HasDropDownItems) continue;
                var dd = mi.DropDown;
                dd.Font = menu.Font;
                dd.Padding = menu.Padding;
                dd.HandleCreated += (s, e) => Theme.RoundCorners(((Control)s).Handle);
                StyleMenu(mi.DropDownItems);
            }
        }

        internal static bool IsAutostart()
        {
            using (var k = Registry.CurrentUser.OpenSubKey(RunKey))
                return k != null && k.GetValue("ResSwitch") != null;
        }

        internal static void SetAutostart(bool on)
        {
            using (var k = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (on) k.SetValue("ResSwitch", "\"" + Application.ExecutablePath + "\"");
                else k.DeleteValue("ResSwitch", false);
            }
        }

        void UpdateTooltip()
        {
            var parts = new List<string>();
            foreach (var d in Display.List())
            {
                Native.DEVMODE cur;
                if (Display.Current(d.Device, out cur)) parts.Add(cur.dmPelsWidth + "×" + cur.dmPelsHeight + "@" + cur.dmDisplayFrequency);
            }
            var text = "ResSwitch: " + string.Join(", ", parts);
            if (activeGame != null) text += " [игра]";
            icon.Text = text.Length > 63 ? text.Substring(0, 63) : text;
            RefreshIcon(false);
            flyout.Relayout();
        }

        void RefreshIcon(bool force)
        {
            bool game = activeGame != null;
            if (!force && iconGame == game) return;
            var old = icon.Icon;
            icon.Icon = MakeIcon(game);
            if (old != null) old.Dispose();
            iconGame = game;
        }

        void Notify(string text, ToolTipIcon kind)
        {
            icon.ShowBalloonTip(3000, "ResSwitch", text.Length > 250 ? text.Substring(0, 250) : text, kind);
        }

        void Quit()
        {
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
            RestoreGame();
            gameTimer.Stop();
            foreach (var id in hotkeyIds) Native.UnregisterHotKey(hotkeys.Handle, id);
            hotkeys.Dispose();
            flyout.Dispose();
            icon.Visible = false;
            icon.Dispose();
            ExitThread();
        }

        // Монитор акцентного цвета; в игровом режиме — жёлтая точка в углу.
        static Icon MakeIcon(bool game)
        {
            int s = Math.Max(16, SystemInformation.SmallIconSize.Width);
            float u = s / 16f;
            using (var bmp = new Bitmap(s, s))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    var screen = new RectangleF(0.5f * u, 1.5f * u, 15f * u, 10.5f * u);
                    using (var path = Theme.Round(screen, 2f * u))
                    using (var b = new LinearGradientBrush(screen, Theme.Blend(Color.White, Theme.Accent, 0.25f), Theme.Accent, 90f))
                        g.FillPath(b, path);

                    var fg = Theme.TaskbarDark ? Color.White : Color.FromArgb(40, 40, 40);
                    using (var b = new SolidBrush(fg))
                    {
                        g.FillRectangle(b, 7f * u, 12f * u, 2f * u, 2f * u);
                        using (var path = Theme.Round(new RectangleF(4f * u, 13.5f * u, 8f * u, 1.75f * u), 0.8f * u)) g.FillPath(b, path);
                    }

                    using (var pen = new Pen(Theme.OnAccent, 1.4f * u) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                    {
                        g.DrawLines(pen, new[] { new PointF(3.5f * u, 6.5f * u), new PointF(3.5f * u, 4f * u), new PointF(6f * u, 4f * u) });
                        g.DrawLines(pen, new[] { new PointF(12.5f * u, 7f * u), new PointF(12.5f * u, 9.5f * u), new PointF(10f * u, 9.5f * u) });
                    }

                    if (game)
                    {
                        float d = 6.5f * u;
                        using (var b = new SolidBrush(fg)) g.FillEllipse(b, s - d - 0.25f * u, s - d - 0.25f * u, d + 0.25f * u, d + 0.25f * u);
                        using (var b = new SolidBrush(Color.FromArgb(255, 185, 0))) g.FillEllipse(b, s - d + 0.75f * u, s - d + 0.75f * u, d - 1.75f * u, d - 1.75f * u);
                    }
                }
                IntPtr h = bmp.GetHicon();
                var ic = (Icon)Icon.FromHandle(h).Clone();
                Native.DestroyIcon(h);
                return ic;
            }
        }
    }

    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            Native.SetProcessDPIAware();
            if (args.Length > 0) return Cli(args);

            bool created;
            using (new Mutex(true, "ResSwitch_SingleInstance", out created))
            {
                if (!created)
                {
                    MessageBox.Show("ResSwitch уже запущен — ищите значок в трее.", "ResSwitch");
                    return 0;
                }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new TrayApp());
            }
            return 0;
        }

        // ResSwitch.exe --list
        // ResSwitch.exe --set 1920x1080@144 [--display 1] [--scaling stretch] [--test]
        static int Cli(string[] args)
        {
            if (Native.GetStdHandle(-11) == IntPtr.Zero) Native.AttachConsole(-1);
            try { Console.OutputEncoding = Encoding.UTF8; } catch (IOException) { } // нет консоли (вывод в файл)

            if (args[0] == "--list")
            {
                foreach (var d in Display.List())
                {
                    Native.DEVMODE cur;
                    Display.Current(d.Device, out cur);
                    Console.WriteLine(d.Short + (d.Primary ? " (основной)" : "") + " — " + (d.Name ?? d.Monitor) + " — " + d.Adapter);
                    if (d.Id != null) Console.WriteLine("  ID: " + d.Id + "   (для Display= в настройках)");
                    Console.WriteLine("  сейчас: " + Display.Describe(cur) + ", масштабирование: " + cur.dmDisplayFixedOutput);
                    foreach (var grp in Display.Modes(d.Device).GroupBy(m => m.W + "x" + m.H))
                        Console.WriteLine("  " + grp.Key.PadRight(10) + " " + string.Join(", ", grp.Select(m => m.Hz + "")));
                }
                return 0;
            }

            if (args[0] == "--set" && args.Length > 1)
            {
                int w, h, hz;
                if (!Display.ParseMode(args[1], out w, out h, out hz)) { Console.WriteLine("Не понял режим: " + args[1]); return 2; }
                string display = null;
                int scaling = -1;
                bool test = false;
                for (int i = 2; i < args.Length; i++)
                {
                    if (args[i] == "--display" && i + 1 < args.Length) display = args[++i];
                    else if (args[i] == "--scaling" && i + 1 < args.Length) scaling = Display.ParseScaling(args[++i]);
                    else if (args[i] == "--test") test = true;
                }
                var dev = Display.Resolve(display);
                if (dev == null) { Console.WriteLine("Монитор не найден: " + display); return 1; }
                int r = Display.Change(dev, w, h, hz, scaling, true, test);
                Console.WriteLine(dev + " " + args[1] + (test ? " (проверка)" : "") + ": " + Display.ErrorText(r));
                return r == 0 ? 0 : 1;
            }

            Console.WriteLine("ResSwitch.exe            — запуск в трее");
            Console.WriteLine("ResSwitch.exe --list     — мониторы и доступные режимы");
            Console.WriteLine("ResSwitch.exe --set 1920x1080@144 [--display 1] [--scaling stretch|center|default] [--test]");
            return 0;
        }
    }
}
