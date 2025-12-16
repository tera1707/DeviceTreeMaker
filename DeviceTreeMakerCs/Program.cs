using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DeviceTreeMakerCs
{
    internal static class Program
    {
        private static void WriteTree(DeviceNode node, TextWriter writer)
        {
            for (int i = 0; i < node.Depth; i++) writer.Write("  ");

            // 出力ラベル: ハードウェアIDの先頭を優先。なければインスタンスID。
            string label = (node.HardwareIds != null && node.HardwareIds.Count > 0)
                ? node.HardwareIds[0]
                : node.InstanceId;

            writer.Write('[');
            writer.Write(label);
            writer.Write("] ");
            writer.WriteLine(node.DisplayName);
            foreach (var child in node.Children)
            {
                WriteTree(child, writer);
            }
        }

        private static void Main()
        {
            try
            {
                using var builder = new DeviceTreeBuilder();
                var root = builder.Build(); // まず全デバイスを取得してツリー構築

                Console.WriteLine("処理を選択してください:");
                Console.WriteLine("0: 全デバイスのツリーの表示");
                Console.WriteLine("1: 指定のハードウェアIDの親デバイスの表示");
                Console.WriteLine("2: 指定のハードウェアIDの直接の子デバイスをすべて表示");
                Console.Write("入力: ");

                var choiceText = Console.ReadLine();
                if (!int.TryParse(choiceText, out var choice))
                {
                    Console.WriteLine("不正な入力です。");
                    return;
                }

                switch (choice)
                {
                    case 0:
                    {
                        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                        string filePath = Path.Combine(desktop, "device_tree.txt");
                        using var writer = new StreamWriter(filePath, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
                        WriteTree(root, writer);
                        Console.WriteLine($"出力しました: {filePath}");
                        break;
                    }
                    case 1:
                    {
                        Console.Write("ハードウェアIDを入力してください: ");
                        var hwid = Console.ReadLine();
                        if (string.IsNullOrWhiteSpace(hwid))
                        {
                            Console.WriteLine("見つかりませんでした。");
                            return;
                        }

                        var finder = new HardwareIdParentFinder(root);
                        if (!finder.TryGetParent(hwid.Trim(), out var parent, out var found))
                        {
                            Console.WriteLine("見つかりませんでした。");
                            return;
                        }

                        if (parent is null)
                        {
                            Console.WriteLine("親デバイスがいません。");
                        }
                        else
                        {
                            string label = (parent.HardwareIds != null && parent.HardwareIds.Count > 0)
                                ? parent.HardwareIds[0]
                                : parent.InstanceId;
                            Console.WriteLine("親デバイス:");
                            Console.WriteLine($"[{label}] {parent.DisplayName}");
                        }
                        break;
                    }
                    case 2:
                    {
                        Console.Write("ハードウェアIDを入力してください: ");
                        var hwid = Console.ReadLine();
                        if (string.IsNullOrWhiteSpace(hwid))
                        {
                            Console.WriteLine("入力が無効です。");
                            return;
                        }
                        // 仮実装: 処理は未実装。ここではプレースホルダーのみ表示。
                        Console.WriteLine($"(仮) 直接の子デバイスを表示: {hwid.Trim()} (未実装)");
                        break;
                    }
                    default:
                        Console.WriteLine("不正な選択です。");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }
}