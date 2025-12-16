using System;
using System.Collections.Generic;

namespace DeviceTreeMakerCs
{
    internal sealed class DeviceNode
    {
        // デバイス インスタンス ID (CM_Get_Device_ID の結果)
        public string InstanceId { get; }

        // FriendlyName または DeviceDesc（取得不可の場合は代替文字列）
        public string DisplayName { get; }

        // ハードウェアID（SPDRP_HARDWAREID, REG_MULTI_SZ）
        public IReadOnlyList<string> HardwareIds { get; }

        // ツリー上の深さ（ルート=0）
        public int Depth { get; }

        // 子ノード
        public List<DeviceNode> Children { get; } = new();

        // 追加で保持する場合の情報
        public uint? DevInstRaw { get; }
        public Guid? ClassGuid { get; }
        public uint? Status { get; }
        public uint? ProblemCode { get; }

        public DeviceNode(
            string instanceId,
            string displayName,
            int depth,
            uint? devInstRaw = null,
            Guid? classGuid = null,
            uint? status = null,
            uint? problemCode = null,
            IReadOnlyList<string>? hardwareIds = null)
        {
            InstanceId = instanceId;
            DisplayName = displayName;
            Depth = depth;
            DevInstRaw = devInstRaw;
            ClassGuid = classGuid;
            Status = status;
            ProblemCode = problemCode;
            HardwareIds = hardwareIds is null ? Array.Empty<string>() : new List<string>(hardwareIds);
        }
    }
}
