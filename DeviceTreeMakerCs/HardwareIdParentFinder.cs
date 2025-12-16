using System;
using System.Collections.Generic;

namespace DeviceTreeMakerCs
{
    /// <summary>
    /// ハードウェアIDをキーに対象デバイスとその親デバイスを検索するクラス。
    /// </summary>
    internal sealed class HardwareIdParentFinder
    {
        private readonly DeviceNode _root;

        public HardwareIdParentFinder(DeviceNode root)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
        }

        /// <summary>
        /// 指定したハードウェアID (大小無視) を持つデバイスの親デバイスを取得します。
        /// </summary>
        /// <param name="hardwareId">検索するハードウェアID。</param>
        /// <param name="parent">結果の親。親が存在しない場合は null。</param>
        /// <param name="found">一致したデバイスノード。</param>
        /// <returns>見つかった場合 true。見つからない場合 false。</returns>
        public bool TryGetParent(string hardwareId, out DeviceNode? parent, out DeviceNode? found)
        {
            parent = null;
            found = null;
            if (string.IsNullOrWhiteSpace(hardwareId)) return false;

            var stack = new Stack<(DeviceNode node, DeviceNode? parent)>();
            stack.Push((_root, null));
            var comparison = StringComparison.OrdinalIgnoreCase;

            while (stack.Count > 0)
            {
                var (node, p) = stack.Pop();
                if (node.HardwareIds != null)
                {
                    foreach (var hw in node.HardwareIds)
                    {
                        if (string.Equals(hw, hardwareId, comparison))
                        {
                            found = node;
                            parent = p; // ルートの場合は null
                            return true;
                        }
                    }
                }
                // 子を積む
                foreach (var child in node.Children)
                {
                    stack.Push((child, node));
                }
            }
            return false;
        }
    }
}
