using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace DeviceTreeJikken;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var tree = DeviceTreeBuilder.Build();
        DeviceListView.ItemsSource = Flatten(tree);
        var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var outputPath = System.IO.Path.Combine(desktopPath, "devicetreecs.txt");

        using var writer = File.CreateText(outputPath);

        foreach (var root in tree)
            Print(root, writer);
    }

    private static List<DeviceNode> Flatten(List<DeviceNode> tree)
    {
        var result = new List<DeviceNode>();

        foreach (var root in tree)
            AddNode(root, result, 0);

        return result;

        static void AddNode(DeviceNode node, List<DeviceNode> list, int indent)
        {
            node.IndentedDisplayName = node.DisplayName;
            node.IconMargin = new Thickness(indent * 16, 0, 2, 0);
            list.Add(node);
            foreach (var child in node.Children)
                AddNode(child, list, indent + 1);
        }
    }

    private void DeviceListView_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var dep = e.OriginalSource as DependencyObject;
        while (dep is not null && dep is not ListBoxItem)
            dep = VisualTreeHelper.GetParent(dep);

        if (dep is ListBoxItem item)
            item.IsSelected = true;
    }

    private void CopyMenuItemToClipboard(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.Tag is string text)
            Clipboard.SetText(text);
    }

    void Print(DeviceNode node, StreamWriter writer, string indent = "")
    {
        return;
        WriteLine(indent + "表示名: " + node.DisplayName);
        WriteLine(indent + "クラス名: " + node.ClassName);
        WriteLine(indent + "デバイスインスタンスパス: " + node.DeviceInstancePath);
        WriteLine(indent + "ハードウェアID:");

        if (node.HardwareIds.Count == 0)
        {
            WriteLine(indent + "  - (なし)");
        }
        else
        {
            foreach (var hardwareId in node.HardwareIds)
                WriteLine(indent + "  - " + hardwareId);
        }

        WriteLine(string.Empty);

        foreach (var child in node.Children)
            Print(child, writer, indent + "  ");

        void WriteLine(string text)
        {
            Debug.WriteLine(text);
            writer.WriteLine(text);
        }
    }
}


public static class DeviceTreeBuilder
{
    public static List<DeviceNode> Build()
    {
        var result = new List<DeviceNode>();

        IntPtr hDevInfo = NativeMethods.SetupDiGetClassDevs(
            IntPtr.Zero, null, IntPtr.Zero,
            NativeMethods.DIGCF_ALLCLASSES | NativeMethods.DIGCF_PRESENT);

        uint index = 0;
        var devInfo = new NativeMethods.SP_DEVINFO_DATA();
        devInfo.cbSize = (uint)Marshal.SizeOf(devInfo);

        var nodes = new Dictionary<uint, DeviceNode>();

        while (NativeMethods.SetupDiEnumDeviceInfo(hDevInfo, index, ref devInfo))
        {
            var displayName = GetDeviceDisplayName(hDevInfo, devInfo);
            var className = GetDeviceClassName(hDevInfo, devInfo);
            var hardwareIds = GetDeviceHardwareIds(hDevInfo, devInfo);
            var instancePath = GetDeviceInstancePath(hDevInfo, devInfo);
            var node = new DeviceNode
            {
                DisplayName = displayName,
                ClassName = className,
                HardwareIds = hardwareIds,
                DeviceInstancePath = instancePath,
                Icon = DeviceIconProvider.GetIcon(devInfo.ClassGuid)
            };
            nodes[devInfo.DevInst] = node;
            index++;
        }

        // 親子関係を構築
        foreach (var kv in nodes)
        {
            uint devInst = kv.Key;
            var node = kv.Value;

            if (NativeMethods.CM_Get_Parent(out uint parent, devInst, 0) == 0)
            {
                if (nodes.TryGetValue(parent, out var parentNode))
                {
                    parentNode.Children.Add(node);
                }
            }
            else
            {
                // 親がいない → ルート
                result.Add(node);
            }
        }

        return result;
    }

    private static string GetDeviceDisplayName(IntPtr hDevInfo, NativeMethods.SP_DEVINFO_DATA devInfo)
    {
        var friendlyName = GetDevicePropertyString(hDevInfo, devInfo, NativeMethods.SPDRP_FRIENDLYNAME);
        if (!string.IsNullOrWhiteSpace(friendlyName))
            return friendlyName;

        return GetDevicePropertyString(hDevInfo, devInfo, NativeMethods.SPDRP_DEVICEDESC);
    }

    private static string GetDeviceClassName(IntPtr hDevInfo, NativeMethods.SP_DEVINFO_DATA devInfo)
    {
        return GetDevicePropertyString(hDevInfo, devInfo, NativeMethods.SPDRP_CLASS);
    }

    private static List<string> GetDeviceHardwareIds(IntPtr hDevInfo, NativeMethods.SP_DEVINFO_DATA devInfo)
    {
        byte[] buffer = new byte[8192];
        if (!NativeMethods.SetupDiGetDeviceRegistryProperty(
                hDevInfo, ref devInfo,
                NativeMethods.SPDRP_HARDWAREID,
                out _, buffer, (uint)buffer.Length, out uint requiredSize) || requiredSize == 0)
        {
            return new List<string>();
        }

        var text = Encoding.Unicode.GetString(buffer, 0, (int)requiredSize);
        return text
            .Split('\0', StringSplitOptions.RemoveEmptyEntries)
            .ToList();
    }

    private static string GetDeviceInstancePath(IntPtr hDevInfo, NativeMethods.SP_DEVINFO_DATA devInfo)
    {
        var sb = new StringBuilder(1024);
        if (NativeMethods.SetupDiGetDeviceInstanceId(hDevInfo, ref devInfo, sb, sb.Capacity, out _))
            return sb.ToString();

        return string.Empty;
    }

    private static string GetDevicePropertyString(IntPtr hDevInfo, NativeMethods.SP_DEVINFO_DATA devInfo, uint property)
    {
        byte[] buffer = new byte[2048];
        if (!NativeMethods.SetupDiGetDeviceRegistryProperty(
            hDevInfo, ref devInfo,
            property,
            out _, buffer, (uint)buffer.Length, out uint requiredSize) || requiredSize == 0)
        {
            return string.Empty;
        }

        return Encoding.Unicode.GetString(buffer, 0, (int)requiredSize).TrimEnd('\0');
    }
}

public class DeviceNode
{
    public string DisplayName { get; set; } = string.Empty;
    public string IndentedDisplayName { get; set; } = string.Empty;
    public string DisplayNameMenuText => "表示名：" + DisplayName;
    public string ClassName { get; set; } = string.Empty;
    public string ClassNameMenuText => "クラス名：" + ClassName;
    public string DeviceInstancePath { get; set; } = string.Empty;
    public string DeviceInstancePathMenuText => "デバイスインスタンスパス：" + DeviceInstancePath;
    public ImageSource? Icon { get; set; }
    public Thickness IconMargin { get; set; } = new Thickness(0, 0, 6, 0);
    public List<string> HardwareIds { get; set; } = new();
    public List<DeviceNode> Children { get; set; } = new();
}

public static class DeviceIconProvider
{
    private static readonly Dictionary<Guid, ImageSource?> Cache = new();

    public static ImageSource? GetIcon(Guid classGuid)
    {
        if (classGuid == Guid.Empty)
            return null;

        if (Cache.TryGetValue(classGuid, out var cached))
            return cached;

        if (!NativeMethods.SetupDiLoadClassIcon(ref classGuid, out IntPtr hIcon, out _))
        {
            Cache[classGuid] = null;
            return null;
        }

        try
        {
            var imageSource = Imaging.CreateBitmapSourceFromHIcon(
                hIcon,
                Int32Rect.Empty,
                BitmapSizeOptions.FromWidthAndHeight(16, 16));
            imageSource.Freeze();
            Cache[classGuid] = imageSource;
            return imageSource;
        }
        finally
        {
            NativeMethods.DestroyIcon(hIcon);
        }
    }
}

public static class NativeMethods
{
    [DllImport("setupapi.dll", SetLastError = true)]
    public static extern IntPtr SetupDiGetClassDevs(
        IntPtr ClassGuid,
        string Enumerator,
        IntPtr hwndParent,
        uint Flags);

    [DllImport("setupapi.dll", SetLastError = true)]
    public static extern bool SetupDiEnumDeviceInfo(
        IntPtr DeviceInfoSet,
        uint MemberIndex,
        ref SP_DEVINFO_DATA DeviceInfoData);

    [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern bool SetupDiGetDeviceRegistryProperty(
        IntPtr DeviceInfoSet,
        ref SP_DEVINFO_DATA DeviceInfoData,
        uint Property,
        out uint PropertyRegDataType,
        byte[] PropertyBuffer,
        uint PropertyBufferSize,
        out uint RequiredSize);

    [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern bool SetupDiGetDeviceInstanceId(
        IntPtr DeviceInfoSet,
        ref SP_DEVINFO_DATA DeviceInfoData,
        StringBuilder DeviceInstanceId,
        int DeviceInstanceIdSize,
        out int RequiredSize);

    [DllImport("setupapi.dll", SetLastError = true)]
    public static extern bool SetupDiLoadClassIcon(
        ref Guid ClassGuid,
        out IntPtr LargeIcon,
        out int MiniIconIndex);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("cfgmgr32.dll")]
    public static extern int CM_Get_Parent(out uint pdnDevInst, uint dnDevInst, int ulFlags);

    [DllImport("cfgmgr32.dll")]
    public static extern int CM_Get_Child(out uint pdnDevInst, uint dnDevInst, int ulFlags);

    [DllImport("cfgmgr32.dll")]
    public static extern int CM_Get_Sibling(out uint pdnDevInst, uint dnDevInst, int ulFlags);

    [StructLayout(LayoutKind.Sequential)]
    public struct SP_DEVINFO_DATA
    {
        public uint cbSize;
        public Guid ClassGuid;
        public uint DevInst;
        public IntPtr Reserved;
    }

    public const uint DIGCF_ALLCLASSES = 0x00000004;
    public const uint DIGCF_PRESENT = 0x00000002;
    public const uint SPDRP_HARDWAREID = 0x00000001;
    public const uint SPDRP_CLASS = 0x00000007;
    public const uint SPDRP_FRIENDLYNAME = 0x0000000C;
    public const uint SPDRP_DEVICEDESC = 0x00000000;
}
